// Controllers/AttendanceController.cs
using ClassBook.Application.DTOs;
using ClassBook.Application.Facades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/teacher/attendance")]
    [Authorize(Roles = "Учитель,Администратор")]
    public class AttendanceController : ApiControllerBase
    {
        private readonly AttendanceFacade _facade;

        public AttendanceController(AttendanceFacade facade)
        {
            _facade = facade;
        }

        /// <summary>
        /// Отмечает посещаемость ученика.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> MarkAttendance([FromBody] MarkAttendanceRequest dto)
        {
            try
            {
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
        }

        /// <summary>
        /// Получает посещаемость за урок.
        /// </summary>
        [HttpGet("{lessonId}")]
        public async Task<IActionResult> GetAttendanceForLesson(int lessonId)
        {
            try
            {
                var attendance = await _facade.GetAttendanceForLessonAsync(lessonId);
                return Ok(attendance);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
        }
    }

}
