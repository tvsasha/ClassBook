using ClassBook.Application.DTOs;
using ClassBook.Domain.Constants;
using ClassBook.Domain.Entities;
using ClassBook.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace ClassBook.Application.Facades
{
    public class FinalGradeFacade
    {
        private readonly AppDbContext _db;

        public FinalGradeFacade(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<AcademicYearDto>> GetYearsAsync()
        {
            await EnsureDefaultYearAsync();
            await EnsureYearPeriodsAsync();
            return await _db.AcademicYears
                .OrderByDescending(year => year.StartDate)
                .Select(year => new AcademicYearDto
                {
                    AcademicYearId = year.AcademicYearId,
                    Name = year.Name,
                    StartDate = year.StartDate,
                    EndDate = year.EndDate,
                    IsActive = year.IsActive,
                    Periods = year.Periods
                        .OrderBy(period => period.Type)
                        .ThenBy(period => period.Sequence)
                        .Select(period => MapPeriod(period))
                        .ToList()
                })
                .ToListAsync();
        }

        public async Task<AcademicYearDto> SaveYearAsync(SaveAcademicYearDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || dto.StartDate.Date >= dto.EndDate.Date)
                throw new ArgumentException("Проверьте название и даты учебного года");

            var year = dto.AcademicYearId.HasValue
                ? await _db.AcademicYears.FirstOrDefaultAsync(item => item.AcademicYearId == dto.AcademicYearId.Value)
                : new AcademicYear();
            if (year == null)
                throw new KeyNotFoundException("Учебный год не найден");

            if (dto.IsActive)
            {
                await _db.AcademicYears.Where(item => item.IsActive)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsActive, false));
            }

            year.Name = dto.Name.Trim();
            year.StartDate = dto.StartDate.Date;
            year.EndDate = dto.EndDate.Date;
            year.IsActive = dto.IsActive;
            if (!dto.AcademicYearId.HasValue)
                _db.AcademicYears.Add(year);

            await _db.SaveChangesAsync();
            return (await GetYearsAsync()).First(item => item.AcademicYearId == year.AcademicYearId);
        }

        public async Task<AcademicPeriodDto> SavePeriodAsync(SaveAcademicPeriodDto dto)
        {
            var type = NormalizePeriodType(dto.Type);
            if (string.IsNullOrWhiteSpace(dto.Name) || dto.Sequence < 1 || dto.StartDate.Date > dto.EndDate.Date)
                throw new ArgumentException("Проверьте название, номер и даты периода");

            var year = await _db.AcademicYears.FirstOrDefaultAsync(item => item.AcademicYearId == dto.AcademicYearId);
            if (year == null)
                throw new KeyNotFoundException("Учебный год не найден");
            if (dto.StartDate.Date < year.StartDate || dto.EndDate.Date > year.EndDate)
                throw new ArgumentException("Период должен находиться внутри учебного года");

            var period = dto.AcademicPeriodId.HasValue
                ? await _db.AcademicPeriods.FirstOrDefaultAsync(item => item.AcademicPeriodId == dto.AcademicPeriodId.Value)
                : new AcademicPeriod();
            if (period == null)
                throw new KeyNotFoundException("Учебный период не найден");

            period.AcademicYearId = dto.AcademicYearId;
            period.Name = dto.Name.Trim();
            period.Type = type;
            period.Sequence = dto.Sequence;
            period.StartDate = dto.StartDate.Date;
            period.EndDate = dto.EndDate.Date;
            period.IsClosed = dto.IsClosed;
            if (!dto.AcademicPeriodId.HasValue)
                _db.AcademicPeriods.Add(period);

            await _db.SaveChangesAsync();
            return MapPeriod(period);
        }

        public async Task DeletePeriodAsync(int periodId)
        {
            var period = await _db.AcademicPeriods.FirstOrDefaultAsync(item => item.AcademicPeriodId == periodId);
            if (period == null)
                throw new KeyNotFoundException("Учебный период не найден");
            _db.AcademicPeriods.Remove(period);
            await _db.SaveChangesAsync();
        }

        public async Task<List<FinalGradeClassDto>> GetAvailableClassesAsync(int userId, string role)
        {
            IQueryable<Class> query = _db.Classes;
            if (role == "Учитель")
            {
                var classIds = _db.SubjectClassAssignments.Where(item => item.TeacherId == userId).Select(item => item.ClassId)
                    .Union(_db.ClassTeachers.Where(item => item.TeacherId == userId).Select(item => item.ClassId));
                query = query.Where(item => classIds.Contains(item.ClassId));
            }

            var classes = await query.OrderBy(item => item.Name).Select(item => new FinalGradeClassDto
            {
                ClassId = item.ClassId,
                ClassName = item.Name,
                IsClassTeacher = role != "Учитель"
            }).ToListAsync();
            if (role == "Учитель")
            {
                var managedClassIds = await _db.ClassTeachers.Where(item => item.TeacherId == userId).Select(item => item.ClassId).ToListAsync();
                classes.ForEach(item => item.IsClassTeacher = managedClassIds.Contains(item.ClassId));
            }
            return classes;
        }

        public async Task<List<StudentFinalGradesDto>> GetClassReportAsync(int userId, string role, int classId, int periodId)
        {
            if (role == "Учитель" && !await CanTeacherViewClassAsync(userId, classId))
                throw new UnauthorizedAccessException("Класс недоступен этому учителю");
            if (role != "Учитель" && role != "Администратор" && role != "Директор")
                throw new UnauthorizedAccessException("Нет доступа к итоговым оценкам класса");

            var studentIds = await _db.Students.Where(item => item.ClassId == classId).OrderBy(item => item.LastName).ThenBy(item => item.FirstName).Select(item => item.StudentId).ToListAsync();
            var isClassTeacher = role == "Учитель" && await _db.ClassTeachers.AnyAsync(item => item.TeacherId == userId && item.ClassId == classId);
            var result = new List<StudentFinalGradesDto>();
            foreach (var studentId in studentIds)
            {
                var report = await GetStudentReportCoreAsync(studentId, periodId);
                await ApplyEditPermissionsAsync(report, userId, role);
                if (role == "Учитель" && !isClassTeacher)
                    report.Grades = report.Grades.Where(item => item.CanEdit).ToList();
                result.Add(report);
            }
            return result;
        }

        public async Task<StudentFinalGradesDto> GetStudentReportAsync(int userId, string role, int studentId, int periodId)
        {
            if (!await CanViewStudentAsync(userId, role, studentId))
                throw new UnauthorizedAccessException("Нет доступа к итоговым оценкам ученика");
            var report = await GetStudentReportCoreAsync(studentId, periodId);
            await ApplyEditPermissionsAsync(report, userId, role);
            return report;
        }

        public async Task<StudentFinalGradesDto> GetMyReportAsync(int userId, int periodId)
        {
            var studentId = await _db.Students.Where(item => item.UserId == userId).Select(item => (int?)item.StudentId).FirstOrDefaultAsync();
            if (!studentId.HasValue)
                throw new KeyNotFoundException("Профиль ученика не найден");
            return await GetStudentReportCoreAsync(studentId.Value, periodId);
        }

        public async Task SetFinalGradeAsync(int userId, string role, SetFinalGradeDto dto)
        {
            if (dto.Value < 2 || dto.Value > 5)
                throw new ArgumentException("Итоговая оценка должна быть от 2 до 5");

            var period = await _db.AcademicPeriods.FirstOrDefaultAsync(item => item.AcademicPeriodId == dto.AcademicPeriodId);
            if (period == null)
                throw new KeyNotFoundException("Учебный период не найден");
            if (period.IsClosed && role != "Администратор")
                throw new InvalidOperationException("Период закрыт администратором");

            var student = await _db.Students.FirstOrDefaultAsync(item => item.StudentId == dto.StudentId);
            if (student?.ClassId == null)
                throw new KeyNotFoundException("Ученик или его класс не найден");

            var subject = await _db.Subjects.FirstOrDefaultAsync(item => item.SubjectId == dto.SubjectId);
            if (subject == null)
                throw new KeyNotFoundException("Предмет не найден");

            if (role != "Администратор")
            {
                var canSet = role == "Учитель" && await _db.SubjectClassAssignments.AnyAsync(item =>
                    item.TeacherId == userId && item.ClassId == student.ClassId && item.SubjectId == dto.SubjectId);
                if (!canSet)
                    throw new UnauthorizedAccessException("Можно выставлять итоговые оценки только по своим предметам");
            }

            var grade = await _db.FinalGrades.FirstOrDefaultAsync(item => item.AcademicPeriodId == dto.AcademicPeriodId && item.StudentId == dto.StudentId && item.SubjectId == dto.SubjectId);
            if (grade == null)
            {
                grade = new FinalGrade
                {
                    AcademicPeriodId = dto.AcademicPeriodId,
                    StudentId = dto.StudentId,
                    SubjectId = dto.SubjectId
                };
                _db.FinalGrades.Add(grade);
            }

            grade.Value = dto.Value;
            grade.SetByUserId = userId;
            grade.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        public async Task<(byte[] Content, string FileName)> ExportStudentCsvAsync(int userId, string role, int studentId, int periodId)
        {
            var report = await GetStudentReportAsync(userId, role, studentId, periodId);
            return BuildStudentCsv(report);
        }

        public async Task<(byte[] Content, string FileName)> ExportMyCsvAsync(int userId, int periodId)
        {
            var report = await GetMyReportAsync(userId, periodId);
            return BuildStudentCsv(report);
        }

        public async Task<(byte[] Content, string FileName)> ExportClassCsvAsync(int userId, string role, int classId, int periodId)
        {
            if (role == "Учитель" && !await _db.ClassTeachers.AnyAsync(item => item.TeacherId == userId && item.ClassId == classId))
                throw new UnauthorizedAccessException("Скачать табель класса может только его классный руководитель");
            var reports = await GetClassReportAsync(userId, role, classId, periodId);
            var builder = new StringBuilder("Ученик;Класс;Период;Предмет;Итоговая оценка;Текущая средняя\r\n");
            foreach (var report in reports)
                AppendReportRows(builder, report);
            return (AddUtf8Bom(builder.ToString()), $"Табель_{SanitizeFileName(reports.FirstOrDefault()?.ClassName ?? "класс")}_{DateTime.Today:yyyyMMdd}.csv");
        }

        private async Task<StudentFinalGradesDto> GetStudentReportCoreAsync(int studentId, int periodId)
        {
            var period = await _db.AcademicPeriods.Include(item => item.AcademicYear).FirstOrDefaultAsync(item => item.AcademicPeriodId == periodId);
            if (period == null)
                throw new KeyNotFoundException("Учебный период не найден");
            var student = await _db.Students.Include(item => item.Class).FirstOrDefaultAsync(item => item.StudentId == studentId);
            if (student == null)
                throw new KeyNotFoundException("Ученик не найден");

            var subjects = student.ClassId.HasValue
                ? await _db.SubjectClassAssignments.Where(item => item.ClassId == student.ClassId)
                    .Include(item => item.Subject).Include(item => item.Teacher)
                    .GroupBy(item => new { item.SubjectId, item.Subject.Name })
                    .Select(group => new { group.Key.SubjectId, group.Key.Name, TeacherName = group.OrderBy(item => item.Teacher.FullName).Select(item => item.Teacher.FullName).First() })
                    .OrderBy(item => item.Name).ToListAsync()
                : [];
            var subjectIds = subjects.Select(item => item.SubjectId).ToList();
            var finalGrades = await _db.FinalGrades.Where(item => item.StudentId == studentId && item.AcademicPeriodId == periodId && subjectIds.Contains(item.SubjectId)).ToDictionaryAsync(item => item.SubjectId);
            var regularGrades = await _db.Grades.Where(item => item.StudentId == studentId && subjectIds.Contains(item.Lesson.SubjectId) && item.Lesson.Date >= period.StartDate && item.Lesson.Date <= period.EndDate)
                .GroupBy(item => item.Lesson.SubjectId)
                .Select(group => new { SubjectId = group.Key, Count = group.Count(), Average = group.Average(item => item.Value) })
                .ToDictionaryAsync(item => item.SubjectId);

            return new StudentFinalGradesDto
            {
                StudentId = student.StudentId,
                StudentName = $"{student.LastName} {student.FirstName}",
                ClassId = student.ClassId,
                ClassName = student.Class?.Name ?? "Без класса",
                Period = MapPeriod(period),
                Grades = subjects.Select(subject => new FinalGradeItemDto
                {
                    FinalGradeId = finalGrades.GetValueOrDefault(subject.SubjectId)?.FinalGradeId,
                    SubjectId = subject.SubjectId,
                    SubjectName = subject.Name,
                    TeacherName = subject.TeacherName,
                    Value = finalGrades.GetValueOrDefault(subject.SubjectId)?.Value,
                    CurrentAverage = regularGrades.TryGetValue(subject.SubjectId, out var stats) ? Math.Round(stats.Average, 2) : 0,
                    CurrentGradesCount = regularGrades.TryGetValue(subject.SubjectId, out stats) ? stats.Count : 0
                }).ToList()
            };
        }

        private async Task<bool> CanViewStudentAsync(int userId, string role, int studentId)
        {
            if (role is "Администратор" or "Директор")
                return true;
            if (role == "Ученик")
                return await _db.Students.AnyAsync(item => item.StudentId == studentId && item.UserId == userId);
            if (role == "Родитель")
                return await _db.StudentParents.AnyAsync(item => item.StudentId == studentId && item.ParentId == userId);
            if (role == "Учитель")
                return await _db.Students.AnyAsync(student => student.StudentId == studentId && student.ClassId != null && _db.ClassTeachers.Any(item => item.ClassId == student.ClassId && item.TeacherId == userId));
            return false;
        }

        private async Task ApplyEditPermissionsAsync(StudentFinalGradesDto report, int userId, string role)
        {
            if (role == "Администратор")
            {
                report.Grades.ForEach(item => item.CanEdit = true);
                return;
            }
            if (role != "Учитель" || !report.ClassId.HasValue)
                return;

            var editableSubjectIds = await _db.SubjectClassAssignments
                .Where(item => item.TeacherId == userId && item.ClassId == report.ClassId.Value)
                .Select(item => item.SubjectId)
                .ToListAsync();
            report.Grades.ForEach(item => item.CanEdit = editableSubjectIds.Contains(item.SubjectId));
        }

        private async Task<bool> CanTeacherViewClassAsync(int teacherId, int classId)
        {
            return await _db.ClassTeachers.AnyAsync(item => item.TeacherId == teacherId && item.ClassId == classId)
                || await _db.SubjectClassAssignments.AnyAsync(item => item.TeacherId == teacherId && item.ClassId == classId);
        }

        private async Task EnsureDefaultYearAsync()
        {
            if (await _db.AcademicYears.AnyAsync())
                return;
            var year = new AcademicYear { Name = "2025/2026", StartDate = new DateTime(2025, 9, 1), EndDate = new DateTime(2026, 8, 31), IsActive = true };
            year.Periods =
            [
                new() { Name = "1 четверть", Type = "quarter", Sequence = 1, StartDate = new DateTime(2025, 9, 1), EndDate = new DateTime(2025, 10, 26), IsClosed = true },
                new() { Name = "2 четверть", Type = "quarter", Sequence = 2, StartDate = new DateTime(2025, 11, 5), EndDate = new DateTime(2025, 12, 30), IsClosed = true },
                new() { Name = "3 четверть", Type = "quarter", Sequence = 3, StartDate = new DateTime(2026, 1, 12), EndDate = new DateTime(2026, 3, 22), IsClosed = true },
                new() { Name = "4 четверть", Type = "quarter", Sequence = 4, StartDate = new DateTime(2026, 3, 30), EndDate = new DateTime(2026, 5, 31) },
                new() { Name = "1 полугодие", Type = "semester", Sequence = 1, StartDate = new DateTime(2025, 9, 1), EndDate = new DateTime(2025, 12, 30), IsClosed = true },
                new() { Name = "2 полугодие", Type = "semester", Sequence = 2, StartDate = new DateTime(2026, 1, 12), EndDate = new DateTime(2026, 5, 31) },
                new() { Name = "Год", Type = "year", Sequence = 1, StartDate = new DateTime(2025, 9, 1), EndDate = new DateTime(2026, 5, 31) }
            ];
            _db.AcademicYears.Add(year);
            await _db.SaveChangesAsync();
            await SeedMissingFinalGradesAsync(year.Periods.Select(period => period.AcademicPeriodId));
        }

        private async Task EnsureYearPeriodsAsync()
        {
            var years = await _db.AcademicYears.Include(year => year.Periods).ToListAsync();
            var createdPeriods = new List<AcademicPeriod>();

            foreach (var year in years)
            {
                if (year.Periods.Any(period => period.Type == "year"))
                    continue;

                var lastStudyDay = new DateTime(year.EndDate.Year, 5, 31);
                if (lastStudyDay < year.StartDate || lastStudyDay > year.EndDate)
                    lastStudyDay = year.EndDate;

                var period = new AcademicPeriod
                {
                    AcademicYearId = year.AcademicYearId,
                    Name = "Год",
                    Type = "year",
                    Sequence = 1,
                    StartDate = year.StartDate,
                    EndDate = lastStudyDay,
                    IsClosed = false
                };
                year.Periods.Add(period);
                createdPeriods.Add(period);
            }

            if (createdPeriods.Count == 0)
                return;

            await _db.SaveChangesAsync();
            await SeedMissingFinalGradesAsync(createdPeriods.Select(period => period.AcademicPeriodId));
        }

        private async Task SeedMissingFinalGradesAsync(IEnumerable<int> periodIds)
        {
            var periods = await _db.AcademicPeriods
                .Where(period => periodIds.Contains(period.AcademicPeriodId))
                .ToListAsync();

            foreach (var period in periods)
            {
                var existingKeys = (await _db.FinalGrades
                    .Where(grade => grade.AcademicPeriodId == period.AcademicPeriodId)
                    .Select(grade => new { grade.StudentId, grade.SubjectId })
                    .ToListAsync())
                    .Select(item => $"{item.StudentId}:{item.SubjectId}")
                    .ToHashSet();

                var averages = await _db.Grades
                    .Where(grade => grade.Lesson.Date >= period.StartDate && grade.Lesson.Date <= period.EndDate)
                    .GroupBy(grade => new { grade.StudentId, grade.Lesson.SubjectId, grade.Lesson.Subject.TeacherId, ClassName = grade.Student.Class != null ? grade.Student.Class.Name : "" })
                    .Select(group => new
                    {
                        group.Key.StudentId,
                        group.Key.SubjectId,
                        group.Key.TeacherId,
                        group.Key.ClassName,
                        Average = group.Average(grade => grade.Value)
                    })
                    .ToListAsync();

                foreach (var item in averages.Where(item => !IsOvzClass(item.ClassName) && !existingKeys.Contains($"{item.StudentId}:{item.SubjectId}")))
                {
                    _db.FinalGrades.Add(new FinalGrade
                    {
                        AcademicPeriodId = period.AcademicPeriodId,
                        StudentId = item.StudentId,
                        SubjectId = item.SubjectId,
                        Value = Math.Clamp((int)Math.Round(item.Average, MidpointRounding.AwayFromZero), 2, 5),
                        SetByUserId = item.TeacherId,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }
            await _db.SaveChangesAsync();
        }

        private static bool IsOvzClass(string className)
        {
            var normalized = (className ?? string.Empty).Trim().ToUpperInvariant();
            return normalized is "1А" or "1Б" or "2О" || normalized.Contains("ОВЗ");
        }

        private static string NormalizePeriodType(string type) => type?.Trim().ToLowerInvariant() switch
        {
            "quarter" => "quarter",
            "semester" => "semester",
            "year" => "year",
            _ => throw new ArgumentException("Тип периода должен быть четвертью, полугодием или годом")
        };

        private static AcademicPeriodDto MapPeriod(AcademicPeriod period) => new()
        {
            AcademicPeriodId = period.AcademicPeriodId,
            AcademicYearId = period.AcademicYearId,
            Name = period.Name,
            Type = period.Type,
            Sequence = period.Sequence,
            StartDate = period.StartDate,
            EndDate = period.EndDate,
            IsClosed = period.IsClosed
        };

        private static (byte[] Content, string FileName) BuildStudentCsv(StudentFinalGradesDto report)
        {
            var builder = new StringBuilder("Ученик;Класс;Период;Предмет;Итоговая оценка;Текущая средняя\r\n");
            AppendReportRows(builder, report);
            return (AddUtf8Bom(builder.ToString()), $"Табель_{SanitizeFileName(report.StudentName)}_{SanitizeFileName(report.Period.Name)}.csv");
        }

        private static void AppendReportRows(StringBuilder builder, StudentFinalGradesDto report)
        {
            foreach (var grade in report.Grades)
                builder.AppendLine(string.Join(';', Escape(report.StudentName), Escape(report.ClassName), Escape(report.Period.Name), Escape(grade.SubjectName), grade.Value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, grade.CurrentAverage.ToString("0.00", CultureInfo.InvariantCulture)));
        }

        private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
        private static byte[] AddUtf8Bom(string value) => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(value)).ToArray();
        private static string SanitizeFileName(string value) => string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character)).Replace(' ', '_');
    }
}
