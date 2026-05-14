// Application/Facades/AttendanceFacade.cs
using ClassBook.Application.DTOs;
using ClassBook.Domain.Entities;
using ClassBook.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClassBook.Application.Facades
{
    /// <summary>
    /// Фасад для управления посещаемостью.
    /// </summary>
    public class AttendanceFacade
    {
        private readonly AppDbContext _db;

        public AttendanceFacade(AppDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <summary>
        /// Отмечает посещаемость ученика на уроке.
        /// </summary>
        public async Task MarkAttendanceAsync(int lessonId, int studentId, byte status)
        {
            if (status != 0 && status != 1 && status != 2)
                throw new ArgumentException("Можно отметить только присутствие, опоздание или неявку");

            var lesson = await _db.Lessons.FindAsync(lessonId);
            if (lesson == null)
                throw new KeyNotFoundException("Урок не найден");

            var student = await _db.Students.FindAsync(studentId);
            if (student == null)
                throw new KeyNotFoundException("Ученик не найден");

            if (student.ClassId != lesson.ClassId)
                throw new InvalidOperationException("Ученик не принадлежит классу урока");

            var existing = await _db.Attendances
                .FirstOrDefaultAsync(a => a.LessonId == lessonId && a.StudentId == studentId);

            if (status == 1)
            {
                if (existing != null)
                {
                    _db.Attendances.Remove(existing);
                    await _db.SaveChangesAsync();
                }

                return;
            }

            if (existing != null)
            {
                existing.Status = status;
            }
            else
            {
                var attendance = new Attendance
                {
                    LessonId = lessonId,
                    StudentId = studentId,
                    Status = status
                };
                _db.Attendances.Add(attendance);
            }

            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Получает посещаемость за урок.
        /// </summary>
        /// <param name="lessonId">Идентификатор урока</param>
        /// <returns>Список отметок с данными ученика</returns>
        public async Task<IEnumerable<LessonAttendanceDto>> GetAttendanceForLessonAsync(int lessonId)
        {
            var lessonExists = await _db.Lessons.AnyAsync(l => l.LessonId == lessonId);
            if (!lessonExists)
                throw new KeyNotFoundException("Урок не найден");

            return await _db.Attendances
                .Where(a => a.LessonId == lessonId)
                .Include(a => a.Student)
                .Select(a => new LessonAttendanceDto
                {
                    AttendanceId = a.AttendanceId,
                    StudentId = a.StudentId,
                    StudentName = a.Student != null
                        ? (a.Student.FirstName + " " + a.Student.LastName)
                        : "Ученик не найден",
                    Status = a.Status,
                    LessonId = a.LessonId
                })
                .OrderBy(a => a.StudentName)
                .ToListAsync();
        }
    }
}
