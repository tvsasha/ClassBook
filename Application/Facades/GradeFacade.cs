// Application/Facades/GradeFacade.cs
using ClassBook.Application.DTOs;
using ClassBook.Domain.Entities;
using ClassBook.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClassBook.Application.Facades
{
    /// <summary>
    /// Фасад для управления оценками учеников.
    /// </summary>
    public class GradeFacade
    {
        private readonly AppDbContext _db;
        private readonly AuditFacade _auditFacade;

        public GradeFacade(AppDbContext db, AuditFacade auditFacade)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _auditFacade = auditFacade ?? throw new ArgumentNullException(nameof(auditFacade));
        }

        public async Task<IEnumerable<StudentGradesForLessonDto>> GetStudentsWithGradesAsync(int lessonId)
        {
            var lesson = await _db.Lessons.FindAsync(lessonId);
            if (lesson == null)
                throw new KeyNotFoundException("Урок не найден");

            var students = await _db.Students
                .Where(s => s.ClassId == lesson.ClassId)
                .ToListAsync();

            var grades = await _db.Grades
                .Where(g => g.LessonId == lessonId)
                .ToListAsync();

            var result = students.Select(s =>
            {
                var studentGrades = grades.Where(x => x.StudentId == s.StudentId)
                    .Select(g => new GradeValueDto
                    {
                        GradeId = g.GradeId,
                        Value = g.Value
                    })
                    .ToList();

                return new StudentGradesForLessonDto
                {
                    StudentId = s.StudentId,
                    FullName = s.FirstName + " " + s.LastName,
                    Grades = studentGrades
                };
            });

            return result;
        }
        /// <summary>
        /// Добавляет оценку ученику за урок.
        /// </summary>
        public async Task<Grade> AddGradeAsync(int lessonId, int studentId, int value, int? userId = null)
        {
            if (value < 1 || value > 5)
                throw new ArgumentException("Оценка должна быть от 1 до 5");

            var lesson = await _db.Lessons.FindAsync(lessonId);
            if (lesson == null)
                throw new KeyNotFoundException("Урок не найден");

            var student = await _db.Students.FindAsync(studentId);
            if (student == null)
                throw new KeyNotFoundException("Ученик не найден");

            if (student.ClassId != lesson.ClassId)
                throw new InvalidOperationException("Ученик не принадлежит классу урока");

            // Разрешаем выставлять несколько оценок за один урок для одного ученика
            var grade = new Grade
            {
                LessonId = lessonId,
                StudentId = studentId,
                Value = value
            };

            _db.Grades.Add(grade);
            await _db.SaveChangesAsync();

            if (userId.HasValue)
            {
                await _auditFacade.LogActionAsync(userId.Value, "Grade", grade.GradeId, "Create", null, new { grade.LessonId, grade.StudentId, grade.Value });
            }

            return grade;
        }

        /// <summary>
        /// Обновляет оценку.
        /// </summary>
        public async Task UpdateGradeAsync(int gradeId, int newValue, int? userId = null)
        {
            if (newValue < 1 || newValue > 5)
                throw new ArgumentException("Оценка должна быть от 1 до 5");

            var grade = await _db.Grades.FindAsync(gradeId);
            if (grade == null)
                throw new KeyNotFoundException("Оценка не найдена");

            var oldValue = grade.Value;
            grade.Value = newValue;
            await _db.SaveChangesAsync();

            if (userId.HasValue)
            {
                await _auditFacade.LogActionAsync(userId.Value, "Grade", gradeId, "Update", new { Value = oldValue }, new { Value = newValue });
            }
        }

        public async Task<IEnumerable<Grade>> GetAllGradesByTeacherAsync(int teacherId)
        {
            return await _db.Grades
                .Include(g => g.Student)
                .Include(g => g.Lesson)
                .Where(g => g.Lesson.TeacherId == teacherId)
                .ToListAsync();
        }

        /// <summary>
        /// Удаляет оценку.
        /// </summary>
        public async Task DeleteGradeAsync(int gradeId, int? userId = null)
        {
            var grade = await _db.Grades.FindAsync(gradeId);
            if (grade == null)
                throw new KeyNotFoundException("Оценка не найдена");

            var oldValues = new { grade.GradeId, grade.LessonId, grade.StudentId, grade.Value };

            _db.Grades.Remove(grade);
            await _db.SaveChangesAsync();

            if (userId.HasValue)
            {
                await _auditFacade.LogActionAsync(userId.Value, "Grade", gradeId, "Delete", oldValues, null);
            }
        }

        /// <summary>
        /// Получает все оценки за урок.
        /// </summary>
        /// <param name="lessonId">Идентификатор урока</param>
        /// <returns>Список оценок с данными ученика</returns>
        public async Task<IEnumerable<Grade>> GetGradesForLessonAsync(int lessonId)
        {
            return await _db.Grades
                .Where(g => g.LessonId == lessonId)
                .Include(g => g.Student)
                .ToListAsync();
        }
    }
}
