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
                .Where(l => l.Date >= startOfDay && l.Date <= endOfDay)
                .Include(l => l.Teacher)
                .Include(l => l.Subject)
                .Include(l => l.Class)
                .Include(l => l.Grades)
                .Include(l => l.Attendances)
                .ToListAsync();

            var report = new List<DailyCompletionLessonDto>();

            foreach (var lesson in lessonsForDate)
            {
                var studentsInClass = await _db.Students
                    .Where(s => s.ClassId == lesson.ClassId)
                    .CountAsync();

                var gradesCount = lesson.Grades?.Count ?? 0;
                var attendanceCount = lesson.Attendances?.Count ?? 0;

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
                    AttendancePercentage = studentsInClass > 0 ? Math.Round((double)attendanceCount / studentsInClass * 100, 2) : 0
                });
            }

            return new DailyCompletionReportDto
            {
                Date = date.Date,
                TotalLessons = lessonsForDate.Count,
                LessonsWithCompleteGrades = lessonsForDate.Count(l => (l.Grades?.Count ?? 0) > 0),
                LessonsWithCompleteAttendance = lessonsForDate.Count(l => (l.Attendances?.Count ?? 0) > 0),
                Report = report
            };
        }

        public async Task<AttendanceStatisticsReportDto> GetAttendanceStatisticsAsync(DateTime startDate, DateTime endDate)
        {
            var classes = await _db.Classes.ToListAsync();
            var stats = new List<AttendanceStatisticsItemDto>();

            foreach (var classItem in classes)
            {
                var attendanceRecords = await _db.Attendances
                    .Where(a => a.Student.ClassId == classItem.ClassId &&
                               a.Lesson.Date >= startDate && a.Lesson.Date <= endDate)
                    .Include(a => a.Student)
                    .Include(a => a.Lesson)
                    .ToListAsync();

                var totalStudents = await _db.Students
                    .CountAsync(s => s.ClassId == classItem.ClassId);

                var presentCount = attendanceRecords.Count(a => a.Status == 1);
                var absentCount = attendanceRecords.Count(a => a.Status == 0);
                var excusedCount = attendanceRecords.Count(a => a.Status == 2);
                var totalRecords = presentCount + absentCount + excusedCount;

                stats.Add(new AttendanceStatisticsItemDto
                {
                    ClassName = classItem.Name,
                    TotalStudents = totalStudents,
                    Present = presentCount,
                    Absent = absentCount,
                    Excused = excusedCount,
                    PresentPercentage = totalRecords > 0 ? Math.Round((double)presentCount / totalRecords * 100, 2) : 0,
                    AbsentPercentage = totalRecords > 0 ? Math.Round((double)absentCount / totalRecords * 100, 2) : 0
                });
            }

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
            var students = new List<ProblematicStudentDto>();

            var query = _db.Students.AsQueryable();
            if (classId.HasValue)
            {
                query = query.Where(s => s.ClassId == classId.Value);
            }

            var filteredStudents = await query
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
                var totalAttendance = student.Attendances
                    ?.Count(a => a.Lesson.Date >= startDate && a.Lesson.Date <= endDate) ?? 0;
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
                        Class = student.Class.Name,
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
            var lessonsWithCompleteAttendance = lessons.Count(l => (l.Attendances?.Count ?? 0) > 0);
            var totalGradesEntered = lessons.Sum(l => l.Grades?.Count ?? 0);
            var totalAttendanceRecorded = lessons.Sum(l => l.Attendances?.Count ?? 0);

            var subjectStats = lessons
                .GroupBy(l => l.Subject.Name)
                .Select(g => new TeacherSubjectProgressDto
                {
                    Subject = g.Key,
                    LessonCount = g.Count(),
                    GradesEntered = g.Sum(l => l.Grades?.Count ?? 0),
                    AttendanceRecorded = g.Sum(l => l.Attendances?.Count ?? 0)
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
                .Include(c => c.Students)
                .ToListAsync();

            var summary = new List<ClassSummaryItemDto>();

            foreach (var classItem in classes)
            {
                var students = classItem.Students ?? [];
                var avgAbsences = 0.0;
                var avgGrade = 0.0;

                if (students.Count > 0)
                {
                    var allAbsences = await _db.Attendances
                        .Where(a => a.Student.ClassId == classItem.ClassId &&
                                   a.Lesson.Date >= startDate && a.Lesson.Date <= endDate &&
                                   a.Status == 0)
                        .CountAsync();

                    var allGrades = await _db.Grades
                        .Where(g => g.Student.ClassId == classItem.ClassId &&
                                   g.Lesson.Date >= startDate && g.Lesson.Date <= endDate)
                        .ToListAsync();

                    avgAbsences = Math.Round((double)allAbsences / students.Count, 2);
                    avgGrade = allGrades.Count > 0 ? Math.Round((double)allGrades.Sum(g => g.Value) / allGrades.Count, 2) : 0;
                }

                summary.Add(new ClassSummaryItemDto
                {
                    ClassName = classItem.Name,
                    StudentCount = students.Count,
                    AverageAbsences = avgAbsences,
                    AverageGrade = avgGrade
                });
            }

            return new ClassSummaryReportDto
            {
                Period = $"{startDate.Date:yyyy-MM-dd} - {endDate.Date:yyyy-MM-dd}",
                ClassSummary = summary
            };
        }
    }
}
