using ClassBook.Application.DTOs;
using ClassBook.Domain.Constants;
using ClassBook.Domain.Entities;
using ClassBook.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace ClassBook.Application.Facades
{
    public class ScheduleFacade
    {
        private readonly AppDbContext _db;

        public ScheduleFacade(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Получает все фиксированные слоты расписания.
        /// </summary>
        public async Task<List<Schedule>> GetAllScheduleSlotsAsync()
        {
            return await _db.Schedules
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.LessonNumber)
                .ToListAsync();
        }

        /// <summary>
        /// Получает расписание на конкретный день недели.
        /// </summary>
        public async Task<List<Schedule>> GetScheduleByDayAsync(int dayOfWeek)
        {
            if (dayOfWeek < 0 || dayOfWeek > 4)
                throw new ArgumentException("День недели должен быть от 0 (Пн) до 4 (Пт)");

            return await _db.Schedules
                .Where(s => s.DayOfWeek == dayOfWeek)
                .OrderBy(s => s.LessonNumber)
                .ToListAsync();
        }

        /// <summary>
        /// Получает полное расписание на неделю (Пн-Пт).
        /// </summary>
        public async Task<Dictionary<int, List<Schedule>>> GetFullWeekScheduleAsync()
        {
            var schedules = await _db.Schedules
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.LessonNumber)
                .ToListAsync();

            var week = new Dictionary<int, List<Schedule>>();
            for (var day = 0; day < 5; day++)
            {
                week[day] = schedules.Where(s => s.DayOfWeek == day).ToList();
            }

            return week;
        }

        /// <summary>
        /// Получает расписание класса на определенную дату.
        /// </summary>
        public async Task<List<(Schedule Schedule, Lesson? Lesson)>> GetScheduleByClassAsync(int classId, DateTime? date = null)
        {
            var dayOfWeek = (int)(date?.DayOfWeek ?? DateTime.UtcNow.DayOfWeek);
            if (dayOfWeek == 0) dayOfWeek = 4;
            if (dayOfWeek > 5) dayOfWeek -= 1;

            var schedules = await _db.Schedules
                .Where(s => s.DayOfWeek == dayOfWeek)
                .OrderBy(s => s.LessonNumber)
                .ToListAsync();

            var result = new List<(Schedule, Lesson?)>();
            foreach (var schedule in schedules)
            {
                var lesson = await _db.Lessons
                    .Where(l => l.ClassId == classId && l.ScheduleId == schedule.ScheduleId &&
                                (date == null || l.Date.Date == date.Value.Date))
                    .Include(l => l.Subject)
                    .Include(l => l.Teacher)
                    .FirstOrDefaultAsync();

                result.Add((schedule, lesson));
            }

            return result;
        }

        /// <summary>
        /// Возвращает справочные данные для редактора расписания.
        /// </summary>
        public async Task<ScheduleEditorMetadataDto> GetEditorMetadataAsync()
        {
            await EnsureDefaultScheduleSlotsAsync();

            var classes = await _db.Classes
                .Select(c => new ClassListItemDto
                {
                    ClassId = c.ClassId,
                    Name = c.Name
                })
                .ToListAsync();

            var subjects = await _db.Subjects
                .Include(s => s.Teacher)
                .OrderBy(s => s.Name)
                .Select(s => new ScheduleEditorSubjectDto
                {
                    SubjectId = s.SubjectId,
                    Name = s.Name,
                    TeacherId = s.TeacherId,
                    TeacherName = s.Teacher != null ? s.Teacher.FullName : "Не назначен"
                })
                .ToListAsync();

            var teachers = await _db.Users
                .Where(u => u.RoleId == SystemRoleIds.Teacher)
                .OrderBy(u => u.FullName)
                .Select(u => new TeacherLookupDto
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Login = u.Login
                })
                .ToListAsync();

            var slots = await _db.Schedules
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.LessonNumber)
                .Select(s => new ScheduleSlotDto
                {
                    ScheduleId = s.ScheduleId,
                    DayOfWeek = s.DayOfWeek,
                    LessonNumber = s.LessonNumber,
                    StartTime = s.StartTime.ToString(@"hh\:mm"),
                    EndTime = s.EndTime.ToString(@"hh\:mm")
                })
                .ToListAsync();

            return new ScheduleEditorMetadataDto
            {
                Classes = classes
                    .OrderBy(c => ExtractClassNumber(c.Name))
                    .ThenBy(c => ExtractClassSuffix(c.Name))
                    .ThenBy(c => c.Name)
                    .ToList(),
                Subjects = subjects,
                Teachers = teachers,
                Slots = slots
            };
        }

        /// <summary>
        /// Создает новый класс из редактора расписания.
        /// </summary>
        public async Task<ClassListItemDto> CreateEditorClassAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Название класса обязательно");

            var normalizedName = name.Trim();
            var exists = await _db.Classes.AnyAsync(c => c.Name == normalizedName);
            if (exists)
                throw new InvalidOperationException("Класс с таким названием уже существует");

            var classEntity = new Class
            {
                Name = normalizedName
            };

            _db.Classes.Add(classEntity);
            await _db.SaveChangesAsync();

            return new ClassListItemDto
            {
                ClassId = classEntity.ClassId,
                Name = classEntity.Name
            };
        }

        /// <summary>
        /// Возвращает уроки недели для редактора расписания.
        /// </summary>
        public async Task<ScheduleEditorWeekDto> GetEditorWeekAsync(DateTime weekStartDate)
        {
            var normalizedWeekStart = weekStartDate.Date;
            var weekEndDate = normalizedWeekStart.AddDays(7);

            var lessons = await _db.Lessons
                .Where(l => l.Date >= normalizedWeekStart && l.Date < weekEndDate)
                .Include(l => l.Subject)
                .Include(l => l.Class)
                .Include(l => l.Teacher)
                .Include(l => l.Schedule)
                .OrderBy(l => l.Date)
                .ThenBy(l => l.Schedule != null ? l.Schedule.LessonNumber : int.MaxValue)
                .Select(l => new ScheduleEditorLessonDto
                {
                    LessonId = l.LessonId,
                    ClassId = l.ClassId,
                    ClassName = l.Class.Name,
                    SubjectId = l.SubjectId,
                    SubjectName = l.Subject.Name,
                    TeacherId = l.TeacherId,
                    TeacherName = l.Teacher.FullName,
                    ScheduleId = l.ScheduleId,
                    DayOfWeek = l.Schedule != null ? l.Schedule.DayOfWeek : (int?)null,
                    LessonNumber = l.Schedule != null ? l.Schedule.LessonNumber : (int?)null,
                    StartTime = l.Schedule != null ? l.Schedule.StartTime.ToString(@"hh\:mm") : null,
                    EndTime = l.Schedule != null ? l.Schedule.EndTime.ToString(@"hh\:mm") : null,
                    Topic = l.Topic,
                    Homework = l.Homework,
                    Date = l.Date
                })
                .ToListAsync();

            return new ScheduleEditorWeekDto
            {
                WeekStart = normalizedWeekStart,
                Lessons = lessons
            };
        }

        /// <summary>
        /// Создает урок в ячейке редактора расписания.
        /// </summary>
        public async Task<ScheduleEditorLessonMutationResultDto> CreateEditorLessonAsync(ScheduleEditorLessonRequest request)
        {
            await ValidateEditorLessonRequestAsync(request);

            var lessonDate = request.Date.Date;

            var existingLesson = await _db.Lessons
                .FirstOrDefaultAsync(l =>
                    l.ClassId == request.ClassId &&
                    l.ScheduleId == request.ScheduleId &&
                    l.Date.Date == lessonDate);

            if (existingLesson != null)
                throw new InvalidOperationException("В этом слоте уже есть урок. Используйте редактирование.");

            var lesson = new Lesson
            {
                SubjectId = request.SubjectId,
                ClassId = request.ClassId,
                TeacherId = request.TeacherId,
                ScheduleId = request.ScheduleId,
                Topic = "Тема будет указана преподавателем",
                Date = lessonDate,
                Homework = string.IsNullOrWhiteSpace(request.Homework) ? null : request.Homework.Trim()
            };

            _db.Lessons.Add(lesson);
            await _db.SaveChangesAsync();

            return new ScheduleEditorLessonMutationResultDto
            {
                Lesson = await BuildRequiredEditorLessonResponseAsync(lesson.LessonId),
                NewValues = BuildLessonAuditDto(lesson)
            };
        }

        /// <summary>
        /// Обновляет урок в редакторе расписания.
        /// </summary>
        public async Task<ScheduleEditorLessonMutationResultDto> UpdateEditorLessonAsync(int lessonId, ScheduleEditorLessonRequest request)
        {
            await ValidateEditorLessonRequestAsync(request);

            var lesson = await _db.Lessons.FindAsync(lessonId);
            if (lesson == null)
                throw new KeyNotFoundException("Урок не найден");

            var lessonDate = request.Date.Date;
            var duplicate = await _db.Lessons
                .AnyAsync(l =>
                    l.LessonId != lessonId &&
                    l.ClassId == request.ClassId &&
                    l.ScheduleId == request.ScheduleId &&
                    l.Date.Date == lessonDate);

            if (duplicate)
                throw new InvalidOperationException("В этом слоте уже есть другой урок");

            var oldValues = BuildLessonAuditDto(lesson);

            lesson.SubjectId = request.SubjectId;
            lesson.ClassId = request.ClassId;
            lesson.TeacherId = request.TeacherId;
            lesson.ScheduleId = request.ScheduleId;
            if (string.IsNullOrWhiteSpace(lesson.Topic))
                lesson.Topic = "Тема будет указана преподавателем";

            lesson.Date = lessonDate;
            lesson.Homework = string.IsNullOrWhiteSpace(request.Homework) ? null : request.Homework.Trim();

            await _db.SaveChangesAsync();

            return new ScheduleEditorLessonMutationResultDto
            {
                Lesson = await BuildRequiredEditorLessonResponseAsync(lessonId),
                OldValues = oldValues,
                NewValues = BuildLessonAuditDto(lesson)
            };
        }

        /// <summary>
        /// Удаляет урок из редактора расписания.
        /// </summary>
        public async Task<ScheduleEditorLessonDeleteResultDto> DeleteEditorLessonAsync(int lessonId)
        {
            var lesson = await _db.Lessons.FindAsync(lessonId);
            if (lesson == null)
                throw new KeyNotFoundException("Урок не найден");

            var oldValues = BuildLessonAuditDto(lesson);
            _db.Lessons.Remove(lesson);
            await _db.SaveChangesAsync();

            return new ScheduleEditorLessonDeleteResultDto
            {
                LessonId = lessonId,
                OldValues = oldValues
            };
        }

        /// <summary>
        /// Создает новый слот расписания.
        /// </summary>
        public async Task<Schedule> CreateScheduleSlotAsync(int dayOfWeek, int lessonNumber, TimeSpan startTime, TimeSpan endTime)
        {
            if (dayOfWeek < 0 || dayOfWeek > 4)
                throw new ArgumentException("День недели должен быть от 0 (Пн) до 4 (Пт)");
            if (lessonNumber < 1 || lessonNumber > 10)
                throw new ArgumentException("Номер урока должен быть от 1 до 10");

            var existing = await _db.Schedules
                .FirstOrDefaultAsync(s => s.DayOfWeek == dayOfWeek && s.LessonNumber == lessonNumber);
            if (existing != null)
                throw new InvalidOperationException($"Слот расписания для {dayOfWeek} дня, урока {lessonNumber} уже существует");

            var schedule = new Schedule
            {
                DayOfWeek = dayOfWeek,
                LessonNumber = lessonNumber,
                StartTime = startTime,
                EndTime = endTime,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Schedules.Add(schedule);
            await _db.SaveChangesAsync();

            return schedule;
        }

        /// <summary>
        /// Обновляет слот расписания.
        /// </summary>
        public async Task<Schedule> UpdateScheduleSlotAsync(int scheduleId, TimeSpan startTime, TimeSpan endTime)
        {
            var schedule = await _db.Schedules.FindAsync(scheduleId);
            if (schedule == null)
                throw new KeyNotFoundException($"Слот расписания с ID {scheduleId} не найден");

            schedule.StartTime = startTime;
            schedule.EndTime = endTime;
            schedule.UpdatedAt = DateTime.UtcNow;

            _db.Schedules.Update(schedule);
            await _db.SaveChangesAsync();

            return schedule;
        }

        /// <summary>
        /// Удаляет слот расписания.
        /// </summary>
        public async Task DeleteScheduleSlotAsync(int scheduleId)
        {
            var schedule = await _db.Schedules.FindAsync(scheduleId);
            if (schedule == null)
                throw new KeyNotFoundException($"Слот расписания с ID {scheduleId} не найден");

            _db.Schedules.Remove(schedule);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Получает слот расписания по идентификатору.
        /// </summary>
        public async Task<Schedule?> GetScheduleSlotAsync(int scheduleId)
        {
            return await _db.Schedules.FindAsync(scheduleId);
        }

        private async Task ValidateEditorLessonRequestAsync(ScheduleEditorLessonRequest request)
        {
            if (request.ClassId <= 0)
                throw new ArgumentException("Класс обязателен");
            if (request.SubjectId <= 0)
                throw new ArgumentException("Предмет обязателен");
            if (request.ScheduleId <= 0)
                throw new ArgumentException("Слот расписания обязателен");
            if (request.TeacherId <= 0)
                throw new ArgumentException("Преподаватель обязателен");

            if (!await _db.Classes.AnyAsync(c => c.ClassId == request.ClassId))
                throw new InvalidOperationException("Класс не найден");

            if (!await _db.Schedules.AnyAsync(s => s.ScheduleId == request.ScheduleId))
                throw new InvalidOperationException("Слот расписания не найден");

            if (!await _db.Subjects.AnyAsync(s => s.SubjectId == request.SubjectId))
                throw new InvalidOperationException("Предмет не найден");

            if (!await _db.Users.AnyAsync(u => u.Id == request.TeacherId && u.RoleId == SystemRoleIds.Teacher))
                throw new InvalidOperationException("Преподаватель не найден");
        }

        private async Task EnsureDefaultScheduleSlotsAsync()
        {
            var hasSlots = await _db.Schedules.AnyAsync();
            if (hasSlots)
                return;

            var defaultTimes = new (string Start, string End)[]
            {
                ("09:15", "09:55"),
                ("10:10", "10:50"),
                ("10:55", "11:35"),
                ("11:45", "12:25"),
                ("12:30", "13:10"),
                ("13:30", "14:10"),
                ("14:15", "14:55")
            };

            for (var day = 0; day < 5; day++)
            {
                for (var index = 0; index < defaultTimes.Length; index++)
                {
                    _db.Schedules.Add(new Schedule
                    {
                        DayOfWeek = day,
                        LessonNumber = index + 1,
                        StartTime = TimeSpan.Parse(defaultTimes[index].Start),
                        EndTime = TimeSpan.Parse(defaultTimes[index].End),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            await _db.SaveChangesAsync();
        }

        private async Task<ScheduleEditorLessonDto> BuildRequiredEditorLessonResponseAsync(int lessonId)
        {
            var lesson = await _db.Lessons
                .Where(l => l.LessonId == lessonId)
                .Include(l => l.Subject)
                .Include(l => l.Class)
                .Include(l => l.Teacher)
                .Include(l => l.Schedule)
                .Select(l => new ScheduleEditorLessonDto
                {
                    LessonId = l.LessonId,
                    ClassId = l.ClassId,
                    ClassName = l.Class.Name,
                    SubjectId = l.SubjectId,
                    SubjectName = l.Subject.Name,
                    TeacherId = l.TeacherId,
                    TeacherName = l.Teacher.FullName,
                    ScheduleId = l.ScheduleId,
                    DayOfWeek = l.Schedule != null ? l.Schedule.DayOfWeek : (int?)null,
                    LessonNumber = l.Schedule != null ? l.Schedule.LessonNumber : (int?)null,
                    StartTime = l.Schedule != null ? l.Schedule.StartTime.ToString(@"hh\:mm") : null,
                    EndTime = l.Schedule != null ? l.Schedule.EndTime.ToString(@"hh\:mm") : null,
                    Topic = l.Topic,
                    Homework = l.Homework,
                    Date = l.Date
                })
                .FirstOrDefaultAsync();

            if (lesson == null)
                throw new InvalidOperationException("Не удалось сформировать ответ по уроку");

            return lesson;
        }

        private static int ExtractClassNumber(string? className)
        {
            if (string.IsNullOrWhiteSpace(className))
                return int.MaxValue;

            var match = Regex.Match(className, @"\d+");
            return match.Success && int.TryParse(match.Value, out var number) ? number : int.MaxValue;
        }

        private static string ExtractClassSuffix(string? className)
        {
            if (string.IsNullOrWhiteSpace(className))
                return string.Empty;

            var match = Regex.Match(className, @"^\s*\d+\s*(.*)$");
            return match.Success ? match.Groups[1].Value.Trim() : className.Trim();
        }

        private static ScheduleEditorLessonAuditDto BuildLessonAuditDto(Lesson lesson)
        {
            return new ScheduleEditorLessonAuditDto
            {
                SubjectId = lesson.SubjectId,
                ClassId = lesson.ClassId,
                TeacherId = lesson.TeacherId,
                ScheduleId = lesson.ScheduleId ?? 0,
                Topic = lesson.Topic ?? string.Empty,
                Date = lesson.Date,
                Homework = lesson.Homework
            };
        }
    }
}
