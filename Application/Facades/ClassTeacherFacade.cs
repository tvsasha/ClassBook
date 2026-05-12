using ClassBook.Application.DTOs;
using ClassBook.Domain.Constants;
using ClassBook.Domain.Entities;
using ClassBook.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ClassBook.Application.Facades
{
    public class ClassTeacherFacade
    {
        private readonly AppDbContext _db;
        private readonly LessonFacade _lessonFacade;

        public ClassTeacherFacade(AppDbContext db, LessonFacade lessonFacade)
        {
            _db = db;
            _lessonFacade = lessonFacade;
        }

        public async Task<List<ClassTeacherAssignmentDto>> GetAssignmentsAsync()
        {
            return await _db.ClassTeachers
                .Include(ct => ct.Class)
                .Include(ct => ct.Teacher)
                .OrderBy(ct => ct.Class.Name)
                .ThenBy(ct => ct.Teacher.FullName)
                .Select(ct => new ClassTeacherAssignmentDto
                {
                    ClassId = ct.ClassId,
                    ClassName = ct.Class.Name,
                    TeacherId = ct.TeacherId,
                    TeacherName = ct.Teacher.FullName
                })
                .ToListAsync();
        }

        public async Task AssignAsync(int classId, int teacherId)
        {
            var classExists = await _db.Classes.AnyAsync(c => c.ClassId == classId);
            if (!classExists)
                throw new KeyNotFoundException("Класс не найден");

            var teacherExists = await _db.Users.AnyAsync(u => u.Id == teacherId && u.RoleId == SystemRoleIds.Teacher);
            if (!teacherExists)
                throw new KeyNotFoundException("Учитель не найден");

            var exists = await _db.ClassTeachers.AnyAsync(ct => ct.ClassId == classId && ct.TeacherId == teacherId);
            if (exists)
                return;

            _db.ClassTeachers.Add(new ClassTeacher
            {
                ClassId = classId,
                TeacherId = teacherId,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        public async Task UnassignAsync(int classId, int teacherId)
        {
            var assignment = await _db.ClassTeachers
                .FirstOrDefaultAsync(ct => ct.ClassId == classId && ct.TeacherId == teacherId);
            if (assignment == null)
                throw new KeyNotFoundException("Назначение классного руководителя не найдено");

            _db.ClassTeachers.Remove(assignment);
            await _db.SaveChangesAsync();
        }

        public async Task<ClassTeacherDashboardDto> GetDashboardAsync(int teacherId)
        {
            var teacher = await _db.Users.FirstOrDefaultAsync(u => u.Id == teacherId && u.RoleId == SystemRoleIds.Teacher);
            if (teacher == null)
                throw new KeyNotFoundException("Учитель не найден");

            var classes = await _db.ClassTeachers
                .Where(ct => ct.TeacherId == teacherId)
                .Include(ct => ct.Class)
                .ThenInclude(c => c.Students)
                .Select(ct => ct.Class)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var summaries = new List<ClassTeacherClassSummaryDto>();

            foreach (var classItem in classes)
            {
                var lessons = await _db.Lessons
                    .Where(l => l.ClassId == classItem.ClassId)
                    .Include(l => l.Subject)
                    .Include(l => l.Teacher)
                    .Include(l => l.Grades)
                    .Include(l => l.Attendances)
                    .ToListAsync();

                var studentIds = classItem.Students?.Select(s => s.StudentId).ToHashSet() ?? [];
                var grades = lessons.SelectMany(l => l.Grades ?? []).Where(g => studentIds.Contains(g.StudentId)).ToList();
                var attendance = lessons.SelectMany(l => l.Attendances ?? []).Where(a => studentIds.Contains(a.StudentId)).ToList();

                summaries.Add(new ClassTeacherClassSummaryDto
                {
                    ClassId = classItem.ClassId,
                    ClassName = classItem.Name,
                    StudentsCount = classItem.Students?.Count ?? 0,
                    LessonsCount = lessons.Count,
                    GradesCount = grades.Count,
                    AverageGrade = grades.Count > 0 ? Math.Round(grades.Average(g => g.Value), 2) : 0,
                    AbsencesCount = attendance.Count(a => a.Status != 1),
                    Subjects = lessons
                        .GroupBy(l => new { l.SubjectId, l.Subject.Name, TeacherName = l.Teacher.FullName })
                        .Select(group =>
                        {
                            var subjectLessons = group.ToList();
                            var subjectGrades = subjectLessons.SelectMany(l => l.Grades ?? []).Where(g => studentIds.Contains(g.StudentId)).ToList();
                            var subjectAttendance = subjectLessons.SelectMany(l => l.Attendances ?? []).Where(a => studentIds.Contains(a.StudentId)).ToList();

                            return new ClassTeacherSubjectSummaryDto
                            {
                                SubjectName = group.Key.Name,
                                TeacherName = group.Key.TeacherName,
                                LessonsCount = subjectLessons.Count,
                                GradesCount = subjectGrades.Count,
                                AverageGrade = subjectGrades.Count > 0 ? Math.Round(subjectGrades.Average(g => g.Value), 2) : 0,
                                AbsencesCount = subjectAttendance.Count(a => a.Status != 1)
                            };
                        })
                        .OrderBy(item => item.SubjectName)
                        .ToList(),
                    Students = (classItem.Students ?? [])
                        .OrderBy(s => s.LastName)
                        .ThenBy(s => s.FirstName)
                        .Select(student =>
                        {
                            var studentGrades = grades.Where(g => g.StudentId == student.StudentId).ToList();
                            var studentAttendance = attendance.Where(a => a.StudentId == student.StudentId).ToList();

                            return new ClassTeacherStudentSummaryDto
                            {
                                StudentId = student.StudentId,
                                FullName = $"{student.LastName} {student.FirstName}",
                                GradesCount = studentGrades.Count,
                                AverageGrade = studentGrades.Count > 0 ? Math.Round(studentGrades.Average(g => g.Value), 2) : 0,
                                AbsencesCount = studentAttendance.Count(a => a.Status != 1)
                            };
                        })
                        .ToList()
                });
            }

            return new ClassTeacherDashboardDto
            {
                TeacherId = teacher.Id,
                TeacherName = teacher.FullName,
                Classes = summaries,
                OwnLessons = (await _lessonFacade.GetLessonsForTeacherAsync(teacherId)).ToList()
            };
        }
    }
}
