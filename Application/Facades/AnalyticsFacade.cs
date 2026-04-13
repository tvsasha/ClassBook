using ClassBook.Domain.Entities;
using ClassBook.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClassBook.Application.Facades
{
    public class AnalyticsFacade
    {
        private readonly AppDbContext _db;

        public AnalyticsFacade(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Ежедневный отчет: кто из учителей заполнил оценки/посещаемость, кто нет
        /// </summary>
        public async Task<dynamic> GetDailyCompletionReportAsync(DateTime date)
        {
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

            // Получаем все уроки на эту дату
            var lessonsForDate = await _db.Lessons
                .Where(l => l.Date >= startOfDay && l.Date <= endOfDay)
                .Include(l => l.Teacher)
                .Include(l => l.Subject)
                .Include(l => l.Class)
                .Include(l => l.Grades)
                .Include(l => l.Attendances)
                .ToListAsync();

            var report = new List<dynamic>();

            foreach (var lesson in lessonsForDate)
            {
                var studentsInClass = await _db.Students
                    .Where(s => s.ClassId == lesson.ClassId)
                    .CountAsync();

                var gradesCount = lesson.Grades?.Count ?? 0;
                var attendanceCount = lesson.Attendances?.Count ?? 0;

                report.Add(new
                {
                    lesson.LessonId,
                    lesson.Subject.Name,
                    Teacher = lesson.Teacher.FullName,
                    Class = lesson.Class.Name,
                    lesson.Date,
                    GradesFormed = gradesCount,
                    AttendanceRecorded = attendanceCount,
                    TotalStudents = studentsInClass,
                    GradesPercentage = studentsInClass > 0 ? Math.Round((double)gradesCount / studentsInClass * 100, 2) : 0,
                    AttendancePercentage = studentsInClass > 0 ? Math.Round((double)attendanceCount / studentsInClass * 100, 2) : 0
                });
            }

            return new
            {
                Date = date.Date,
                TotalLessons = lessonsForDate.Count,
                LessonsWithCompleteGrades = lessonsForDate.Count(l => (l.Grades?.Count ?? 0) > 0),
                LessonsWithCompleteAttendance = lessonsForDate.Count(l => (l.Attendances?.Count ?? 0) > 0),
                Report = report
            };
        }

        /// <summary>
        /// Статистика посещаемости по классам за период
        /// </summary>
        public async Task<dynamic> GetAttendanceStatisticsAsync(DateTime startDate, DateTime endDate)
        {
            var classes = await _db.Classes.ToListAsync();
            var stats = new List<dynamic>();

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

                var presentCount = attendanceRecords.Count(a => a.Status == 1); // 1 = Present
                var absentCount = attendanceRecords.Count(a => a.Status == 0); // 0 = Absent
                var excusedCount = attendanceRecords.Count(a => a.Status == 2); // 2 = Excused

                stats.Add(new
                {
                    ClassName = classItem.Name,
                    TotalStudents = totalStudents,
                    Present = presentCount,
                    Absent = absentCount,
                    Excused = excusedCount,
                    PresentPercentage = presentCount + absentCount + excusedCount > 0
                        ? Math.Round((double)presentCount / (presentCount + absentCount + excusedCount) * 100, 2)
                        : 0,
                    AbsentPercentage = presentCount + absentCount + excusedCount > 0
                        ? Math.Round((double)absentCount / (presentCount + absentCount + excusedCount) * 100, 2)
                        : 0
                });
            }

            return new
            {
                StartDate = startDate.Date,
                EndDate = endDate.Date,
                Statistics = stats
            };
        }

        /// <summary>
        /// Проблемные ученики: многие пропуски или низкие оценки за период
        /// </summary>
        public async Task<dynamic> GetProblematicStudentsAsync(DateTime startDate, DateTime endDate,
            int? classId = null, int? studentId = null, int? teacherId = null)
        {
            var students = new List<dynamic>();

            var query = _db.Students.AsQueryable();
            if (classId.HasValue)
                query = query.Where(s => s.ClassId == classId.Value);

            var filteredStudents = await query
                .Include(s => s.Class)
                .Include(s => s.Attendances)
                .ThenInclude(a => a.Lesson)
                .Include(s => s.Grades)
                .ThenInclude(g => g.Lesson)
                .ToListAsync();

            if (studentId.HasValue)
                filteredStudents = filteredStudents.Where(s => s.StudentId == studentId.Value).ToList();

            foreach (var student in filteredStudents)
            {
                var absences = student.Attendances
                    ?.Where(a => a.Lesson.Date >= startDate && a.Lesson.Date <= endDate && a.Status == 0)
                    .Count() ?? 0;

                var grades = student.Grades
                    ?.Where(g => g.Lesson.Date >= startDate && g.Lesson.Date <= endDate)
                    .ToList() ?? new List<Grade>();

                if (teacherId.HasValue)
                    grades = grades.Where(g => g.Lesson.TeacherId == teacherId.Value).ToList();

                var avgGrade = grades.Count > 0 ? Math.Round((double)grades.Sum(g => g.Value) / grades.Count, 2) : 0;
                var lowGradeCount = grades.Count(g => g.Value < 3); // Предполагаем, что 3 - минимально приемлемая оценка

                var totalAttendance = student.Attendances
                    ?.Where(a => a.Lesson.Date >= startDate && a.Lesson.Date <= endDate)
                    .Count() ?? 0;

                var absencePercentage = totalAttendance > 0
                    ? Math.Round((double)absences / totalAttendance * 100, 2)
                    : 0;

                // Считаем проблемным, если много пропусков ИЛИ низкие оценки
                if (absences >= 5 || lowGradeCount >= 3 || (avgGrade < 3 && avgGrade > 0))
                {
                    students.Add(new
                    {
                        student.StudentId,
                        student.FirstName,
                        student.LastName,
                        Class = student.Class.Name,
                        Absences = absences,
                        AbsencePercentage = absencePercentage,
                        AverageGrade = avgGrade,
                        LowGrades = lowGradeCount,
                        TotalGrades = grades.Count
                    });
                }
            }

            return new
            {
                StartDate = startDate.Date,
                EndDate = endDate.Date,
                ProblematicStudents = students.OrderByDescending(s => ((dynamic)s).Absences)
            };
        }

        /// <summary>
        /// Прогресс учителя: сколько уроков провел, сколько положил оценок/посещаемости
        /// </summary>
        public async Task<dynamic> GetTeacherProgressAsync(int teacherId, DateTime startDate, DateTime endDate)
        {
            var teacher = await _db.Users.FindAsync(teacherId);
            if (teacher == null)
                throw new KeyNotFoundException($"Учитель с ID {teacherId} не найден");

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
                .Select(g => new
                {
                    Subject = g.Key,
                    LessonCount = g.Count(),
                    GradesEntered = g.Sum(l => l.Grades?.Count ?? 0),
                    AttendanceRecorded = g.Sum(l => l.Attendances?.Count ?? 0)
                })
                .ToList();

            return new
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

        /// <summary>
        /// Сводка классов: сколько учеников, среднее количество отсутствий, средняя оценка
        /// </summary>
        public async Task<dynamic> GetClassSummaryAsync(DateTime startDate, DateTime endDate)
        {
            var classes = await _db.Classes
                .Include(c => c.Students)
                .ToListAsync();

            var summary = new List<dynamic>();

            foreach (var classItem in classes)
            {
                var students = classItem.Students ?? new List<Student>();
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

                summary.Add(new
                {
                    ClassName = classItem.Name,
                    StudentCount = students.Count,
                    AverageAbsences = avgAbsences,
                    AverageGrade = avgGrade
                });
            }

            return new
            {
                Period = $"{startDate.Date} - {endDate.Date}",
                ClassSummary = summary
            };
        }
    }
}
