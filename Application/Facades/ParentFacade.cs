using ClassBook.Domain.Entities;
using ClassBook.Domain.Interfaces;
using ClassBook.Infrastructure.Data;
using ClassBook.Application.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ClassBook.Application.Facades
{
    public class ParentFacade
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher _hasher;
        private readonly ILogger<ParentFacade> _logger;

        public ParentFacade(AppDbContext db, IPasswordHasher hasher, ILogger<ParentFacade> logger)
        {
            _db = db;
            _hasher = hasher;
            _logger = logger;
        }

        /// <summary>
        /// Создает учетную запись родителя сразу из контекста конкретного ученика
        /// и тут же привязывает ее к этому ученику.
        /// </summary>
        public async Task<User> CreateParentAccountForStudentAsync(int studentId, string fullName, string login, string password)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("ФИО, логин и временный пароль обязательны");

            var student = await _db.Students
                .Include(s => s.Class)
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student == null)
                throw new KeyNotFoundException("Ученик не найден");

            var normalizedLogin = login.Trim();
            if (await _db.Users.AnyAsync(u => u.Login == normalizedLogin))
                throw new InvalidOperationException("Логин уже занят");

            var parentRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Родитель");
            if (parentRole == null)
                throw new KeyNotFoundException("Роль 'Родитель' не найдена");

            var parent = new User
            {
                Login = normalizedLogin,
                FullName = fullName.Trim(),
                PasswordHash = _hasher.Hash(password),
                RoleId = parentRole.Id,
                IsActive = true,
                MustChangePassword = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(parent);
            await _db.SaveChangesAsync();

            var studentParent = new StudentParent
            {
                StudentId = studentId,
                ParentId = parent.Id,
                CreatedAt = DateTime.UtcNow
            };

            _db.StudentParents.Add(studentParent);
            await _db.SaveChangesAsync();

            return parent;
        }

        public async Task<IssuedAccessDto> IssueParentAccountForStudentAsync(int studentId, string fullName, string? login)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                throw new ArgumentException("Укажите ФИО родителя");

            var normalizedFullName = fullName.Trim();
            var normalizedLogin = string.IsNullOrWhiteSpace(login)
                ? await GenerateUniqueLoginAsync(normalizedFullName)
                : login.Trim();
            var temporaryPassword = UserFacade.GenerateTemporaryPassword();
            var parent = await CreateParentAccountForStudentAsync(studentId, normalizedFullName, normalizedLogin, temporaryPassword);

            return new IssuedAccessDto
            {
                Id = parent.Id,
                Login = parent.Login,
                FullName = parent.FullName,
                TemporaryPassword = temporaryPassword,
                MustChangePassword = parent.MustChangePassword,
                Message = "Учетная запись родителя создана и привязана к ученику"
            };
        }

        public async Task<ParentRosterImportResultDto> ImportParentRosterDocxAsync(Stream docxStream)
        {
            var result = new ParentRosterImportResultDto();
            var rows = ExtractDocxRows(docxStream);
            var parentRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Родитель");
            if (parentRole == null)
                throw new KeyNotFoundException("Роль 'Родитель' не найдена");

            var parentsByName = await _db.Users
                .Where(u => u.RoleId == parentRole.Id)
                .ToDictionaryAsync(u => u.FullName.ToLower(), u => u);

            foreach (var row in rows.Skip(1))
            {
                if (row.Count < 6)
                    continue;

                var parentName = NormalizeName(row[1]);
                var childItems = ExtractChildren(row[4], row[5]);
                if (string.IsNullOrWhiteSpace(parentName) || childItems.Count == 0)
                {
                    result.Skipped++;
                    continue;
                }

                var matchedStudents = new List<Student>();
                foreach (var child in childItems)
                {
                    var student = await FindStudentForParentImportAsync(child.Name, child.ClassName, child.BirthDate);
                    if (student == null)
                    {
                        result.Errors.Add($"Не найден ученик: {child.Name}, {child.ClassName}, {child.BirthDate:dd.MM.yyyy}");
                        continue;
                    }

                    matchedStudents.Add(student);
                }

                if (matchedStudents.Count == 0)
                {
                    result.Skipped++;
                    continue;
                }

                User parent;
                var created = false;
                var temporaryPassword = string.Empty;
                if (!parentsByName.TryGetValue(parentName.ToLower(), out parent!))
                {
                    temporaryPassword = UserFacade.GenerateTemporaryPassword();
                    parent = new User
                    {
                        Login = await GenerateUniqueLoginAsync(parentName),
                        FullName = parentName,
                        PasswordHash = _hasher.Hash(temporaryPassword),
                        RoleId = parentRole.Id,
                        IsActive = true,
                        MustChangePassword = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.Users.Add(parent);
                    await _db.SaveChangesAsync();
                    parentsByName[parentName.ToLower()] = parent;
                    created = true;
                    result.ParentsCreated++;
                }
                else
                {
                    result.ParentsFound++;
                }

                var importedParent = new ImportedParentAccountDto
                {
                    Id = parent.Id,
                    FullName = parent.FullName,
                    Login = parent.Login,
                    TemporaryPassword = temporaryPassword,
                    Created = created
                };

                foreach (var student in matchedStudents)
                {
                    var exists = await _db.StudentParents.AnyAsync(sp => sp.StudentId == student.StudentId && sp.ParentId == parent.Id);
                    if (!exists)
                    {
                        _db.StudentParents.Add(new StudentParent
                        {
                            StudentId = student.StudentId,
                            ParentId = parent.Id,
                            CreatedAt = DateTime.UtcNow
                        });
                        result.LinksCreated++;
                    }

                    importedParent.LinkedStudents.Add($"{student.LastName} {student.FirstName}".Trim());
                }

                result.Parents.Add(importedParent);
            }

            await _db.SaveChangesAsync();
            return result;
        }

        /// <summary>
        /// Привязывает родителя к ученику
        /// </summary>
        public async Task<StudentParent> AddParentToStudentAsync(int studentId, int parentId)
        {
            // Проверяем, что студент существует
            var student = await _db.Students.FindAsync(studentId);
            if (student == null)
                throw new KeyNotFoundException($"Ученик с ID {studentId} не найден");

            // Проверяем, что родитель существует
            var parent = await _db.Users.FindAsync(parentId);
            if (parent == null)
                throw new KeyNotFoundException($"Родитель с ID {parentId} не найден");

            // Проверяем, что у родителя роль "Родитель"
            var parentRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Родитель");
            if (parent.RoleId != parentRole?.Id)
                throw new InvalidOperationException("Пользователь не является родителем");

            // Проверяем, что связь не существует
            var existing = await _db.StudentParents
                .FirstOrDefaultAsync(sp => sp.StudentId == studentId && sp.ParentId == parentId);
            if (existing != null)
                throw new InvalidOperationException("Связь ученик-родитель уже существует");

            var studentParent = new StudentParent
            {
                StudentId = studentId,
                ParentId = parentId,
                CreatedAt = DateTime.UtcNow
            };

            _db.StudentParents.Add(studentParent);
            await _db.SaveChangesAsync();

            return studentParent;
        }

        private async Task<string> GenerateUniqueLoginAsync(string fullName)
        {
            var baseLogin = Transliterate(fullName)
                .ToLowerInvariant()
                .Replace(" ", ".");
            baseLogin = System.Text.RegularExpressions.Regex.Replace(baseLogin, @"[^a-z0-9\.]", string.Empty).Trim('.');
            if (string.IsNullOrWhiteSpace(baseLogin))
                baseLogin = "parent";

            var login = baseLogin;
            var index = 1;
            while (await _db.Users.AnyAsync(u => u.Login == login))
            {
                index++;
                login = $"{baseLogin}{index}";
            }

            return login;
        }

        private async Task<Student?> FindStudentForParentImportAsync(string childName, string className, DateTime birthDate)
        {
            var normalizedChildName = NormalizeForCompare(childName);
            var classKey = NormalizeClassName(className);
            var candidates = await _db.Students
                .Include(s => s.Class)
                .Where(s => s.BirthDate.Date == birthDate.Date && s.Class != null && s.Class.Name == classKey)
                .ToListAsync();

            if (candidates.Count == 1)
                return candidates[0];

            return candidates.FirstOrDefault(student =>
            {
                var studentName = NormalizeForCompare($"{student.LastName} {student.FirstName}");
                return normalizedChildName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .All(part => studentName.Contains(part, StringComparison.OrdinalIgnoreCase));
            });
        }

        private static List<List<string>> ExtractDocxRows(Stream docxStream)
        {
            using var archive = new ZipArchive(docxStream, ZipArchiveMode.Read, leaveOpen: true);
            var entry = archive.GetEntry("word/document.xml");
            if (entry == null)
                throw new ArgumentException("В документе не найдено содержимое");

            using var reader = new StreamReader(entry.Open());
            var xml = XDocument.Parse(reader.ReadToEnd());
            var w = XNamespace.Get("http://schemas.openxmlformats.org/wordprocessingml/2006/main");

            return xml.Descendants(w + "tr")
                .Select(row => row.Elements(w + "tc")
                    .Select(cell => string.Join(" / ", cell.Descendants(w + "p")
                        .Select(paragraph => NormalizeName(string.Concat(paragraph.Descendants(w + "t").Select(t => t.Value))))
                        .Where(text => !string.IsNullOrWhiteSpace(text))))
                    .ToList())
                .Where(row => row.Any(cell => !string.IsNullOrWhiteSpace(cell)))
                .ToList();
        }

        private static List<(string Name, string ClassName, DateTime BirthDate)> ExtractChildren(string rawChildren, string rawDates)
        {
            var dates = rawDates
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => DateTime.TryParseExact(value, ["dd.MM.yyyy", "d.M.yyyy"], CultureInfo.GetCultureInfo("ru-RU"), DateTimeStyles.None, out var date) ? date : (DateTime?)null)
                .Where(date => date.HasValue)
                .Select(date => date!.Value)
                .ToList();

            var parts = rawChildren
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeName)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            var children = new List<(string Name, string ClassName, DateTime BirthDate)>();
            var nameParts = new List<string>();
            foreach (var part in parts)
            {
                if (TryReadClassName(part, out var className))
                {
                    var childIndex = children.Count;
                    if (childIndex < dates.Count && nameParts.Count > 0)
                    {
                        children.Add((NormalizeName(string.Join(" ", nameParts)), className, dates[childIndex]));
                    }

                    nameParts.Clear();
                    continue;
                }

                nameParts.Add(part);
            }

            return children;
        }

        private static bool TryReadClassName(string value, out string className)
        {
            className = string.Empty;
            var match = System.Text.RegularExpressions.Regex.Match(value, @"(\d+\s*[А-ЯЁA-Z]?)\s*класс", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success)
                return false;

            className = NormalizeClassName(match.Groups[1].Value);
            return true;
        }

        private static string NormalizeClassName(string value)
        {
            return NormalizeName(value)
                .Replace("«", string.Empty)
                .Replace("»", string.Empty)
                .Replace("\"", string.Empty)
                .Replace(" ", string.Empty)
                .ToUpperInvariant();
        }

        private static string NormalizeName(string value)
        {
            return System.Text.RegularExpressions.Regex.Replace(value.Replace("/", " ").Trim(), @"\s+", " ");
        }

        private static string NormalizeForCompare(string value)
        {
            return NormalizeName(value).ToLowerInvariant().Replace("ё", "е");
        }

        private static string Transliterate(string value)
        {
            var map = new Dictionary<char, string>
            {
                ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "e",
                ['ж'] = "zh", ['з'] = "z", ['и'] = "i", ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m",
                ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u",
                ['ф'] = "f", ['х'] = "h", ['ц'] = "c", ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "sch", ['ъ'] = "",
                ['ы'] = "y", ['ь'] = "", ['э'] = "e", ['ю'] = "yu", ['я'] = "ya"
            };
            var builder = new System.Text.StringBuilder();
            foreach (var symbol in value.ToLowerInvariant())
            {
                builder.Append(map.TryGetValue(symbol, out var replacement) ? replacement : symbol);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Получает всех учеников родителя
        /// </summary>
        public async Task<List<PortalStudentInfoDto>> GetStudentsForParentAsync(int parentId)
        {
            _logger.LogInformation("Загрузка списка детей для родителя {ParentId}", parentId);
            try
            {
                var studentParents = await _db.StudentParents
                    .Where(sp => sp.ParentId == parentId)
                    .Include(sp => sp.Student)
                    .ThenInclude(s => s.Class)
                    .ToListAsync();

                _logger.LogDebug("Для родителя {ParentId} найдено {Count} связей StudentParent", parentId, studentParents.Count);

                var students = studentParents
                    .Select(sp => new PortalStudentInfoDto
                    {
                        StudentId = sp.Student.StudentId,
                        FirstName = sp.Student.FirstName,
                        LastName = sp.Student.LastName,
                        BirthDate = sp.Student.BirthDate,
                        Class = new PortalClassDto
                        {
                            Name = sp.Student.Class?.Name ?? "Класс не определен"
                        }
                    })
                    .ToList();

                _logger.LogInformation("Для родителя {ParentId} возвращено {Count} учеников", parentId, students.Count);
                
                return students;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке детей для родителя {ParentId}", parentId);
                throw;
            }
        }

        /// <summary>
        /// Получает всех родителей ученика
        /// </summary>
        public async Task<List<User>> GetParentsForStudentAsync(int studentId)
        {
            return await _db.StudentParents
                .Where(sp => sp.StudentId == studentId)
                .Include(sp => sp.Parent)
                .Select(sp => sp.Parent)
                .ToListAsync();
        }

        /// <summary>
        /// Удаляет связь ученик-родитель
        /// </summary>
        public async Task RemoveParentFromStudentAsync(int studentId, int parentId)
        {
            var studentParent = await _db.StudentParents
                .FirstOrDefaultAsync(sp => sp.StudentId == studentId && sp.ParentId == parentId);

            if (studentParent == null)
                throw new KeyNotFoundException("Связь ученик-родитель не найдена");

            _db.StudentParents.Remove(studentParent);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Получает количество учеников у родителя
        /// </summary>
        public async Task<int> GetStudentCountForParentAsync(int parentId)
        {
            return await _db.StudentParents
                .CountAsync(sp => sp.ParentId == parentId);
        }

        /// <summary>
        /// Получает количество родителей у ученика
        /// </summary>
        public async Task<int> GetParentCountForStudentAsync(int studentId)
        {
            return await _db.StudentParents
                .CountAsync(sp => sp.StudentId == studentId);
        }

        /// <summary>
        /// Проверяет, является ли пользователь родителем конкретного ученика
        /// </summary>
        public async Task<bool> IsParentOfStudentAsync(int parentId, int studentId)
        {
            return await _db.StudentParents
                .AnyAsync(sp => sp.ParentId == parentId && sp.StudentId == studentId);
        }

        public async Task<List<PortalScheduleEntryDto>> GetStudentScheduleAsync(int studentId)
        {
            var studentClassId = await _db.Students
                .Where(s => s.StudentId == studentId)
                .Select(s => (int?)s.ClassId)
                .FirstOrDefaultAsync();

            if (!studentClassId.HasValue)
                throw new KeyNotFoundException("Ученик не найден");

            var schedule = await _db.Lessons
                .Where(l => l.ClassId == studentClassId.Value)
                .Include(l => l.Subject)
                .Include(l => l.Teacher)
                .Include(l => l.Schedule)
                .OrderBy(l => l.Date)
                .ThenBy(l => l.Schedule != null ? l.Schedule.LessonNumber : int.MaxValue)
                .Select(l => new PortalScheduleEntryDto
                {
                    LessonId = l.LessonId,
                    Subject = l.Subject.Name,
                    Teacher = l.Teacher.FullName,
                    Date = l.Date,
                    Topic = l.Topic,
                    Homework = l.Homework,
                    ScheduleId = l.ScheduleId,
                    LessonNumber = l.Schedule != null ? l.Schedule.LessonNumber : (int?)null,
                    StartTime = l.Schedule != null ? l.Schedule.StartTime.ToString(@"hh\:mm") : null,
                    EndTime = l.Schedule != null ? l.Schedule.EndTime.ToString(@"hh\:mm") : null
                })
                .ToListAsync();

            return schedule;
        }

        public async Task<List<PortalGradeEntryDto>> GetStudentGradesAsync(int studentId)
        {
            var studentExists = await _db.Students.AnyAsync(s => s.StudentId == studentId);
            if (!studentExists)
                throw new KeyNotFoundException("Ученик не найден");

            var grades = await _db.Grades
                .Where(g => g.StudentId == studentId)
                .Include(g => g.Lesson)
                .ThenInclude(l => l.Subject)
                .OrderBy(g => g.Lesson.Date)
                .Select(g => new PortalGradeEntryDto
                {
                    GradeId = g.GradeId,
                    Subject = g.Lesson.Subject.Name,
                    Value = g.Value,
                    Date = g.Lesson.Date,
                    Topic = g.Lesson.Topic
                })
                .ToListAsync();

            return grades;
        }

        public async Task<List<PortalHomeworkEntryDto>> GetStudentHomeworkAsync(int studentId)
        {
            var studentClassId = await _db.Students
                .Where(s => s.StudentId == studentId)
                .Select(s => (int?)s.ClassId)
                .FirstOrDefaultAsync();

            if (!studentClassId.HasValue)
                throw new KeyNotFoundException("Ученик не найден");

            var homework = await _db.Lessons
                .Where(l => l.ClassId == studentClassId.Value && !string.IsNullOrEmpty(l.Homework))
                .Include(l => l.Subject)
                .Include(l => l.Teacher)
                .OrderBy(l => l.Date)
                .Select(l => new PortalHomeworkEntryDto
                {
                    LessonId = l.LessonId,
                    Subject = l.Subject.Name,
                    Teacher = l.Teacher.FullName,
                    Date = l.Date,
                    Topic = l.Topic,
                    Homework = l.Homework!
                })
                .ToListAsync();

            return homework;
        }

        public async Task<List<PortalAttendanceEntryDto>> GetStudentAttendanceAsync(int studentId)
        {
            var studentAddedDate = await _db.StudentParents
                .Where(sp => sp.StudentId == studentId)
                .OrderBy(sp => sp.CreatedAt)
                .Select(sp => (DateTime?)sp.CreatedAt)
                .FirstOrDefaultAsync();

            var student = await _db.Students
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student == null)
                throw new KeyNotFoundException("Ученик не найден");

            var lessons = await _db.Lessons
                .Where(l => l.ClassId == student.ClassId && (!studentAddedDate.HasValue || l.Date >= studentAddedDate.Value))
                .Include(l => l.Subject)
                .OrderBy(l => l.Date)
                .ToListAsync();

            var attendanceRecords = await _db.Attendances
                .Where(a => a.StudentId == studentId)
                .ToListAsync();

            return lessons.Select(lesson =>
            {
                var attendance = attendanceRecords.FirstOrDefault(a => a.LessonId == lesson.LessonId);

                return new PortalAttendanceEntryDto
                {
                    LessonId = lesson.LessonId,
                    AttendanceId = attendance?.AttendanceId,
                    Subject = lesson.Subject.Name,
                    Status = attendance?.Status,
                    StatusLabel = attendance == null
                        ? "Не отмечено"
                        : (attendance.Status == 1 ? "Присутствовал"
                            : (attendance.Status == 0 ? "Отсутствовал"
                                : (attendance.Status == 2 ? "Опоздание"
                                    : "Отсутствовал по уважительной причине"))),
                    Date = lesson.Date,
                    Topic = lesson.Topic
                };
            }).OrderByDescending(l => l.Date).ToList();
        }

        /// <summary>
        /// Получает детальную информацию о связи ученик-родитель с полными данными
        /// </summary>
        public async Task<PortalStudentParentDetailDto?> GetStudentParentDetailAsync(int studentId, int parentId)
        {
            var studentParent = await _db.StudentParents
                .Where(sp => sp.StudentId == studentId && sp.ParentId == parentId)
                .Include(sp => sp.Student)
                .ThenInclude(s => s.Class)
                .Include(sp => sp.Parent)
                .FirstOrDefaultAsync();

            if (studentParent == null)
                return null;

            return new PortalStudentParentDetailDto
            {
                StudentParentId = studentParent.StudentParentId,
                Student = new PortalStudentReferenceDto
                {
                    StudentId = studentParent.Student.StudentId,
                    FirstName = studentParent.Student.FirstName,
                    LastName = studentParent.Student.LastName,
                    Class = studentParent.Student.Class.Name
                },
                Parent = new PortalParentReferenceDto
                {
                    Id = studentParent.Parent.Id,
                    FullName = studentParent.Parent.FullName,
                    Login = studentParent.Parent.Login
                },
                CreatedAt = studentParent.CreatedAt
            };
        }
    }
}
