// Controllers/AttendanceController.cs
using ClassBook.Application.DTOs;
using ClassBook.Application.Facades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/teacher/attendance")]
    [Authorize(Roles = "Учитель,Администратор,Директор")]
    public class AttendanceController : ApiControllerBase
    {
        private readonly AttendanceFacade _facade;
        private readonly SchoolAccessFacade _accessFacade;

        public AttendanceController(AttendanceFacade facade, SchoolAccessFacade accessFacade)
        {
            _facade = facade;
            _accessFacade = accessFacade;
        }

        /// <summary>
        /// Отмечает посещаемость ученика.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> MarkAttendance([FromBody] MarkAttendanceRequest dto)
        {
            try
            {
                if (User.IsInRole("Директор"))
                    return ForbiddenError("Директору доступен только просмотр журнала");

                await _accessFacade.EnsureLessonAccessAsync(GetUserId(), GetRole(), dto.LessonId, writeAccess: true);
                await _facade.MarkAttendanceAsync(dto.LessonId, dto.StudentId, dto.Status);
                return Ok("Посещаемость отмечена");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequestError(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequestError(ex.Message);
            }
            catch (UnauthorizedAccessException ex) { return ForbiddenError(ex.Message); }
        }

        /// <summary>
        /// Получает посещаемость за урок.
        /// </summary>
        [HttpGet("{lessonId}")]
        public async Task<IActionResult> GetAttendanceForLesson(int lessonId)
        {
            try
            {
                await _accessFacade.EnsureLessonAccessAsync(GetUserId(), GetRole(), lessonId, writeAccess: false);
                var attendance = await _facade.GetAttendanceForLessonAsync(lessonId);
                return Ok(attendance);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
            catch (UnauthorizedAccessException ex) { return ForbiddenError(ex.Message); }
        }

        private int GetUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        private string GetRole() => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
    }

}
