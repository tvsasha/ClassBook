using ClassBook.Application.Facades;
using ClassBook.Domain.Entities;
using ClassBook.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/schedule")]
    public class ScheduleController : ControllerBase
    {
        private readonly ScheduleFacade _scheduleFacade;
        private readonly AuditFacade _auditFacade;
        private readonly AppDbContext _db;

        public ScheduleController(ScheduleFacade scheduleFacade, AuditFacade auditFacade, AppDbContext db)
        {
            _scheduleFacade = scheduleFacade;
            _auditFacade = auditFacade;
            _db = db;
        }

        private int GetUserId()
        {
            return int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) ? userId : 0;
        }

        /// <summary>
        /// Получить все фиксированные слоты расписания
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAllScheduleSlots()
        {
            var slots = await _scheduleFacade.GetAllScheduleSlotsAsync();
            return Ok(slots);
        }

        /// <summary>
        /// Получить расписание на конкретный день недели
        /// </summary>
        [HttpGet("day/{dayOfWeek}")]
        [Authorize]
        public async Task<IActionResult> GetScheduleByDay(int dayOfWeek)
        {
            try
            {
                var schedule = await _scheduleFacade.GetScheduleByDayAsync(dayOfWeek);
                return Ok(schedule);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Получить полное расписание на неделю
        /// </summary>
        [HttpGet("week")]
        [Authorize]
        public async Task<IActionResult> GetFullWeekSchedule()
        {
            var schedule = await _scheduleFacade.GetFullWeekScheduleAsync();
            return Ok(schedule);
        }

        /// <summary>
        /// Получить расписание класса
        /// </summary>
        [HttpGet("class/{classId}")]
        [Authorize]
        public async Task<IActionResult> GetScheduleByClass(int classId, [FromQuery] string? date = null)
        {
            try
            {
                System.DateTime? parsedDate = null;
                if (!string.IsNullOrEmpty(date) && System.DateTime.TryParse(date, out var d))
                {
                    parsedDate = d;
                }

                var schedule = await _scheduleFacade.GetScheduleByClassAsync(classId, parsedDate);
                return Ok(schedule);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Метаданные редактора расписания для менеджера.
        /// </summary>
        [HttpGet("editor/metadata")]
        [Authorize(Policy = "ScheduleManagerOnly")]
        public async Task<IActionResult> GetEditorMetadata()
        {
            await EnsureDefaultScheduleSlotsAsync();

            var classes = await _db.Classes
                .Select(c => new
                {
                    c.ClassId,
                    c.Name
                })
                .ToListAsync();

            var sortedClasses = classes
                .OrderBy(c => ExtractClassNumber(c.Name))
                .ThenBy(c => ExtractClassSuffix(c.Name))
                .ThenBy(c => c.Name)
                .ToList();

            var subjects = await _db.Subjects
                .Include(s => s.Teacher)
                .OrderBy(s => s.Name)
                .Select(s => new
                {
                    s.SubjectId,
                    s.Name,
                    s.TeacherId,
                    TeacherName = s.Teacher != null ? s.Teacher.FullName : "Не назначен"
                })
                .ToListAsync();

            var teachers = await _db.Users
                .Where(u => u.RoleId == 2)
                .OrderBy(u => u.FullName)
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.Login
                })
                .ToListAsync();

            var slots = await _db.Schedules
                .OrderBy(s => s.DayOfWeek)
                .ThenBy(s => s.LessonNumber)
                .Select(s => new
                {
                    s.ScheduleId,
                    s.DayOfWeek,
                    s.LessonNumber,
                    StartTime = s.StartTime.ToString(@"hh\:mm"),
                    EndTime = s.EndTime.ToString(@"hh\:mm")
                })
                .ToListAsync();

            return Ok(new { classes = sortedClasses, subjects, teachers, slots });
        }

        /// <summary>
        /// Создать класс из редактора расписания.
        /// </summary>
        [HttpPost("editor/class")]
        [Authorize(Policy = "ScheduleManagerOnly")]
        public async Task<IActionResult> CreateEditorClass([FromBody] ScheduleEditorClassRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Название класса обязательно");

            var normalizedName = request.Name.Trim();

            var exists = await _db.Classes.AnyAsync(c => c.Name == normalizedName);
            if (exists)
                return BadRequest("Класс с таким названием уже существует");

            var classEntity = new Class
            {
                Name = normalizedName
            };

            _db.Classes.Add(classEntity);
            await _db.SaveChangesAsync();

            var userId = GetUserId();
            if (userId > 0)
            {
                await _auditFacade.LogActionAsync(userId, "Class", classEntity.ClassId, "Create", null, new
                {
                    classEntity.ClassId,
                    classEntity.Name
                });
            }

            return Ok(new
            {
                classEntity.ClassId,
                classEntity.Name
            });
        }

        /// <summary>
        /// Уроки по неделе для редактора расписания.
        /// </summary>
        [HttpGet("editor/week")]
        [Authorize(Policy = "ScheduleManagerOnly")]
        public async Task<IActionResult> GetEditorWeek([FromQuery] string weekStart)
        {
            if (!DateTime.TryParse(weekStart, out var parsedWeekStart))
                return BadRequest("Некорректная дата начала недели");

            var weekStartDate = parsedWeekStart.Date;
            var weekEndDate = weekStartDate.AddDays(7);

            var lessons = await _db.Lessons
                .Where(l => l.Date >= weekStartDate && l.Date < weekEndDate)
                .Include(l => l.Subject)
                .Include(l => l.Class)
                .Include(l => l.Teacher)
                .Include(l => l.Schedule)
                .OrderBy(l => l.Date)
                .ThenBy(l => l.Schedule != null ? l.Schedule.LessonNumber : int.MaxValue)
                .Select(l => new
                {
                    l.LessonId,
                    l.ClassId,
                    ClassName = l.Class.Name,
                    l.SubjectId,
                    SubjectName = l.Subject.Name,
                    l.TeacherId,
                    TeacherName = l.Teacher.FullName,
                    l.ScheduleId,
                    DayOfWeek = l.Schedule != null ? l.Schedule.DayOfWeek : (int?)null,
                    LessonNumber = l.Schedule != null ? l.Schedule.LessonNumber : (int?)null,
                    StartTime = l.Schedule != null ? l.Schedule.StartTime.ToString(@"hh\:mm") : null,
                    EndTime = l.Schedule != null ? l.Schedule.EndTime.ToString(@"hh\:mm") : null,
                    l.Topic,
                    l.Homework,
                    l.Date
                })
                .ToListAsync();

            return Ok(new { weekStart = weekStartDate, lessons });
        }

        /// <summary>
        /// Создать урок в конкретном слоте расписания.
        /// </summary>
        [HttpPost("editor/lesson")]
        [Authorize(Policy = "ScheduleManagerOnly")]
        public async Task<IActionResult> CreateEditorLesson([FromBody] ScheduleEditorLessonRequest request)
        {
            try
            {
                var validationError = await ValidateEditorLessonRequestAsync(request);
                if (validationError != null)
                    return BadRequest(validationError);

                var lessonDate = request.Date.Date;

                var existingLesson = await _db.Lessons
                    .FirstOrDefaultAsync(l =>
                        l.ClassId == request.ClassId &&
                        l.ScheduleId == request.ScheduleId &&
                        l.Date.Date == lessonDate);

                if (existingLesson != null)
                    return BadRequest("В этом слоте уже есть урок. Используйте редактирование.");

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

                var userId = GetUserId();
                if (userId > 0)
                {
                    await _auditFacade.LogActionAsync(userId, "Lesson", lesson.LessonId, "Create", null, new
                    {
                        lesson.SubjectId,
                        lesson.ClassId,
                        lesson.TeacherId,
                        lesson.ScheduleId,
                        lesson.Topic,
                        lesson.Date,
                        lesson.Homework
                    });
                }

                return Ok(await BuildEditorLessonResponseAsync(lesson.LessonId));
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Обновить урок в конкретном слоте расписания.
        /// </summary>
        [HttpPut("editor/lesson/{lessonId}")]
        [Authorize(Policy = "ScheduleManagerOnly")]
        public async Task<IActionResult> UpdateEditorLesson(int lessonId, [FromBody] ScheduleEditorLessonRequest request)
        {
            try
            {
                var validationError = await ValidateEditorLessonRequestAsync(request);
                if (validationError != null)
                    return BadRequest(validationError);

                var lesson = await _db.Lessons.FindAsync(lessonId);
                if (lesson == null)
                    return NotFound("Урок не найден");

                var lessonDate = request.Date.Date;
                var duplicate = await _db.Lessons
                    .AnyAsync(l =>
                        l.LessonId != lessonId &&
                        l.ClassId == request.ClassId &&
                        l.ScheduleId == request.ScheduleId &&
                        l.Date.Date == lessonDate);

                if (duplicate)
                    return BadRequest("В этом слоте уже есть другой урок");

                var oldValues = new
                {
                    lesson.SubjectId,
                    lesson.ClassId,
                    lesson.TeacherId,
                    lesson.ScheduleId,
                    lesson.Topic,
                    lesson.Date,
                    lesson.Homework
                };

                lesson.SubjectId = request.SubjectId;
                lesson.ClassId = request.ClassId;
                lesson.TeacherId = request.TeacherId;
                lesson.ScheduleId = request.ScheduleId;
                if (string.IsNullOrWhiteSpace(lesson.Topic))
                {
                    lesson.Topic = "Тема будет указана преподавателем";
                }
                lesson.Date = lessonDate;
                lesson.Homework = string.IsNullOrWhiteSpace(request.Homework) ? null : request.Homework.Trim();

                await _db.SaveChangesAsync();

                var userId = GetUserId();
                if (userId > 0)
                {
                    await _auditFacade.LogActionAsync(userId, "Lesson", lesson.LessonId, "Update", oldValues, new
                    {
                        lesson.SubjectId,
                        lesson.ClassId,
                        lesson.TeacherId,
                        lesson.ScheduleId,
                        lesson.Topic,
                        lesson.Date,
                        lesson.Homework
                    });
                }

                return Ok(await BuildEditorLessonResponseAsync(lessonId));
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Удалить урок из сетки расписания.
        /// </summary>
        [HttpDelete("editor/lesson/{lessonId}")]
        [Authorize(Policy = "ScheduleManagerOnly")]
        public async Task<IActionResult> DeleteEditorLesson(int lessonId)
        {
            var lesson = await _db.Lessons.FindAsync(lessonId);
            if (lesson == null)
                return NotFound("Урок не найден");

            var oldValues = new
            {
                lesson.LessonId,
                lesson.SubjectId,
                lesson.ClassId,
                lesson.TeacherId,
                lesson.ScheduleId,
                lesson.Topic,
                lesson.Date,
                lesson.Homework
            };

            _db.Lessons.Remove(lesson);
            await _db.SaveChangesAsync();

            var userId = GetUserId();
            if (userId > 0)
            {
                await _auditFacade.LogActionAsync(userId, "Lesson", lessonId, "Delete", oldValues, null);
            }

            return NoContent();
        }

        /// <summary>
        /// Создать новый слот расписания (только для менеджера расписания)
        /// </summary>
        [HttpPost]
        [Authorize(Policy = "ScheduleManagerOnly")]
        public async Task<IActionResult> CreateScheduleSlot([FromBody] CreateScheduleRequest request)
        {
            try
            {
                if (!System.TimeSpan.TryParse(request.StartTime, out var startTime) ||
                    !System.TimeSpan.TryParse(request.EndTime, out var endTime))
                {
                    return BadRequest("Некорректный формат времени");
                }

                var schedule = await _scheduleFacade.CreateScheduleSlotAsync(
                    request.DayOfWeek,
                    request.LessonNumber,
                    startTime,
                    endTime
                );

                var userId = GetUserId();
                if (userId > 0)
                {
                    await _auditFacade.LogActionAsync(userId, "Schedule", schedule.ScheduleId, "Create",
                        null, new { schedule.DayOfWeek, schedule.LessonNumber, schedule.StartTime, schedule.EndTime });
                }

                return CreatedAtAction(nameof(GetAllScheduleSlots), new { id = schedule.ScheduleId }, schedule);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Обновить слот расписания (только для менеджера расписания)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Policy = "ScheduleManagerOnly")]
        public async Task<IActionResult> UpdateScheduleSlot(int id, [FromBody] UpdateScheduleRequest request)
        {
            try
            {
                if (!System.TimeSpan.TryParse(request.StartTime, out var startTime) ||
                    !System.TimeSpan.TryParse(request.EndTime, out var endTime))
                {
                    return BadRequest("Некорректный формат времени");
                }

                var existing = await _scheduleFacade.GetScheduleSlotAsync(id);
                if (existing == null)
                    return NotFound($"Слот расписания с ID {id} не найден");

                var oldValues = new { existing.StartTime, existing.EndTime };

                var schedule = await _scheduleFacade.UpdateScheduleSlotAsync(id, startTime, endTime);

                var userId = GetUserId();
                if (userId > 0)
                {
                    await _auditFacade.LogActionAsync(userId, "Schedule", id, "Update",
                        oldValues, new { schedule.StartTime, schedule.EndTime });
                }

                return Ok(schedule);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Удалить слот расписания (только для менеджера расписания)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Policy = "ScheduleManagerOnly")]
        public async Task<IActionResult> DeleteScheduleSlot(int id)
        {
            try
            {
                var schedule = await _scheduleFacade.GetScheduleSlotAsync(id);
                if (schedule == null)
                    return NotFound($"Слот расписания с ID {id} не найден");

                var oldValues = new { schedule.ScheduleId, schedule.DayOfWeek, schedule.LessonNumber, schedule.StartTime, schedule.EndTime };

                await _scheduleFacade.DeleteScheduleSlotAsync(id);

                var userId = GetUserId();
                if (userId > 0)
                {
                    await _auditFacade.LogActionAsync(userId, "Schedule", id, "Delete", oldValues, null);
                }

                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        private async Task<string?> ValidateEditorLessonRequestAsync(ScheduleEditorLessonRequest request)
        {
            if (request.ClassId <= 0)
                return "Класс обязателен";

            if (request.SubjectId <= 0)
                return "Предмет обязателен";

            if (request.ScheduleId <= 0)
                return "Слот расписания обязателен";

            if (request.TeacherId <= 0)
                return "Преподаватель обязателен";

            var classExists = await _db.Classes.AnyAsync(c => c.ClassId == request.ClassId);
            if (!classExists)
                return "Класс не найден";

            var scheduleExists = await _db.Schedules.AnyAsync(s => s.ScheduleId == request.ScheduleId);
            if (!scheduleExists)
                return "Слот расписания не найден";

            var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.SubjectId == request.SubjectId);
            if (subject == null)
                return "Предмет не найден";

            var teacherExists = await _db.Users.AnyAsync(u => u.Id == request.TeacherId && u.RoleId == 2);
            if (!teacherExists)
                return "Преподаватель не найден";

            return null;
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

        private async Task<object?> BuildEditorLessonResponseAsync(int lessonId)
        {
            return await _db.Lessons
                .Where(l => l.LessonId == lessonId)
                .Include(l => l.Subject)
                .Include(l => l.Class)
                .Include(l => l.Teacher)
                .Include(l => l.Schedule)
                .Select(l => new
                {
                    l.LessonId,
                    l.ClassId,
                    ClassName = l.Class.Name,
                    l.SubjectId,
                    SubjectName = l.Subject.Name,
                    l.TeacherId,
                    TeacherName = l.Teacher.FullName,
                    l.ScheduleId,
                    DayOfWeek = l.Schedule != null ? l.Schedule.DayOfWeek : (int?)null,
                    LessonNumber = l.Schedule != null ? l.Schedule.LessonNumber : (int?)null,
                    StartTime = l.Schedule != null ? l.Schedule.StartTime.ToString(@"hh\:mm") : null,
                    EndTime = l.Schedule != null ? l.Schedule.EndTime.ToString(@"hh\:mm") : null,
                    l.Topic,
                    l.Homework,
                    l.Date
                })
                .FirstOrDefaultAsync();
        }
    }

    public class CreateScheduleRequest
    {
        public int DayOfWeek { get; set; }
        public int LessonNumber { get; set; }
        public string StartTime { get; set; } = null!;
        public string EndTime { get; set; } = null!;
    }

    public class UpdateScheduleRequest
    {
        public string StartTime { get; set; } = null!;
        public string EndTime { get; set; } = null!;
    }

    public class ScheduleEditorLessonRequest
    {
        public int ClassId { get; set; }
        public int SubjectId { get; set; }
        public int TeacherId { get; set; }
        public int ScheduleId { get; set; }
        public DateTime Date { get; set; }
        public string? Homework { get; set; }
    }

    public class ScheduleEditorClassRequest
    {
        public string Name { get; set; } = null!;
    }
}
