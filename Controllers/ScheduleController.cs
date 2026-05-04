using ClassBook.Application.DTOs;
using ClassBook.Application.Facades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/schedule")]
    public class ScheduleController : ApiControllerBase
    {
        private readonly ScheduleFacade _scheduleFacade;
        private readonly AuditFacade _auditFacade;
        private readonly ILogger<ScheduleController> _logger;

        public ScheduleController(ScheduleFacade scheduleFacade, AuditFacade auditFacade, ILogger<ScheduleController> logger)
        {
            _scheduleFacade = scheduleFacade;
            _auditFacade = auditFacade;
            _logger = logger;
        }

        private int GetUserId()
        {
            return int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) ? userId : 0;
        }

        /// <summary>
        /// Возвращает все фиксированные слоты расписания, используемые системой.
        /// </summary>
        /// <returns>Список временных слотов расписания.</returns>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAllScheduleSlots()
        {
            var slots = await _scheduleFacade.GetAllScheduleSlotsAsync();
            return Ok(slots);
        }

        /// <summary>
        /// Возвращает расписание по выбранному дню недели.
        /// </summary>
        /// <param name="dayOfWeek">Номер дня недели.</param>
        /// <returns>Список слотов расписания для дня.</returns>
        [HttpGet("day/{dayOfWeek}")]
        [Authorize]
        public async Task<IActionResult> GetScheduleByDay(int dayOfWeek)
        {
            try
            {
                return Ok(await _scheduleFacade.GetScheduleByDayAsync(dayOfWeek));
            }
            catch (ArgumentException ex)
            {
                return BadRequestError(ex.Message);
            }
        }

        /// <summary>
        /// Возвращает полное недельное расписание по всем дням.
        /// </summary>
        /// <returns>Неделя расписания в агрегированном виде.</returns>
        [HttpGet("week")]
        [Authorize]
        public async Task<IActionResult> GetFullWeekSchedule()
        {
            return Ok(await _scheduleFacade.GetFullWeekScheduleAsync());
        }

        /// <summary>
        /// Возвращает расписание выбранного класса на указанную дату или ближайший период.
        /// </summary>
        /// <param name="classId">Идентификатор класса.</param>
        /// <param name="date">Необязательная дата фильтрации.</param>
        /// <returns>Список уроков класса.</returns>
        [HttpGet("class/{classId}")]
        [Authorize]
        public async Task<IActionResult> GetScheduleByClass(int classId, [FromQuery] string? date = null)
        {
            try
            {
                DateTime? parsedDate = null;
                if (!string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var parsed))
                {
                    parsedDate = parsed;
                }

                return Ok(await _scheduleFacade.GetScheduleByClassAsync(classId, parsedDate));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке расписания класса {ClassId}", classId);
                return InternalServerError("Не удалось загрузить расписание класса");
            }
        }

        /// <summary>
        /// Возвращает справочные данные для редактора расписания: классы, предметы, учителей и слоты.
        /// </summary>
        /// <returns>Полный набор метаданных редактора расписания.</returns>
        [HttpGet("editor/metadata")]
        [Authorize(Policy = "ScheduleManagerOnly")]
        public async Task<IActionResult> GetEditorMetadata()
        {
            return Ok(await _scheduleFacade.GetEditorMetadataAsync());
        }

        /// <summary>
        /// Создаёт новый класс прямо из редактора расписания.
        /// </summary>
        /// <param name="request">Название создаваемого класса.</param>
        /// <returns>Созданный класс.</returns>
        [HttpPost("editor/class")]
        [Authorize(Policy = "ScheduleManagerOnly")]
        public async Task<IActionResult> CreateEditorClass([FromBody] ScheduleEditorClassRequest request)
        {
            try
            {
                var createdClass = await _scheduleFacade.CreateEditorClassAsync(request.Name);

                var userId = GetUserId();
                if (userId > 0)
                {
                    await _auditFacade.LogActionAsync(userId, "Class", createdClass.ClassId, "Create", null, createdClass);
                }

                return Ok(createdClass);
            }
            catch (ArgumentException ex)
            {
                return BadRequestError(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequestError(ex.Message);
            }
        }

        /// <summary>
        /// Возвращает уроки выбранной недели для редактора расписания.
        /// </summary>
        /// <param name="weekStart">Дата начала недели.</param>
        /// <returns>Неделя расписания с уроками и справочными полями.</returns>
        [HttpGet("editor/week")]
        [Authorize(Policy = "ScheduleManagerOnly")]
        public async Task<IActionResult> GetEditorWeek([FromQuery] string weekStart)
        {
            if (!DateTime.TryParse(weekStart, out var parsedWeekStart))
                return BadRequestError("Некорректная дата начала недели");

            return Ok(await _scheduleFacade.GetEditorWeekAsync(parsedWeekStart.Date));
        }

        /// <summary>
        /// Создаёт урок в конкретной ячейке редактора расписания.
        /// </summary>
        /// <param name="request">Данные класса, предмета, преподавателя, слота и даты.</param>
        /// <returns>Созданный урок редактора.</returns>
        [HttpPost("editor/lesson")]
        [Authorize(Policy = "ScheduleManagerOnly")]
        public async Task<IActionResult> CreateEditorLesson([FromBody] ScheduleEditorLessonRequest request)
        {
            try
            {
                var result = await _scheduleFacade.CreateEditorLessonAsync(request);

                var userId = GetUserId();
                if (userId > 0)
                {
                    await _auditFacade.LogActionAsync(userId, "Lesson", result.Lesson.LessonId, "Create", null, result.NewValues);
                }

                return Ok(result.Lesson);
            }
            catch (ArgumentException ex)
            {
                return BadRequestError(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequestError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании урока в расписании");
                return InternalServerError("Не удалось создать урок в расписании");
            }
        }

        /// <summary>
        /// Обновляет урок в редакторе расписания.
        /// </summary>
        /// <param name="lessonId">Идентификатор урока.</param>
        /// <param name="request">Новые данные урока.</param>
        /// <returns>Обновлённый урок редактора.</returns>
        [HttpPut("editor/lesson/{lessonId}")]
        [Authorize(Policy = "ScheduleManagerOnly")]
        public async Task<IActionResult> UpdateEditorLesson(int lessonId, [FromBody] ScheduleEditorLessonRequest request)
        {
            try
            {
                var result = await _scheduleFacade.UpdateEditorLessonAsync(lessonId, request);

                var userId = GetUserId();
                if (userId > 0)
                {
                    await _auditFacade.LogActionAsync(userId, "Lesson", lessonId, "Update", result.OldValues, result.NewValues);
                }

                return Ok(result.Lesson);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequestError(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequestError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении урока в расписании {LessonId}", lessonId);
                return InternalServerError("Не удалось обновить урок в расписании");
            }
        }

        /// <summary>
        /// Удаляет урок из редактора расписания.
        /// </summary>
        /// <param name="lessonId">Идентификатор урока.</param>
        /// <returns>Пустой ответ при успешном удалении.</returns>
        [HttpDelete("editor/lesson/{lessonId}")]
        [Authorize(Policy = "ScheduleManagerOnly")]
        public async Task<IActionResult> DeleteEditorLesson(int lessonId)
        {
            try
            {
                var result = await _scheduleFacade.DeleteEditorLessonAsync(lessonId);

                var userId = GetUserId();
                if (userId > 0)
                {
                    await _auditFacade.LogActionAsync(userId, "Lesson", lessonId, "Delete", result.OldValues, null);
                }

                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
        }

        /// <summary>
        /// Создаёт новый слот расписания.
        /// </summary>
        /// <param name="request">Номер урока, день недели и время слота.</param>
        /// <returns>Созданный слот расписания.</returns>
        [HttpPost]
        [Authorize(Policy = "ScheduleManagerOnly")]
        public async Task<IActionResult> CreateScheduleSlot([FromBody] CreateScheduleRequest request)
        {
            try
            {
                if (!TimeSpan.TryParse(request.StartTime, out var startTime) ||
                    !TimeSpan.TryParse(request.EndTime, out var endTime))
                {
                    return BadRequestError("Некорректный формат времени");
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
                        null, new ScheduleSlotAuditDto
                        {
                            ScheduleId = schedule.ScheduleId,
                            DayOfWeek = schedule.DayOfWeek,
                            LessonNumber = schedule.LessonNumber,
                            StartTime = schedule.StartTime,
                            EndTime = schedule.EndTime
                        });
                }

                return CreatedAtAction(nameof(GetAllScheduleSlots), new { id = schedule.ScheduleId }, schedule);
            }
            catch (ArgumentException ex)
            {
                return BadRequestError(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequestError(ex.Message);
            }
        }

        /// <summary>
        /// Обновляет существующий слот расписания.
        /// </summary>
        /// <param name="id">Идентификатор слота.</param>
        /// <param name="request">Новые параметры слота.</param>
        /// <returns>Обновлённый слот расписания.</returns>
        [HttpPut("{id}")]
        [Authorize(Policy = "ScheduleManagerOnly")]
        public async Task<IActionResult> UpdateScheduleSlot(int id, [FromBody] UpdateScheduleRequest request)
        {
            try
            {
                if (!TimeSpan.TryParse(request.StartTime, out var startTime) ||
                    !TimeSpan.TryParse(request.EndTime, out var endTime))
                {
                    return BadRequestError("Некорректный формат времени");
                }

                var existing = await _scheduleFacade.GetScheduleSlotAsync(id);
                if (existing == null)
                    return NotFoundError($"Слот расписания с ID {id} не найден");

                var oldValues = new ScheduleSlotAuditDto
                {
                    ScheduleId = existing.ScheduleId,
                    DayOfWeek = existing.DayOfWeek,
                    LessonNumber = existing.LessonNumber,
                    StartTime = existing.StartTime,
                    EndTime = existing.EndTime
                };
                var schedule = await _scheduleFacade.UpdateScheduleSlotAsync(id, startTime, endTime);

                var userId = GetUserId();
                if (userId > 0)
                {
                    await _auditFacade.LogActionAsync(userId, "Schedule", id, "Update", oldValues, new ScheduleSlotAuditDto
                    {
                        ScheduleId = schedule.ScheduleId,
                        DayOfWeek = schedule.DayOfWeek,
                        LessonNumber = schedule.LessonNumber,
                        StartTime = schedule.StartTime,
                        EndTime = schedule.EndTime
                    });
                }

                return Ok(schedule);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении слота расписания");
                return InternalServerError("Не удалось обновить слот расписания");
            }
        }

        /// <summary>
        /// Удаляет слот расписания, если к нему не привязаны уроки.
        /// </summary>
        /// <param name="id">Идентификатор слота.</param>
        /// <returns>Пустой ответ при успешном удалении.</returns>
        [HttpDelete("{id}")]
        [Authorize(Policy = "ScheduleManagerOnly")]
        public async Task<IActionResult> DeleteScheduleSlot(int id)
        {
            try
            {
                var schedule = await _scheduleFacade.GetScheduleSlotAsync(id);
                if (schedule == null)
                    return NotFoundError($"Слот расписания с ID {id} не найден");

                var oldValues = new ScheduleSlotAuditDto
                {
                    ScheduleId = schedule.ScheduleId,
                    DayOfWeek = schedule.DayOfWeek,
                    LessonNumber = schedule.LessonNumber,
                    StartTime = schedule.StartTime,
                    EndTime = schedule.EndTime
                };
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
                return NotFoundError(ex.Message);
            }
        }
    }
}
