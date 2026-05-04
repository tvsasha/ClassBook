using ClassBook.Application.Common;
using ClassBook.Application.Facades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public DirectorController(AnalyticsFacade analyticsFacade, AuditFacade auditFacade)
        {
            _analyticsFacade = analyticsFacade;
            _auditFacade = auditFacade;
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
                Console.WriteLine($"[DirectorController.GetDailyReport] Exception: {ex.Message}");
                Console.WriteLine($"[DirectorController.GetDailyReport] StackTrace: {ex.StackTrace}");
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
                Console.WriteLine($"[DirectorController.GetAttendanceStatistics] Exception: {ex.Message}");
                Console.WriteLine($"[DirectorController.GetAttendanceStatistics] StackTrace: {ex.StackTrace}");
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
                Console.WriteLine($"[DirectorController.GetProblematicStudents] Exception: {ex.Message}");
                Console.WriteLine($"[DirectorController.GetProblematicStudents] StackTrace: {ex.StackTrace}");
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
                Console.WriteLine($"[DirectorController.GetTeacherProgress] Exception: {ex.Message}");
                Console.WriteLine($"[DirectorController.GetTeacherProgress] StackTrace: {ex.StackTrace}");
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
                Console.WriteLine($"[DirectorController.GetClassSummary] Exception: {ex.Message}");
                Console.WriteLine($"[DirectorController.GetClassSummary] StackTrace: {ex.StackTrace}");
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
                Console.WriteLine($"[DirectorController.GetAuditLog] Exception: {ex.Message}");
                Console.WriteLine($"[DirectorController.GetAuditLog] StackTrace: {ex.StackTrace}");
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
                Console.WriteLine($"[DirectorController.GetUserAuditLog] Exception: {ex.Message}");
                Console.WriteLine($"[DirectorController.GetUserAuditLog] StackTrace: {ex.StackTrace}");
                return InternalServerError("Не удалось загрузить действия пользователя");
            }
        }
    }
}
