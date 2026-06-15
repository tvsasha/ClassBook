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
                ScheduleId = await FindFreeScheduleSlotAsync(classId, teacherId, date),
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
            var shouldReassignSchedule = lesson.ScheduleId == null
                || lesson.ClassId != classId
                || lesson.TeacherId != teacherId
                || lesson.Date.Date != date.Date;

            lesson.SubjectId = subjectId;
            lesson.ClassId = classId;
            lesson.TeacherId = teacherId;
            if (shouldReassignSchedule)
            {
                lesson.ScheduleId = await FindFreeScheduleSlotAsync(classId, teacherId, date, id);
            }
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
                    TeacherId = l.TeacherId,
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

            await EnsureSubjectAssignmentAsync(subjectId, classId, teacherId);
        }

        private async Task EnsureSubjectAssignmentAsync(int subjectId, int classId, int teacherId)
        {
            var assignmentExists = await _db.SubjectClassAssignments.AnyAsync(assignment =>
                assignment.SubjectId == subjectId &&
                assignment.ClassId == classId &&
                assignment.TeacherId == teacherId);
            if (assignmentExists)
                return;

            _db.SubjectClassAssignments.Add(new SubjectClassAssignment
            {
                SubjectId = subjectId,
                ClassId = classId,
                TeacherId = teacherId,
                CreatedAt = DateTime.UtcNow
            });
        }

        private async Task<int?> FindFreeScheduleSlotAsync(int classId, int teacherId, DateTime date, int? ignoredLessonId = null)
        {
            var lessonDate = date.Date;
            var dayOfWeek = (int)lessonDate.DayOfWeek - 1;
            if (dayOfWeek < 0 || dayOfWeek > 4)
                return null;

            var slots = await _db.Schedules
                .Where(schedule => schedule.DayOfWeek == dayOfWeek)
                .OrderBy(schedule => schedule.LessonNumber)
                .Select(schedule => new
                {
                    schedule.ScheduleId,
                    schedule.LessonNumber
                })
                .ToListAsync();

            if (slots.Count == 0)
                return null;

            var nextDate = lessonDate.AddDays(1);
            var dayLessonsQuery = _db.Lessons
                .Where(lesson => lesson.Date >= lessonDate && lesson.Date < nextDate && lesson.ScheduleId.HasValue);

            if (ignoredLessonId.HasValue)
            {
                dayLessonsQuery = dayLessonsQuery.Where(lesson => lesson.LessonId != ignoredLessonId.Value);
            }

            var occupiedLessons = await dayLessonsQuery
                .Join(
                    _db.Schedules,
                    lesson => lesson.ScheduleId!.Value,
                    schedule => schedule.ScheduleId,
                    (lesson, schedule) => new
                    {
                        lesson.ClassId,
                        lesson.TeacherId,
                        schedule.LessonNumber
                    })
                .ToListAsync();

            var classNumbers = occupiedLessons
                .Where(lesson => lesson.ClassId == classId)
                .Select(lesson => lesson.LessonNumber)
                .ToHashSet();
            var teacherNumbers = occupiedLessons
                .Where(lesson => lesson.TeacherId == teacherId)
                .Select(lesson => lesson.LessonNumber)
                .ToHashSet();

            var maxClassLesson = classNumbers.Count == 0 ? 0 : classNumbers.Max();
            var preferredNumbers = maxClassLesson > 1
                ? Enumerable.Range(1, maxClassLesson).Where(number => !classNumbers.Contains(number))
                : Enumerable.Empty<int>();
            var orderedNumbers = preferredNumbers
                .Concat(slots.Select(slot => slot.LessonNumber))
                .Distinct()
                .ToList();

            foreach (var lessonNumber in orderedNumbers)
            {
                if (classNumbers.Contains(lessonNumber) || teacherNumbers.Contains(lessonNumber))
                    continue;

                var slot = slots.FirstOrDefault(item => item.LessonNumber == lessonNumber);
                if (slot != null)
                    return slot.ScheduleId;
            }

            return null;
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
