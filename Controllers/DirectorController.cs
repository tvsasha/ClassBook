using ClassBook.Application.Common;
using ClassBook.Application.Facades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/director")]
    [Authorize(Policy = "DirectorOnly")]
    public class DirectorController : ApiControllerBase
    {
        private readonly AnalyticsFacade _analyticsFacade;
        private readonly AuditFacade _auditFacade;
        private readonly ILogger<DirectorController> _logger;

        public DirectorController(AnalyticsFacade analyticsFacade, AuditFacade auditFacade, ILogger<DirectorController> logger)
        {
            _analyticsFacade = analyticsFacade;
            _auditFacade = auditFacade;
            _logger = logger;
        }

        /// <summary>
        /// Ежедневный отчет: кто заполнил оценки и посещаемость
        /// </summary>
        [HttpGet("report/daily")]
        public async Task<IActionResult> GetDailyReport([FromQuery] string? date = null)
        {
            try
            {
                var reportDate = QueryDateParser.ParseDateOrDefault(date, () => DateTime.Now);
                var report = await _analyticsFacade.GetDailyCompletionReportAsync(reportDate);
                return Ok(report);
            }
            catch (ArgumentException ex)
            {
                return BadRequestError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при формировании ежедневного отчета");
                return InternalServerError("Не удалось сформировать ежедневный отчет");
            }
        }

        /// <summary>
        /// Статистика посещаемости по классам за период
        /// </summary>
        [HttpGet("report/attendance")]
        public async Task<IActionResult> GetAttendanceStatistics([FromQuery] string? startDate = null, [FromQuery] string? endDate = null)
        {
            try
            {
                var (start, end) = QueryDateParser.ParseRangeOrDefault(
                    startDate,
                    endDate,
                    () => DateTime.Now.AddMonths(-1),
                    () => DateTime.Now);
                var stats = await _analyticsFacade.GetAttendanceStatisticsAsync(start, end);
                return Ok(stats);
            }
            catch (ArgumentException ex)
            {
                return BadRequestError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при формировании статистики посещаемости");
                return InternalServerError("Не удалось сформировать статистику посещаемости");
            }
        }

        /// <summary>
        /// Проблемные ученики: много пропусков или низкие оценки
        /// Фильтры: классId, studentId, teacherId (опционально)
        /// </summary>
        [HttpGet("report/problematic")]
        public async Task<IActionResult> GetProblematicStudents(
            [FromQuery] string? startDate = null,
            [FromQuery] string? endDate = null,
            [FromQuery] int? classId = null,
            [FromQuery] int? studentId = null,
            [FromQuery] int? teacherId = null)
        {
            try
            {
                var (start, end) = QueryDateParser.ParseRangeOrDefault(
                    startDate,
                    endDate,
                    () => DateTime.Now.AddMonths(-1),
                    () => DateTime.Now);
                var report = await _analyticsFacade.GetProblematicStudentsAsync(start, end, classId, studentId, teacherId);
                return Ok(report);
            }
            catch (ArgumentException ex)
            {
                return BadRequestError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при формировании отчета по проблемным ученикам");
                return InternalServerError("Не удалось сформировать отчет по проблемным ученикам");
            }
        }

        /// <summary>
        /// Прогресс учителя: сколько уроков провел, оценок и посещаемости заполнил
        /// </summary>
        [HttpGet("report/teacher-progress/{teacherId}")]
        public async Task<IActionResult> GetTeacherProgress(
            int teacherId,
            [FromQuery] string? startDate = null,
            [FromQuery] string? endDate = null)
        {
            try
            {
                var (start, end) = QueryDateParser.ParseRangeOrDefault(
                    startDate,
                    endDate,
                    () => DateTime.Now.AddMonths(-1),
                    () => DateTime.Now);
                var report = await _analyticsFacade.GetTeacherProgressAsync(teacherId, start, end);
                return Ok(report);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequestError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при формировании отчета по прогрессу преподавателя {TeacherId}", teacherId);
                return InternalServerError("Не удалось сформировать отчет по прогрессу преподавателя");
            }
        }

        /// <summary>
        /// Сводка по классам: количество учеников, средние отсутствия, средняя оценка
        /// </summary>
        [HttpGet("report/class-summary")]
        public async Task<IActionResult> GetClassSummary(
            [FromQuery] string? startDate = null,
            [FromQuery] string? endDate = null)
        {
            try
            {
                var (start, end) = QueryDateParser.ParseRangeOrDefault(
                    startDate,
                    endDate,
                    () => DateTime.Now.AddMonths(-1),
                    () => DateTime.Now);
                var summary = await _analyticsFacade.GetClassSummaryAsync(start, end);
                return Ok(summary);
            }
            catch (ArgumentException ex)
            {
                return BadRequestError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при формировании сводки по классам");
                return InternalServerError("Не удалось сформировать сводку по классам");
            }
        }

        /// <summary>
        /// История изменений (аудит лог) по типу сущности за период
        /// </summary>
        [HttpGet("audit-log")]
        public async Task<IActionResult> GetAuditLog(
            [FromQuery] string? entityType = null,
            [FromQuery] string? startDate = null,
            [FromQuery] string? endDate = null)
        {
            try
            {
                var (start, end) = QueryDateParser.ParseRangeOrDefault(
                    startDate,
                    endDate,
                    () => DateTime.Now.AddMonths(-1),
                    () => DateTime.Now);

                if (string.IsNullOrEmpty(entityType))
                {
                    return BadRequestError("Параметр entityType обязателен");
                }

                var logs = await _auditFacade.GetAuditLogByTypeAsync(entityType, start, end);
                return Ok(logs);
            }
            catch (ArgumentException ex)
            {
                return BadRequestError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке журнала аудита по типу {EntityType}", entityType);
                return InternalServerError("Не удалось загрузить журнал аудита");
            }
        }

        /// <summary>
        /// Получить все действия конкретного пользователя за период
        /// </summary>
        [HttpGet("audit-log/user/{userId}")]
        public async Task<IActionResult> GetUserAuditLog(
            int userId,
            [FromQuery] string? startDate = null,
            [FromQuery] string? endDate = null)
        {
            try
            {
                var (start, end) = QueryDateParser.ParseRangeOrDefault(
                    startDate,
                    endDate,
                    () => DateTime.Now.AddMonths(-1),
                    () => DateTime.Now);
                var logs = await _auditFacade.GetUserActionsAsync(userId, start, end);
                return Ok(logs);
            }
            catch (ArgumentException ex)
            {
                return BadRequestError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке действий пользователя {UserId}", userId);
                return InternalServerError("Не удалось загрузить действия пользователя");
            }
        }
    }
}
