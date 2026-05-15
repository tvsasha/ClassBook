using ClassBook.Application.DTOs;
using ClassBook.Domain.Constants;
using ClassBook.Domain.Entities;
using ClassBook.Domain.Interfaces;
using ClassBook.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ClassBook.Application.Facades
{
    /// <summary>
    /// Фасад для управления учениками.
    /// </summary>
    public class StudentFacade
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher _hasher;

        public StudentFacade(AppDbContext db, IPasswordHasher hasher)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        }

        /// <summary>
        /// Создаёт новую карточку ученика без учетной записи.
        /// </summary>
        public async Task<AdminStudentDto> CreateStudentAsync(string firstName, string lastName, DateTime birthDate, int? classId = null)
        {
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
                throw new ArgumentException("Имя и фамилия обязательны");

            if (classId.HasValue && !await _db.Classes.AnyAsync(c => c.ClassId == classId))
                throw new KeyNotFoundException("Класс не найден");

            var student = new Student
            {
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                BirthDate = birthDate,
                ClassId = classId ?? 0
            };

            _db.Students.Add(student);
            await _db.SaveChangesAsync();
            return MapStudent(student, null);
        }

        public async Task<AdminStudentDto> UpdateStudentAsync(int id, string firstName, string lastName, DateTime birthDate, int? classId)
        {
            var student = await _db.Students.FindAsync(id);
            if (student == null)
                throw new KeyNotFoundException("Ученик не найден");

            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
                throw new ArgumentException("Имя и фамилия обязательны");

            if (classId.HasValue && !await _db.Classes.AnyAsync(c => c.ClassId == classId))
                throw new KeyNotFoundException("Класс не найден");

            student.FirstName = firstName.Trim();
            student.LastName = lastName.Trim();
            student.BirthDate = birthDate;
            student.ClassId = classId ?? 0;

            await _db.SaveChangesAsync();

            string? className = null;
            if (student.ClassId > 0)
            {
                className = await _db.Classes
                    .Where(c => c.ClassId == student.ClassId)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync();
            }

            return MapStudent(student, className);
        }

        /// <summary>
        /// Создает учетную запись для существующей карточки ученика.
        /// </summary>
        public async Task<IssuedStudentAccountDto> CreateStudentAccountAsync(int studentId, string login, string password)
        {
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Логин и временный пароль обязательны");

            var student = await _db.Students
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student == null)
                throw new KeyNotFoundException("Ученик не найден");

            if (student.UserId.HasValue)
                throw new ArgumentException("Для этого ученика уже создана учетная запись");

            var normalizedLogin = login.Trim();
            if (await _db.Users.AnyAsync(u => u.Login == normalizedLogin))
                throw new ArgumentException("Логин уже занят");

            var studentRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Ученик");
            if (studentRole == null)
                throw new KeyNotFoundException("Роль 'Ученик' не найдена");

            var user = new User
            {
                Login = normalizedLogin,
                FullName = $"{student.LastName} {student.FirstName}".Trim(),
                PasswordHash = _hasher.Hash(password),
                RoleId = studentRole.Id,
                IsActive = true,
                MustChangePassword = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            student.UserId = user.Id;
            await _db.SaveChangesAsync();

            return new IssuedStudentAccountDto
            {
                Id = user.Id,
                Login = user.Login,
                FullName = user.FullName,
                MustChangePassword = user.MustChangePassword,
                Message = "Учетная запись ученика создана"
            };
        }

        public async Task<IssuedAccessDto> IssueStudentAccountAsync(int studentId, string? login)
        {
            var student = await _db.Students
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student == null)
                throw new KeyNotFoundException("Ученик не найден");

            if (student.User != null)
            {
                var password = UserFacade.GenerateTemporaryPassword();
                student.User.PasswordHash = _hasher.Hash(password);
                student.User.MustChangePassword = true;
                student.User.IsActive = true;
                await _db.SaveChangesAsync();

                return new IssuedAccessDto
                {
                    Id = student.User.Id,
                    Login = student.User.Login,
                    FullName = student.User.FullName,
                    TemporaryPassword = password,
                    MustChangePassword = true,
                    Message = "Временный пароль ученика обновлен"
                };
            }

            var generatedLogin = string.IsNullOrWhiteSpace(login)
                ? await GenerateUniqueLoginAsync($"{student.LastName} {student.FirstName}")
                : login.Trim();
            var temporaryPassword = UserFacade.GenerateTemporaryPassword();
            var created = await CreateStudentAccountAsync(studentId, generatedLogin, temporaryPassword);

            return new IssuedAccessDto
            {
                Id = created.Id,
                Login = created.Login,
                FullName = created.FullName,
                TemporaryPassword = temporaryPassword,
                MustChangePassword = created.MustChangePassword,
                Message = "Учетная запись ученика создана"
            };
        }

        public async Task<IssuedStudentAccountDto> AttachStudentAccountAsync(int studentId, int userId)
        {
            var student = await _db.Students
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student == null)
                throw new KeyNotFoundException("Ученик не найден");

            if (student.UserId.HasValue)
                throw new ArgumentException("К этому ученику уже привязана учетная запись");

            var user = await _db.Users
                .Include(u => u.Role)
                .Include(u => u.Student)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                throw new KeyNotFoundException("Пользователь не найден");

            if (user.Role?.Name != "Ученик")
                throw new ArgumentException("К карточке ученика можно привязать только пользователя с ролью 'Ученик'");

            if (user.Student != null)
                throw new ArgumentException("Эта учетная запись уже привязана к другому ученику");

            student.UserId = user.Id;
            await _db.SaveChangesAsync();

            return new IssuedStudentAccountDto
            {
                Id = user.Id,
                Login = user.Login,
                FullName = user.FullName,
                MustChangePassword = user.MustChangePassword,
                Message = "Учетная запись ученика привязана"
            };
        }

        /// <summary>
        /// Получает список учеников по классу.
        /// </summary>
        public async Task<IEnumerable<AdminStudentDto>> GetStudentsByClassAsync(int classId)
        {
            if (!await _db.Classes.AnyAsync(c => c.ClassId == classId))
                throw new KeyNotFoundException("Класс не найден");

            return await _db.Students
                .Where(s => s.ClassId == classId)
                .Select(s => new AdminStudentDto
                {
                    StudentId = s.StudentId,
                    UserId = s.UserId,
                    HasAccount = s.UserId.HasValue,
                    FirstName = s.FirstName,
                    LastName = s.LastName,
                    BirthDate = s.BirthDate,
                    ClassId = s.ClassId
                })
                .OrderBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .ToListAsync();
        }

        /// <summary>
        /// Получает всех учеников с информацией о классе.
        /// </summary>
        public async Task<IEnumerable<AdminStudentDto>> GetAllStudentsAsync()
        {
            return await _db.Students
                .Include(s => s.Class)
                .Select(s => new AdminStudentDto
                {
                    StudentId = s.StudentId,
                    UserId = s.UserId,
                    HasAccount = s.UserId.HasValue,
                    FirstName = s.FirstName,
                    LastName = s.LastName,
                    BirthDate = s.BirthDate,
                    ClassId = s.ClassId,
                    ClassName = s.Class != null ? s.Class.Name : null
                })
                .OrderBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .ToListAsync();
        }

        public async Task<ImportStudentsResultDto> ImportStudentsAsync(string csvText, bool createMissingClasses)
        {
            if (string.IsNullOrWhiteSpace(csvText))
                throw new ArgumentException("Файл импорта пустой");

            var result = new ImportStudentsResultDto();
            var lines = csvText
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (lines.Length == 0)
                throw new ArgumentException("Файл импорта не содержит строк");

            var startIndex = IsStudentImportHeader(lines[0]) ? 1 : 0;
            var classes = await _db.Classes.ToDictionaryAsync(c => c.Name.ToLower(), c => c);
            var seenRows = new HashSet<string>();

            for (var index = startIndex; index < lines.Length; index++)
            {
                var rowNumber = index + 1;
                var values = SplitStudentImportLine(lines[index]);
                if (values.Count < 4)
                {
                    result.Skipped++;
                    result.Errors.Add($"Строка {rowNumber}: нужно указать фамилию, имя, дату рождения и класс");
                    continue;
                }

                var lastName = values[0].Trim();
                var firstName = values[1].Trim();
                var birthDateText = values[2].Trim();
                var className = values[3].Trim();

                if (string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(className))
                {
                    result.Skipped++;
                    result.Errors.Add($"Строка {rowNumber}: фамилия, имя и класс обязательны");
                    continue;
                }

                if (!TryParseStudentBirthDate(birthDateText, out var birthDate))
                {
                    result.Skipped++;
                    result.Errors.Add($"Строка {rowNumber}: дата рождения должна быть в формате дд.мм.гггг или гггг-мм-дд");
                    continue;
                }

                if (!classes.TryGetValue(className.ToLower(), out var classEntity))
                {
                    if (!createMissingClasses)
                    {
                        result.Skipped++;
                        result.Errors.Add($"Строка {rowNumber}: класс '{className}' не найден");
                        continue;
                    }

                    classEntity = new Class { Name = className };
                    _db.Classes.Add(classEntity);
                    classes[className.ToLower()] = classEntity;
                }

                var importKey = $"{lastName}|{firstName}|{birthDate:yyyy-MM-dd}|{className}".ToLowerInvariant();
                if (!seenRows.Add(importKey))
                {
                    result.Skipped++;
                    result.Errors.Add($"Строка {rowNumber}: такая строка уже встречалась в файле");
                    continue;
                }

                var exists = await _db.Students.AnyAsync(s =>
                    s.LastName == lastName &&
                    s.FirstName == firstName &&
                    s.BirthDate.Date == birthDate.Date &&
                    s.ClassId == classEntity.ClassId);

                if (exists)
                {
                    result.Skipped++;
                    result.Errors.Add($"Строка {rowNumber}: такой ученик уже есть в выбранном классе");
                    continue;
                }

                _db.Students.Add(new Student
                {
                    LastName = lastName,
                    FirstName = firstName,
                    BirthDate = birthDate,
                    Class = classEntity
                });
                result.Imported++;
            }

            await _db.SaveChangesAsync();
            return result;
        }

        public async Task<string> ExportStudentsCsvAsync()
        {
            var students = await _db.Students
                .Include(s => s.Class)
                .OrderBy(s => s.Class.Name)
                .ThenBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .ToListAsync();

            var builder = new StringBuilder();
            builder.AppendLine("Фамилия;Имя;Дата рождения;Класс;Аккаунт");

            foreach (var student in students)
            {
                builder.Append(EscapeCsv(student.LastName)).Append(';')
                    .Append(EscapeCsv(student.FirstName)).Append(';')
                    .Append(student.BirthDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)).Append(';')
                    .Append(EscapeCsv(student.Class?.Name ?? string.Empty)).Append(';')
                    .Append(student.UserId.HasValue ? "Есть" : "Не выдан")
                    .AppendLine();
            }

            return builder.ToString();
        }

        public async Task<byte[]> ExportSchoolRosterDocxAsync()
        {
            var classes = await _db.Classes
                .Include(c => c.Students)
                .Include(c => c.ClassTeachers!)
                .ThenInclude(ct => ct.Teacher)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var body = new StringBuilder();
            foreach (var classItem in classes)
            {
                var teacherNames = classItem.ClassTeachers?
                    .Select(ct => ct.Teacher.FullName)
                    .OrderBy(name => name)
                    .ToList() ?? [];

                if (teacherNames.Count > 0)
                    AppendDocxParagraph(body, string.Join(", ", teacherNames));

                AppendDocxTableStart(body);
                AppendDocxRow(body, ["№", "ФИО", "Класс", "Дата рождения"]);

                var students = (classItem.Students ?? [])
                    .OrderBy(s => s.LastName)
                    .ThenBy(s => s.FirstName)
                    .ToList();

                for (var index = 0; index < students.Count; index++)
                {
                    var student = students[index];
                    AppendDocxRow(body, [
                        (index + 1).ToString(CultureInfo.InvariantCulture),
                        $"{student.LastName} {student.FirstName}",
                        classItem.Name,
                        student.BirthDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
                    ]);
                }

                AppendDocxTableEnd(body);
            }

            var documentXml = $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
  <w:body>
    {body}
    <w:sectPr/>
  </w:body>
</w:document>";

            using var memory = new MemoryStream();
            using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
            {
                WriteZipEntry(archive, "[Content_Types].xml", @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
  <Default Extension=""xml"" ContentType=""application/xml""/>
  <Override PartName=""/word/document.xml"" ContentType=""application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml""/>
</Types>");
                WriteZipEntry(archive, "_rels/.rels", @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""word/document.xml""/>
</Relationships>");
                WriteZipEntry(archive, "word/document.xml", documentXml);
            }

            return memory.ToArray();
        }

        public async Task<SchoolRosterImportResultDto> ImportSchoolRosterDocxAsync(Stream docxStream)
        {
            var result = new SchoolRosterImportResultDto();
            var teacherRole = await _db.Roles.FirstOrDefaultAsync(r => r.Id == SystemRoleIds.Teacher);
            if (teacherRole == null)
                throw new KeyNotFoundException("Роль 'Учитель' не найдена");

            var rows = ExtractDocxRows(docxStream);
            var classes = await _db.Classes.ToDictionaryAsync(c => c.Name.ToLower(), c => c);
            var teacherByName = await _db.Users
                .Where(u => u.RoleId == SystemRoleIds.Teacher)
                .ToDictionaryAsync(u => u.FullName.ToLower(), u => u);
            var classTeacherKeys = (await _db.ClassTeachers
                    .Include(ct => ct.Class)
                    .Select(ct => new { ct.Class.Name, ct.TeacherId })
                    .ToListAsync())
                .Select(ct => $"{ct.Name.ToLowerInvariant()}:{ct.TeacherId}")
                .ToHashSet();
            var seenStudents = new HashSet<string>();
            var currentTeachers = new List<User>();

            foreach (var row in rows)
            {
                if (TryReadStudentRow(row, out var fullName, out var className, out var birthDate))
                {
                    if (!classes.TryGetValue(className.ToLower(), out var classEntity))
                    {
                        classEntity = new Class { Name = className };
                        _db.Classes.Add(classEntity);
                        classes[className.ToLower()] = classEntity;
                    }

                    foreach (var teacher in currentTeachers)
                    {
                        var linkKey = $"{className.ToLowerInvariant()}:{teacher.Id}";
                        if (!classTeacherKeys.Contains(linkKey))
                        {
                            _db.ClassTeachers.Add(new ClassTeacher
                            {
                                Class = classEntity,
                                Teacher = teacher,
                                CreatedAt = DateTime.UtcNow
                            });
                            classTeacherKeys.Add(linkKey);
                            result.ClassTeacherLinksCreated++;
                        }
                    }

                    var nameParts = SplitStudentFullName(fullName);
                    var key = $"{nameParts.LastName}|{nameParts.FirstName}|{birthDate:yyyy-MM-dd}|{className}".ToLowerInvariant();
                    if (!seenStudents.Add(key))
                    {
                        result.Skipped++;
                        continue;
                    }

                    var exists = await _db.Students.AnyAsync(s =>
                        s.LastName == nameParts.LastName &&
                        s.FirstName == nameParts.FirstName &&
                        s.BirthDate.Date == birthDate.Date &&
                        s.ClassId == classEntity.ClassId);

                    if (exists)
                    {
                        result.Skipped++;
                        continue;
                    }

                    _db.Students.Add(new Student
                    {
                        LastName = nameParts.LastName,
                        FirstName = nameParts.FirstName,
                        BirthDate = birthDate,
                        Class = classEntity
                    });
                    result.Imported++;
                    continue;
                }

                var teacherNames = ExtractTeacherNames(row);
                if (teacherNames.Count == 0)
                {
                    var combined = NormalizeSpaces(string.Join(" ", row));
                    if (combined.Equals("ОВЗ", StringComparison.OrdinalIgnoreCase) ||
                        combined.Contains("Общее количество", StringComparison.OrdinalIgnoreCase))
                    {
                        currentTeachers.Clear();
                    }
                    continue;
                }

                currentTeachers.Clear();
                foreach (var teacherName in teacherNames)
                {
                    var normalizedTeacherName = NormalizeSpaces(teacherName);
                    if (!teacherByName.TryGetValue(normalizedTeacherName.ToLower(), out var teacher))
                    {
                        var temporaryPassword = GenerateTemporaryPassword();
                        teacher = new User
                        {
                            FullName = normalizedTeacherName,
                            Login = await GenerateUniqueLoginAsync(normalizedTeacherName),
                            PasswordHash = _hasher.Hash(temporaryPassword),
                            RoleId = teacherRole.Id,
                            IsActive = true,
                            MustChangePassword = true,
                            CreatedAt = DateTime.UtcNow
                        };
                        _db.Users.Add(teacher);
                        await _db.SaveChangesAsync();
                        teacherByName[normalizedTeacherName.ToLower()] = teacher;
                        result.TeachersCreated++;
                        result.Teachers.Add(new ImportedTeacherAccountDto
                        {
                            Id = teacher.Id,
                            FullName = teacher.FullName,
                            Login = teacher.Login,
                            TemporaryPassword = temporaryPassword,
                            Created = true
                        });
                    }
                    else
                    {
                        result.TeachersFound++;
                        result.Teachers.Add(new ImportedTeacherAccountDto
                        {
                            Id = teacher.Id,
                            FullName = teacher.FullName,
                            Login = teacher.Login,
                            Created = false
                        });
                    }

                    currentTeachers.Add(teacher);
                }
            }

            await _db.SaveChangesAsync();
            return result;
        }

        public async Task DeleteStudentAsync(int studentId)
        {
            var student = await _db.Students
                .Include(s => s.Grades)
                .Include(s => s.Attendances)
                .Include(s => s.Parents)
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student == null)
                throw new KeyNotFoundException("Ученик не найден");

            if (student.Grades != null && student.Grades.Any())
                _db.Grades.RemoveRange(student.Grades);

            if (student.Attendances != null && student.Attendances.Any())
                _db.Attendances.RemoveRange(student.Attendances);

            if (student.Parents != null && student.Parents.Any())
                _db.StudentParents.RemoveRange(student.Parents);

            _db.Students.Remove(student);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Привязывает ученика к родителю.
        /// </summary>
        public async Task AttachStudentToParentAsync(int parentId, int studentId)
        {
            var parent = await _db.Users.FirstOrDefaultAsync(u => u.Id == parentId);
            if (parent == null)
                throw new KeyNotFoundException("Родитель не найден");

            var parentRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Родитель");
            if (parentRole == null || parent.RoleId != parentRole.Id)
                throw new ArgumentException("Пользователь не является родителем");

            var student = await _db.Students.FindAsync(studentId);
            if (student == null)
                throw new KeyNotFoundException("Студент не найден");

            var existingLink = await _db.StudentParents
                .FirstOrDefaultAsync(sp => sp.StudentId == studentId && sp.ParentId == parentId);

            if (existingLink != null)
                throw new ArgumentException("Этот студент уже привязан к этому родителю");

            var studentParent = new StudentParent
            {
                StudentId = studentId,
                ParentId = parentId,
                CreatedAt = DateTime.UtcNow
            };

            _db.StudentParents.Add(studentParent);
            await _db.SaveChangesAsync();
        }

        public async Task<List<PortalScheduleEntryDto>> GetScheduleForUserAsync(int userId)
        {
            var student = await GetCurrentStudentAsync(userId);
            var schedule = await _db.Lessons
                .Where(l => l.ClassId == student.ClassId)
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

        public async Task<List<PortalGradeEntryDto>> GetGradesForUserAsync(int userId)
        {
            var student = await GetCurrentStudentAsync(userId);
            return await _db.Grades
                .Where(g => g.StudentId == student.StudentId)
                .Include(g => g.Lesson)
                .ThenInclude(l => l.Subject)
                .ThenInclude(s => s.Teacher)
                .OrderByDescending(g => g.Lesson.Date)
                .Select(g => new PortalGradeEntryDto
                {
                    GradeId = g.GradeId,
                    Subject = g.Lesson.Subject.Name,
                    Teacher = g.Lesson.Subject.Teacher.FullName,
                    Value = g.Value,
                    Date = g.Lesson.Date,
                    Topic = g.Lesson.Topic
                })
                .ToListAsync();
        }

        public async Task<List<PortalHomeworkEntryDto>> GetHomeworkForUserAsync(int userId)
        {
            var student = await GetCurrentStudentAsync(userId);
            return await _db.Lessons
                .Where(l => l.ClassId == student.ClassId && !string.IsNullOrEmpty(l.Homework))
                .Include(l => l.Subject)
                .Include(l => l.Teacher)
                .OrderByDescending(l => l.Date)
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
        }

        public async Task<List<PortalAttendanceEntryDto>> GetAttendanceForUserAsync(int userId)
        {
            var student = await GetCurrentStudentAsync(userId);
            var lessons = await _db.Lessons
                .Where(l => l.ClassId == student.ClassId)
                .Include(l => l.Subject)
                .OrderByDescending(l => l.Date)
                .ToListAsync();

            var attendanceRecords = await _db.Attendances
                .Where(a => a.StudentId == student.StudentId)
                .ToListAsync();

            return lessons.Select(lesson =>
            {
                var attendance = attendanceRecords.FirstOrDefault(a => a.LessonId == lesson.LessonId);
                var status = attendance?.Status ?? (byte)1;

                return new PortalAttendanceEntryDto
                {
                    LessonId = lesson.LessonId,
                    AttendanceId = attendance?.AttendanceId,
                    Subject = lesson.Subject.Name,
                    Status = status,
                    StatusLabel = AttendanceStatusLabel(status),
                    Date = lesson.Date,
                    Topic = lesson.Topic
                };
            }).ToList();
        }

        public async Task<PortalStudentInfoDto> GetClassInfoForUserAsync(int userId)
        {
            var student = await GetCurrentStudentAsync(userId, includeClass: true);
            return new PortalStudentInfoDto
            {
                StudentId = student.StudentId,
                FirstName = student.FirstName,
                LastName = student.LastName,
                BirthDate = student.BirthDate,
                Class = new PortalClassDto
                {
                    Name = student.Class?.Name ?? "Класс не определен"
                }
            };
        }

        private static string AttendanceStatusLabel(byte status) => status switch
        {
            0 => "Не явился",
            2 => "Опоздал",
            _ => "Присутствовал"
        };

        private static AdminStudentDto MapStudent(Student student, string? className)
        {
            return new AdminStudentDto
            {
                StudentId = student.StudentId,
                UserId = student.UserId,
                HasAccount = student.UserId.HasValue,
                FirstName = student.FirstName,
                LastName = student.LastName,
                BirthDate = student.BirthDate,
                ClassId = student.ClassId,
                ClassName = className
            };
        }

        private static bool IsStudentImportHeader(string line)
        {
            var normalized = line.ToLowerInvariant();
            return normalized.Contains("фамилия") && normalized.Contains("имя") && normalized.Contains("класс");
        }

        private static List<string> SplitStudentImportLine(string line)
        {
            var delimiter = line.Contains(';') ? ';' : ',';
            return line.Split(delimiter).Select(value => value.Trim().Trim('"')).ToList();
        }

        private static List<List<string>> ExtractDocxRows(Stream docxStream)
        {
            using var archive = new ZipArchive(docxStream, ZipArchiveMode.Read, leaveOpen: true);
            var documentEntry = archive.GetEntry("word/document.xml")
                ?? throw new ArgumentException("В документе не найден основной XML-файл Word");

            using var stream = documentEntry.Open();
            var document = XDocument.Load(stream);
            var w = XNamespace.Get("http://schemas.openxmlformats.org/wordprocessingml/2006/main");

            return document
                .Descendants(w + "tr")
                .Select(row => row.Elements(w + "tc")
                    .Select(cell => NormalizeSpaces(string.Concat(cell.Descendants(w + "t").Select(text => text.Value))))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToList())
                .Where(row => row.Count > 0)
                .ToList();
        }

        private static bool TryReadStudentRow(List<string> row, out string fullName, out string className, out DateTime birthDate)
        {
            fullName = string.Empty;
            className = string.Empty;
            birthDate = default;

            var dateIndex = row.FindIndex(value => TryParseStudentBirthDate(value, out _));
            if (dateIndex < 2)
                return false;

            var rawClassName = row[dateIndex - 1];
            var rawFullName = row[dateIndex - 2];
            if (!LooksLikePersonName(rawFullName))
                return false;

            fullName = NormalizeSpaces(rawFullName);
            className = NormalizeClassName(rawClassName);
            return !string.IsNullOrWhiteSpace(className) && TryParseStudentBirthDate(row[dateIndex], out birthDate);
        }

        private static List<string> ExtractTeacherNames(List<string> row)
        {
            var combined = NormalizeSpaces(string.Join(" ", row));
            if (string.IsNullOrWhiteSpace(combined) ||
                combined.Equals("ОВЗ", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("Общее количество", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("ФИО", StringComparison.OrdinalIgnoreCase))
            {
                return [];
            }

            var names = new List<string>();
            var tokens = combined
                .Replace(",", " ")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(token => token.Trim())
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .ToArray();

            for (var index = 0; index < tokens.Length; index++)
            {
                var token = tokens[index];
                if (!Regex.IsMatch(token, @"^[А-ЯЁ][а-яё\-]+$"))
                    continue;

                if (index + 2 < tokens.Length &&
                    Regex.IsMatch(tokens[index + 1], @"^[А-ЯЁ][а-яё\-]+$") &&
                    Regex.IsMatch(tokens[index + 2], @"^[А-ЯЁ][а-яё\-]+$"))
                {
                    var fullName = NormalizeSpaces($"{token} {tokens[index + 1]} {tokens[index + 2]}");
                    if (LooksLikePersonName(fullName))
                        names.Add(fullName);

                    index += 2;
                    continue;
                }

                if (TryReadInitials(tokens, index + 1, out var initials, out var consumed))
                {
                    var shortName = NormalizeSpaces($"{token} {initials}");
                    if (LooksLikePersonName(shortName))
                        names.Add(shortName);

                    index += consumed;
                }
            }

            return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static bool TryReadInitials(string[] tokens, int startIndex, out string initials, out int consumed)
        {
            initials = string.Empty;
            consumed = 0;

            if (startIndex >= tokens.Length)
                return false;

            var raw = tokens[startIndex].Trim(',', ';');
            consumed = 1;

            if (!HasTwoInitialLetters(raw) && startIndex + 1 < tokens.Length)
            {
                raw += tokens[startIndex + 1].Trim(',', ';');
                consumed = 2;
            }

            var letters = Regex.Matches(raw, @"[А-ЯЁ]")
                .Select(match => match.Value)
                .Take(2)
                .ToArray();

            if (letters.Length != 2)
                return false;

            initials = $"{letters[0]}.{letters[1]}.";
            return true;
        }

        private static bool HasTwoInitialLetters(string value)
        {
            return Regex.Matches(value, @"[А-ЯЁ]").Count >= 2;
        }

        private static (string LastName, string FirstName) SplitStudentFullName(string fullName)
        {
            var parts = NormalizeSpaces(fullName).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return (parts[0], string.Empty);

            return (parts[0], string.Join(' ', parts.Skip(1)));
        }

        private static string NormalizeClassName(string value)
        {
            return NormalizeSpaces(value)
                .Replace("«", string.Empty)
                .Replace("»", string.Empty)
                .Replace("\"", string.Empty)
                .Replace(" ", string.Empty)
                .ToUpperInvariant();
        }

        private static bool LooksLikePersonName(string value)
        {
            var normalized = NormalizeSpaces(value);
            return Regex.IsMatch(normalized, @"^[А-ЯЁ][А-Яа-яЁё\.\-]+(?:\s+[А-ЯЁ][А-Яа-яЁё\.\-]+){1,3}$");
        }

        private async Task<string> GenerateUniqueLoginAsync(string fullName)
        {
            var baseLogin = Transliterate(fullName)
                .ToLowerInvariant()
                .Replace(" ", ".");
            baseLogin = Regex.Replace(baseLogin, @"[^a-z0-9\.]", string.Empty).Trim('.');
            if (string.IsNullOrWhiteSpace(baseLogin))
                baseLogin = "teacher";

            var login = baseLogin;
            var index = 1;
            while (await _db.Users.AnyAsync(u => u.Login == login))
            {
                index++;
                login = $"{baseLogin}{index}";
            }

            return login;
        }

        private static string GenerateTemporaryPassword()
        {
            Span<byte> bytes = stackalloc byte[6];
            RandomNumberGenerator.Fill(bytes);
            return $"Cb-{Convert.ToHexString(bytes)}!";
        }

        private static string NormalizeSpaces(string value)
        {
            return Regex.Replace(value.Trim(), @"\s+", " ");
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
            var builder = new StringBuilder();
            foreach (var symbol in value.ToLowerInvariant())
            {
                builder.Append(map.TryGetValue(symbol, out var replacement) ? replacement : symbol);
            }

            return builder.ToString();
        }

        private static bool TryParseStudentBirthDate(string value, out DateTime birthDate)
        {
            var formats = new[] { "dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy" };
            return DateTime.TryParseExact(value, formats, CultureInfo.GetCultureInfo("ru-RU"), DateTimeStyles.None, out birthDate)
                || DateTime.TryParse(value, CultureInfo.GetCultureInfo("ru-RU"), DateTimeStyles.None, out birthDate);
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(';') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";

            return value;
        }

        private static void AppendDocxParagraph(StringBuilder builder, string text)
        {
            builder.Append("<w:p><w:r><w:t>")
                .Append(EscapeXml(text))
                .Append("</w:t></w:r></w:p>");
        }

        private static void AppendDocxTableStart(StringBuilder builder)
        {
            builder.Append("<w:tbl>");
        }

        private static void AppendDocxTableEnd(StringBuilder builder)
        {
            builder.Append("</w:tbl>");
        }

        private static void AppendDocxRow(StringBuilder builder, IEnumerable<string> cells)
        {
            builder.Append("<w:tr>");
            foreach (var cell in cells)
            {
                builder.Append("<w:tc><w:p><w:r><w:t>")
                    .Append(EscapeXml(cell))
                    .Append("</w:t></w:r></w:p></w:tc>");
            }

            builder.Append("</w:tr>");
        }

        private static void WriteZipEntry(ZipArchive archive, string path, string content)
        {
            var entry = archive.CreateEntry(path);
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(content);
        }

        private static string EscapeXml(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        private async Task<Student> GetCurrentStudentAsync(int userId, bool includeClass = false)
        {
            var query = _db.Students.AsQueryable();
            if (includeClass)
                query = query.Include(s => s.Class);

            var student = await query.FirstOrDefaultAsync(s => s.UserId == userId);
            if (student == null)
                throw new KeyNotFoundException("Карточка ученика не привязана к учетной записи");

            return student;
        }
    }
}
