using ClassBook.Application.DTOs;
using ClassBook.Domain.Entities;
using ClassBook.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ClassBook.Application.Facades
{
    public class AnalyticsFacade
    {
        private readonly AppDbContext _db;

        public AnalyticsFacade(AppDbContext db)
        {
            _db = db;
        }

        public async Task<DailyCompletionReportDto> GetDailyCompletionReportAsync(DateTime date)
        {
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

            var lessonsForDate = await _db.Lessons
                .AsNoTracking()
                .Where(l => l.Date >= startOfDay && l.Date <= endOfDay)
                .Include(l => l.Teacher)
                .Include(l => l.Subject)
                .Include(l => l.Class)
                .Include(l => l.Grades)
                .Include(l => l.Attendances)
                .ToListAsync();

            var classIds = lessonsForDate.Select(lesson => lesson.ClassId).Distinct().ToList();
            var studentCounts = await _db.Students
                .AsNoTracking()
                .Where(student => student.ClassId.HasValue && classIds.Contains(student.ClassId.Value))
                .GroupBy(student => student.ClassId!.Value)
                .Select(group => new { ClassId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.ClassId, item => item.Count);
            var report = new List<DailyCompletionLessonDto>();

            foreach (var lesson in lessonsForDate)
            {
                var studentsInClass = studentCounts.GetValueOrDefault(lesson.ClassId);
                var gradesCount = lesson.Grades?.Count ?? 0;
                var explicitAttendanceCount = lesson.Attendances?.Count ?? 0;
                var absentCount = lesson.Attendances?.Count(a => a.Status == 0) ?? 0;
                var attendanceCount = studentsInClass > 0 ? studentsInClass : explicitAttendanceCount;

                report.Add(new DailyCompletionLessonDto
                {
                    LessonId = lesson.LessonId,
                    Name = lesson.Subject.Name,
                    Teacher = lesson.Teacher.FullName,
                    Class = lesson.Class.Name,
                    Date = lesson.Date,
                    GradesFormed = gradesCount,
                    AttendanceRecorded = attendanceCount,
                    TotalStudents = studentsInClass,
                    GradesPercentage = studentsInClass > 0 ? Math.Round((double)gradesCount / studentsInClass * 100, 2) : 0,
                    AttendancePercentage = studentsInClass > 0 ? Math.Round((double)(studentsInClass - absentCount) / studentsInClass * 100, 2) : 0
                });
            }

            return new DailyCompletionReportDto
            {
                Date = date.Date,
                TotalLessons = lessonsForDate.Count,
                LessonsWithCompleteGrades = lessonsForDate.Count(l => (l.Grades?.Count ?? 0) > 0),
                LessonsWithCompleteAttendance = lessonsForDate.Count,
                Report = report
            };
        }

        public async Task<AttendanceStatisticsReportDto> GetAttendanceStatisticsAsync(DateTime startDate, DateTime endDate)
        {
            var classes = await _db.Classes.AsNoTracking().ToListAsync();
            var studentCounts = await _db.Students
                .AsNoTracking()
                .Where(student => student.ClassId.HasValue)
                .GroupBy(student => student.ClassId!.Value)
                .Select(group => new { ClassId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.ClassId, item => item.Count);
            var lessonCounts = await _db.Lessons
                .AsNoTracking()
                .Where(lesson => lesson.Date >= startDate && lesson.Date <= endDate)
                .GroupBy(lesson => lesson.ClassId)
                .Select(group => new { ClassId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.ClassId, item => item.Count);
            var attendanceStats = await _db.Attendances
                .AsNoTracking()
                .Where(attendance => attendance.Lesson.Date >= startDate && attendance.Lesson.Date <= endDate)
                .GroupBy(attendance => attendance.Student.ClassId)
                .Select(group => new
                {
                    ClassId = group.Key,
                    Absent = group.Count(attendance => attendance.Status == 0),
                    Excused = group.Count(attendance => attendance.Status == 2)
                })
                .ToDictionaryAsync(item => item.ClassId ?? 0, item => item);
            var stats = classes.Select(classItem =>
            {
                var totalStudents = studentCounts.GetValueOrDefault(classItem.ClassId);
                var lessonsCount = lessonCounts.GetValueOrDefault(classItem.ClassId);
                var totalRecords = totalStudents * lessonsCount;
                var classAttendance = attendanceStats.GetValueOrDefault(classItem.ClassId);
                var absentCount = classAttendance?.Absent ?? 0;
                var excusedCount = classAttendance?.Excused ?? 0;
                var presentCount = totalRecords > 0 ? totalRecords - absentCount : 0;

                return new AttendanceStatisticsItemDto
                {
                    ClassName = classItem.Name,
                    TotalStudents = totalStudents,
                    Present = presentCount,
                    Absent = absentCount,
                    Excused = excusedCount,
                    PresentPercentage = totalRecords > 0 ? Math.Round((double)presentCount / totalRecords * 100, 2) : 0,
                    AbsentPercentage = totalRecords > 0 ? Math.Round((double)absentCount / totalRecords * 100, 2) : 0
                };
            }).ToList();

            return new AttendanceStatisticsReportDto
            {
                StartDate = startDate.Date,
                EndDate = endDate.Date,
                Statistics = stats
            };
        }

        public async Task<ProblematicStudentsReportDto> GetProblematicStudentsAsync(
            DateTime startDate,
            DateTime endDate,
            int? classId = null,
            int? studentId = null,
            int? teacherId = null)
        {
            var query = _db.Students.AsQueryable();
            if (classId.HasValue)
            {
                query = query.Where(s => s.ClassId == classId.Value);
            }

            var filteredStudents = await query
                .AsNoTracking()
                .AsSplitQuery()
                .Include(s => s.Class)
                .Include(s => s.Attendances!)
                .ThenInclude(a => a.Lesson)
                .Include(s => s.Grades!)
                .ThenInclude(g => g.Lesson)
                .ToListAsync();

            if (studentId.HasValue)
            {
                filteredStudents = filteredStudents.Where(s => s.StudentId == studentId.Value).ToList();
            }

            var classIds = filteredStudents
                .Where(student => student.ClassId.HasValue)
                .Select(student => student.ClassId!.Value)
                .Distinct()
                .ToList();
            var lessonCountsByClass = await _db.Lessons
                .AsNoTracking()
                .Where(lesson => classIds.Contains(lesson.ClassId) && lesson.Date >= startDate && lesson.Date <= endDate)
                .GroupBy(lesson => lesson.ClassId)
                .Select(group => new { ClassId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.ClassId, item => item.Count);
            var students = new List<ProblematicStudentDto>();

            foreach (var student in filteredStudents)
            {
                var absences = student.Attendances
                    ?.Count(a => a.Lesson.Date >= startDate && a.Lesson.Date <= endDate && a.Status == 0) ?? 0;

                var grades = student.Grades
                    ?.Where(g => g.Lesson.Date >= startDate && g.Lesson.Date <= endDate)
                    .ToList() ?? [];

                if (teacherId.HasValue)
                {
                    grades = grades.Where(g => g.Lesson.TeacherId == teacherId.Value).ToList();
                }

                var avgGrade = grades.Count > 0 ? Math.Round((double)grades.Sum(g => g.Value) / grades.Count, 2) : 0;
                var lowGradeCount = grades.Count(g => g.Value < 3);
                var totalAttendance = student.ClassId.HasValue
                    ? lessonCountsByClass.GetValueOrDefault(student.ClassId.Value)
                    : 0;
                var absencePercentage = totalAttendance > 0
                    ? Math.Round((double)absences / totalAttendance * 100, 2)
                    : 0;

                if (absences >= 5 || lowGradeCount >= 3 || (avgGrade < 3 && avgGrade > 0))
                {
                    students.Add(new ProblematicStudentDto
                    {
                        StudentId = student.StudentId,
                        FirstName = student.FirstName,
                        LastName = student.LastName,
                        Class = student.Class?.Name ?? "",
                        Absences = absences,
                        AbsencePercentage = absencePercentage,
                        AverageGrade = avgGrade,
                        LowGrades = lowGradeCount,
                        TotalGrades = grades.Count
                    });
                }
            }

            return new ProblematicStudentsReportDto
            {
                StartDate = startDate.Date,
                EndDate = endDate.Date,
                ProblematicStudents = students
                    .OrderByDescending(s => s.Absences)
                    .ToList()
            };
        }

        public async Task<TeacherProgressReportDto> GetTeacherProgressAsync(int teacherId, DateTime startDate, DateTime endDate)
        {
            var teacher = await _db.Users.FindAsync(teacherId);
            if (teacher == null)
            {
                throw new KeyNotFoundException($"Учитель с ID {teacherId} не найден");
            }

            var lessons = await _db.Lessons
                .Where(l => l.TeacherId == teacherId &&
                           l.Date >= startDate && l.Date <= endDate)
                .Include(l => l.Grades)
                .Include(l => l.Attendances)
                .Include(l => l.Subject)
                .Include(l => l.Class)
                .ToListAsync();

            var lessonsWithCompleteGrades = lessons.Count(l => (l.Grades?.Count ?? 0) > 0);
            var lessonsWithCompleteAttendance = lessons.Count;
            var totalGradesEntered = lessons.Sum(l => l.Grades?.Count ?? 0);
            var lessonClassIds = lessons.Select(l => l.ClassId).Distinct().ToList();
            var classStudentCounts = await _db.Students
                .Where(s => s.ClassId.HasValue && lessonClassIds.Contains(s.ClassId.Value))
                .GroupBy(s => s.ClassId)
                .Select(g => new { ClassId = g.Key!.Value, Count = g.Count() })
                .ToDictionaryAsync(item => item.ClassId, item => item.Count);
            var totalAttendanceRecorded = lessons.Sum(l => classStudentCounts.GetValueOrDefault(l.ClassId));

            var subjectStats = lessons
                .GroupBy(l => l.Subject.Name)
                .Select(g => new TeacherSubjectProgressDto
                {
                    Subject = g.Key,
                    LessonCount = g.Count(),
                    GradesEntered = g.Sum(l => l.Grades?.Count ?? 0),
                    AttendanceRecorded = g.Sum(l => classStudentCounts.GetValueOrDefault(l.ClassId))
                })
                .ToList();

            return new TeacherProgressReportDto
            {
                TeacherId = teacher.Id,
                Teacher = teacher.FullName,
                StartDate = startDate.Date,
                EndDate = endDate.Date,
                TotalLessons = lessons.Count,
                LessonsWithCompleteGrades = lessonsWithCompleteGrades,
                LessonsWithCompleteAttendance = lessonsWithCompleteAttendance,
                CompletionRateGrades = lessons.Count > 0 ? Math.Round((double)lessonsWithCompleteGrades / lessons.Count * 100, 2) : 0,
                CompletionRateAttendance = lessons.Count > 0 ? Math.Round((double)lessonsWithCompleteAttendance / lessons.Count * 100, 2) : 0,
                TotalGradesEntered = totalGradesEntered,
                TotalAttendanceRecorded = totalAttendanceRecorded,
                SubjectStatistics = subjectStats
            };
        }

        public async Task<ClassSummaryReportDto> GetClassSummaryAsync(DateTime startDate, DateTime endDate)
        {
            var classes = await _db.Classes
                .AsNoTracking()
                .Include(c => c.Students)
                .ToListAsync();
            var absenceCounts = await _db.Attendances
                .AsNoTracking()
                .Where(attendance => attendance.Lesson.Date >= startDate &&
                                     attendance.Lesson.Date <= endDate &&
                                     attendance.Status == 0)
                .GroupBy(attendance => attendance.Student.ClassId)
                .Select(group => new { ClassId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.ClassId ?? 0, item => item.Count);
            var gradeStats = await _db.Grades
                .AsNoTracking()
                .Where(grade => grade.Lesson.Date >= startDate && grade.Lesson.Date <= endDate)
                .GroupBy(grade => grade.Student.ClassId)
                .Select(group => new
                {
                    ClassId = group.Key,
                    Count = group.Count(),
                    Sum = group.Sum(grade => grade.Value)
                })
                .ToDictionaryAsync(item => item.ClassId ?? 0, item => item);

            var summary = classes.Select(classItem =>
            {
                var students = classItem.Students ?? [];
                var allAbsences = absenceCounts.GetValueOrDefault(classItem.ClassId);
                var grades = gradeStats.GetValueOrDefault(classItem.ClassId);
                return new ClassSummaryItemDto
                {
                    ClassName = classItem.Name,
                    StudentCount = students.Count,
                    AverageAbsences = students.Count > 0 ? Math.Round((double)allAbsences / students.Count, 2) : 0,
                    AverageGrade = grades is { Count: > 0 } ? Math.Round((double)grades.Sum / grades.Count, 2) : 0
                };
            }).ToList();

            return new ClassSummaryReportDto
            {
                Period = $"{startDate.Date:yyyy-MM-dd} - {endDate.Date:yyyy-MM-dd}",
                ClassSummary = summary
            };
        }

        public async Task<TeacherSummaryReportDto> GetTeacherSummaryAsync(DateTime startDate, DateTime endDate)
        {
            var teachers = await _db.Users
                .Where(user => user.Role.Name == "Учитель")
                .OrderBy(user => user.FullName)
                .ToListAsync();

            var lessons = await _db.Lessons
                .Where(lesson => lesson.Date >= startDate && lesson.Date <= endDate)
                .Include(lesson => lesson.Grades)
                .Include(lesson => lesson.Attendances)
                .ToListAsync();

            var items = teachers.Select(teacher =>
            {
                var teacherLessons = lessons
                    .Where(lesson => lesson.TeacherId == teacher.Id)
                    .ToList();
                var lessonsWithGrades = teacherLessons.Count(lesson => (lesson.Grades?.Count ?? 0) > 0);
                var topicsFilled = teacherLessons.Count(lesson => !IsEmptyJournalText(lesson.Topic));
                var homeworkFilled = teacherLessons.Count(lesson => !IsEmptyJournalText(lesson.Homework));

                return new TeacherSummaryItemDto
                {
                    TeacherId = teacher.Id,
                    Teacher = teacher.FullName,
                    LessonsCount = teacherLessons.Count,
                    GradesEntered = teacherLessons.Sum(lesson => lesson.Grades?.Count ?? 0),
                    AttendanceProblems = teacherLessons.Sum(lesson => lesson.Attendances?.Count(attendance => attendance.Status != 1) ?? 0),
                    GradesCompletionPercentage = teacherLessons.Count > 0
                        ? Math.Round((double)lessonsWithGrades / teacherLessons.Count * 100, 2)
                        : 0,
                    TopicsFilled = topicsFilled,
                    HomeworkFilled = homeworkFilled,
                    TopicsCompletionPercentage = teacherLessons.Count > 0
                        ? Math.Round((double)topicsFilled / teacherLessons.Count * 100, 2)
                        : 0,
                    HomeworkCompletionPercentage = teacherLessons.Count > 0
                        ? Math.Round((double)homeworkFilled / teacherLessons.Count * 100, 2)
                        : 0
                };
            })
            .Where(item => item.LessonsCount > 0 || item.GradesEntered > 0 || item.AttendanceProblems > 0)
            .OrderByDescending(item => item.LessonsCount)
            .ToList();

            return new TeacherSummaryReportDto
            {
                StartDate = startDate.Date,
                EndDate = endDate.Date,
                Teachers = items
            };
        }

        public async Task<ClassTeacherSummaryReportDto> GetClassTeacherSummaryAsync(DateTime startDate, DateTime endDate)
        {
            var assignments = await _db.ClassTeachers
                .Include(assignment => assignment.Class)
                .ThenInclude(classItem => classItem.Students)
                .Include(assignment => assignment.Teacher)
                .OrderBy(assignment => assignment.Class.Name)
                .ThenBy(assignment => assignment.Teacher.FullName)
                .ToListAsync();

            var classIds = assignments.Select(assignment => assignment.ClassId).Distinct().ToList();
            var lessons = await _db.Lessons
                .Where(lesson => classIds.Contains(lesson.ClassId) && lesson.Date >= startDate && lesson.Date <= endDate)
                .Include(lesson => lesson.Grades)
                .Include(lesson => lesson.Attendances)
                .ToListAsync();

            var items = assignments.Select(assignment =>
            {
                var classLessons = lessons
                    .Where(lesson => lesson.ClassId == assignment.ClassId)
                    .ToList();
                var grades = classLessons.SelectMany(lesson => lesson.Grades ?? []).ToList();
                var averageGrade = grades.Count > 0
                    ? Math.Round(grades.Average(grade => grade.Value), 2)
                    : 0;

                return new ClassTeacherSummaryItemDto
                {
                    TeacherId = assignment.TeacherId,
                    Teacher = assignment.Teacher.FullName,
                    ClassName = assignment.Class.Name,
                    StudentsCount = assignment.Class.Students?.Count ?? 0,
                    LessonsCount = classLessons.Count,
                    Absences = classLessons.Sum(lesson => lesson.Attendances?.Count(attendance => attendance.Status == 0) ?? 0),
                    LowGrades = grades.Count(grade => grade.Value < 3),
                    AverageGrade = averageGrade
                };
            }).ToList();

            return new ClassTeacherSummaryReportDto
            {
                StartDate = startDate.Date,
                EndDate = endDate.Date,
                ClassTeachers = items
            };
        }

        private static bool IsEmptyJournalText(string? value)
        {
            var text = (value ?? string.Empty).Trim().ToLowerInvariant();
            return string.IsNullOrWhiteSpace(text) ||
                   text.Contains("???") ||
                   text.Contains("будет указана") ||
                   text.Contains("не указана");
        }
    }
}
