using ClassBook.Controllers;
using ClassBook.Domain.Entities;
using ClassBook.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClassBook.Application.Facades
{
    public class LessonFacade
    {
        private readonly AppDbContext _db;
        private readonly AuditFacade _auditFacade;

        public LessonFacade(AppDbContext db, AuditFacade auditFacade)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _auditFacade = auditFacade ?? throw new ArgumentNullException(nameof(auditFacade));
        }

        public async Task<IEnumerable<LessonResponse>> GetAllLessonsAsync()
        {
            return await _db.Lessons
                .Include(l => l.Subject)
                .Include(l => l.Class)
                .Include(l => l.Teacher)
                .Select(l => new LessonResponse
                {
                    LessonId = l.LessonId,
                    SubjectId = l.SubjectId,
                    SubjectName = l.Subject.Name,
                    ClassId = l.ClassId,
                    ClassName = l.Class.Name,
                    TeacherId = l.TeacherId,
                    TeacherName = l.Teacher.FullName,
                    Topic = l.Topic,
                    Date = l.Date,
                    Homework = l.Homework
                })
                .ToListAsync();
        }

        public async Task<Lesson> UpdateLessonAsync(int id, int subjectId, int classId, int teacherId, string topic, DateTime date, string? homework = null, int? userId = null)
        {
            var lesson = await _db.Lessons.FindAsync(id);
            if (lesson == null) throw new KeyNotFoundException("Урок не найден");

            if (string.IsNullOrWhiteSpace(topic)) throw new ArgumentException("Тема обязательна");

            var subject = await _db.Subjects.FindAsync(subjectId);
            if (subject == null) throw new KeyNotFoundException("Предмет не найден");

            var classEntity = await _db.Classes.FindAsync(classId);
            if (classEntity == null) throw new KeyNotFoundException("Класс не найден");

            var teacher = await _db.Users.FindAsync(teacherId);
            if (teacher == null || teacher.RoleId != 2) throw new InvalidOperationException("Учитель не найден или это не учитель");

            var oldValues = new { lesson.SubjectId, lesson.ClassId, lesson.TeacherId, lesson.Topic, lesson.Date, lesson.Homework };

            lesson.SubjectId = subjectId;
            lesson.ClassId = classId;
            lesson.TeacherId = teacherId;
            lesson.Topic = topic;
            lesson.Date = date;
            lesson.Homework = homework;

            await _db.SaveChangesAsync();

            if (userId.HasValue)
            {
                await _auditFacade.LogActionAsync(userId.Value, "Lesson", id, "Update", oldValues, new { lesson.SubjectId, lesson.ClassId, lesson.TeacherId, lesson.Topic, lesson.Date, lesson.Homework });
            }

            return lesson;
        }

        /// <summary>
        /// Удаляет урок по ID.
        /// </summary>
        public async Task DeleteLessonAsync(int lessonId, int? userId = null)
        {
            var lesson = await _db.Lessons.FindAsync(lessonId);
            if (lesson == null)
                throw new KeyNotFoundException("Урок не найден");

            var oldValues = new { lesson.LessonId, lesson.SubjectId, lesson.ClassId, lesson.TeacherId, lesson.Topic, lesson.Date, lesson.Homework };

            _db.Lessons.Remove(lesson);
            await _db.SaveChangesAsync();

            if (userId.HasValue)
            {
                await _auditFacade.LogActionAsync(userId.Value, "Lesson", lessonId, "Delete", oldValues, null);
            }
        }

        public async Task<Lesson?> GetLessonByIdAsync(int lessonId)
        {
            return await _db.Lessons.FindAsync(lessonId);
        }

        public async Task<IEnumerable<object>> GetLessonsForTeacherAsync(int teacherId)
        {
            return await _db.Lessons
                .Where(l => l.TeacherId == teacherId)
                .Include(l => l.Subject)
                .Include(l => l.Class)
                .Select(l => new
                {
                    l.LessonId,
                    SubjectName = l.Subject.Name,
                    ClassName = l.Class.Name,
                    l.Topic,
                    l.Date,
                    l.Homework
                })
                .OrderByDescending(l => l.Date)
                .ToListAsync();
        }

        public async Task<Lesson> CreateLessonAsync(int subjectId, int classId, int teacherId, string topic, DateTime date, string? homework = null, int? userId = null)
        {
            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentException("Тема урока обязательна");

            var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.SubjectId == subjectId);
            if (subject == null)
                throw new KeyNotFoundException("Предмет не найден");

            var classEntity = await _db.Classes.FindAsync(classId);
            if (classEntity == null)
                throw new KeyNotFoundException("Класс не найден");

            var teacher = await _db.Users.FindAsync(teacherId);
            if (teacher == null || teacher.RoleId != 2)
                throw new InvalidOperationException("Учитель не найден или это не учитель");

            var lesson = new Lesson
            {
                SubjectId = subjectId,
                ClassId = classId,
                TeacherId = teacherId,
                Topic = topic,
                Date = date,
                Homework = homework
            };

            _db.Lessons.Add(lesson);
            await _db.SaveChangesAsync();

            if (userId.HasValue)
            {
                await _auditFacade.LogActionAsync(userId.Value, "Lesson", lesson.LessonId, "Create", null, new { lesson.SubjectId, lesson.ClassId, lesson.TeacherId, lesson.Topic, lesson.Date, lesson.Homework });
            }

            return lesson;
        }
    }
}