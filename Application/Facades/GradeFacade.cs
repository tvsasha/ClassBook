// Application/Facades/GradeFacade.cs
using ClassBook.Domain.Entities;
using ClassBook.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClassBook.Application.Facades
{
    /// <summary>
    /// Фасад для управления оценками учеников.
    /// </summary>
    public class GradeFacade
    {
        private readonly AppDbContext _db;

        public GradeFacade(AppDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <summary>
        /// Добавляет оценку ученику за урок.
        /// </summary>
        public async Task<Grade> AddGradeAsync(int lessonId, int studentId, int value)
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

            if (await _db.Grades.AnyAsync(g => g.LessonId == lessonId && g.StudentId == studentId))
                throw new InvalidOperationException("Оценка уже выставлена");

            var grade = new Grade
            {
                LessonId = lessonId,
                StudentId = studentId,
                Value = value
            };

            _db.Grades.Add(grade);
            await _db.SaveChangesAsync();
            return grade;
        }

        /// <summary>
        /// Обновляет оценку.
        /// </summary>
        public async Task UpdateGradeAsync(int gradeId, int newValue)
        {
            if (newValue < 1 || newValue > 5)
                throw new ArgumentException("Оценка должна быть от 1 до 5");

            var grade = await _db.Grades.FindAsync(gradeId);
            if (grade == null)
                throw new KeyNotFoundException("Оценка не найдена");

            grade.Value = newValue;
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Удаляет оценку.
        /// </summary>
        public async Task DeleteGradeAsync(int gradeId)
        {
            var grade = await _db.Grades.FindAsync(gradeId);
            if (grade == null)
                throw new KeyNotFoundException("Оценка не найдена");

            _db.Grades.Remove(grade);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Получает все оценки за урок.
        /// </summary>
        /// <param name="lessonId">Идентификатор урока</param>
        /// <returns>Список оценок с данными ученика</returns>
        // В GradeFacade.cs
        public async Task<IEnumerable<Grade>> GetGradesForLessonAsync(int lessonId)
        {
            return await _db.Grades
                .Where(g => g.LessonId == lessonId)
                .Include(g => g.Student)
                .ToListAsync();
        }
    }
}