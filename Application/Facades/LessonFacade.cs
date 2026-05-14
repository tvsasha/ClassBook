using ClassBook.Application.DTOs;
using ClassBook.Domain.Constants;
using ClassBook.Domain.Entities;
using ClassBook.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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

        public async Task<LessonResponse> CreateLessonAsync(int subjectId, int classId, int teacherId, string topic, DateTime date, string? homework = null, int? userId = null)
        {
            await ValidateLessonReferencesAsync(subjectId, classId, teacherId, topic);

            var lesson = new Lesson
            {
                SubjectId = subjectId,
                ClassId = classId,
                TeacherId = teacherId,
                Topic = topic.Trim(),
                Date = date,
                Homework = string.IsNullOrWhiteSpace(homework) ? null : homework.Trim()
            };

            _db.Lessons.Add(lesson);
            await _db.SaveChangesAsync();

            if (userId.HasValue)
            {
                await _auditFacade.LogActionAsync<LessonAuditDto>(userId.Value, "Lesson", lesson.LessonId, "Create", null, BuildLessonAuditDto(lesson));
            }

            return await GetRequiredLessonResponseAsync(lesson.LessonId);
        }

        public async Task<LessonResponse> UpdateLessonAsync(int id, int subjectId, int classId, int teacherId, string topic, DateTime date, string? homework = null, int? userId = null)
        {
            var lesson = await _db.Lessons.FindAsync(id);
            if (lesson == null)
                throw new KeyNotFoundException("Урок не найден");

            await ValidateLessonReferencesAsync(subjectId, classId, teacherId, topic);

            var oldValues = BuildLessonAuditDto(lesson);

            lesson.SubjectId = subjectId;
            lesson.ClassId = classId;
            lesson.TeacherId = teacherId;
            lesson.Topic = topic.Trim();
            lesson.Date = date;
            lesson.Homework = string.IsNullOrWhiteSpace(homework) ? null : homework.Trim();

            await _db.SaveChangesAsync();

            if (userId.HasValue)
            {
                await _auditFacade.LogActionAsync<LessonAuditDto>(userId.Value, "Lesson", id, "Update", oldValues, BuildLessonAuditDto(lesson));
            }

            return await GetRequiredLessonResponseAsync(id);
        }

        public async Task DeleteLessonAsync(int lessonId, int? userId = null)
        {
            var lesson = await _db.Lessons.FindAsync(lessonId);
            if (lesson == null)
                throw new KeyNotFoundException("Урок не найден");

            var oldValues = BuildLessonAuditDto(lesson);

            _db.Lessons.Remove(lesson);
            await _db.SaveChangesAsync();

            if (userId.HasValue)
            {
                await _auditFacade.LogActionAsync<LessonAuditDto>(userId.Value, "Lesson", lessonId, "Delete", oldValues, null);
            }
        }

        public async Task<Lesson?> GetLessonByIdAsync(int lessonId)
        {
            return await _db.Lessons.FindAsync(lessonId);
        }

        public async Task<IEnumerable<TeacherLessonListItemDto>> GetLessonsForTeacherAsync(int teacherId)
        {
            return await _db.Lessons
                .Where(l => l.TeacherId == teacherId)
                .Include(l => l.Subject)
                .Include(l => l.Class)
                .Select(l => new TeacherLessonListItemDto
                {
                    LessonId = l.LessonId,
                    SubjectId = l.SubjectId,
                    SubjectName = l.Subject.Name,
                    ClassId = l.ClassId,
                    ClassName = l.Class.Name,
                    Topic = l.Topic,
                    Date = l.Date,
                    Homework = l.Homework
                })
                .OrderByDescending(l => l.Date)
                .ToListAsync();
        }

        private async Task ValidateLessonReferencesAsync(int subjectId, int classId, int teacherId, string topic)
        {
            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentException("Тема урока обязательна");

            if (!await _db.Subjects.AnyAsync(s => s.SubjectId == subjectId))
                throw new KeyNotFoundException("Предмет не найден");

            if (!await _db.Classes.AnyAsync(c => c.ClassId == classId))
                throw new KeyNotFoundException("Класс не найден");

            var teacherExists = await _db.Users.AnyAsync(u => u.Id == teacherId && u.RoleId == SystemRoleIds.Teacher);
            if (!teacherExists)
                throw new InvalidOperationException("Учитель не найден или это не учитель");

            var assignmentExists = await _db.SubjectClassAssignments.AnyAsync(a =>
                a.SubjectId == subjectId &&
                a.ClassId == classId &&
                a.TeacherId == teacherId);
            if (!assignmentExists)
                throw new InvalidOperationException("Учитель не назначен на этот предмет в выбранном классе");
        }

        private async Task<LessonResponse> GetRequiredLessonResponseAsync(int lessonId)
        {
            var lesson = await _db.Lessons
                .Where(l => l.LessonId == lessonId)
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
                .FirstOrDefaultAsync();

            return lesson ?? throw new InvalidOperationException("Не удалось загрузить данные урока");
        }

        private static LessonAuditDto BuildLessonAuditDto(Lesson lesson)
        {
            return new LessonAuditDto
            {
                SubjectId = lesson.SubjectId,
                ClassId = lesson.ClassId,
                TeacherId = lesson.TeacherId,
                Topic = lesson.Topic,
                Date = lesson.Date,
                Homework = lesson.Homework
            };
        }
    }
}
