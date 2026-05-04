using ClassBook.Application.Facades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/student")]
    public class StudentController : ApiControllerBase
    {
        private readonly StudentFacade _studentFacade;
        private readonly ILogger<StudentController> _logger;

        public StudentController(StudentFacade studentFacade, ILogger<StudentController> logger)
        {
            _studentFacade = studentFacade;
            _logger = logger;
        }

        private int GetUserId()
        {
            return int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) ? userId : 0;
        }

        /// <summary>
        /// Возвращает расписание текущего ученика по его учетной записи.
        /// </summary>
        /// <returns>Список занятий текущего ученика.</returns>
        [HttpGet("me/schedule")]
        [Authorize(Roles = "Ученик,Администратор")]
        public async Task<IActionResult> GetMySchedule()
        {
            try
            {
                return Ok(await _studentFacade.GetScheduleForUserAsync(GetUserId()));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке расписания текущего ученика");
                return InternalServerError("Не удалось загрузить расписание ученика");
            }
        }

        /// <summary>
        /// Возвращает оценки текущего ученика.
        /// </summary>
        /// <returns>Список оценок текущего ученика.</returns>
        [HttpGet("me/grades")]
        [Authorize(Roles = "Ученик,Администратор")]
        public async Task<IActionResult> GetMyGrades()
        {
            try
            {
                return Ok(await _studentFacade.GetGradesForUserAsync(GetUserId()));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке оценок текущего ученика");
                return InternalServerError("Не удалось загрузить оценки ученика");
            }
        }

        /// <summary>
        /// Возвращает домашние задания текущего ученика.
        /// </summary>
        /// <returns>Список домашних заданий текущего ученика.</returns>
        [HttpGet("me/homework")]
        [Authorize(Roles = "Ученик,Администратор")]
        public async Task<IActionResult> GetMyHomework()
        {
            try
            {
                return Ok(await _studentFacade.GetHomeworkForUserAsync(GetUserId()));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке домашнего задания текущего ученика");
                return InternalServerError("Не удалось загрузить домашние задания ученика");
            }
        }

        /// <summary>
        /// Возвращает посещаемость текущего ученика.
        /// </summary>
        /// <returns>Список отметок посещаемости текущего ученика.</returns>
        [HttpGet("me/attendance")]
        [Authorize(Roles = "Ученик,Администратор")]
        public async Task<IActionResult> GetMyAttendance()
        {
            try
            {
                return Ok(await _studentFacade.GetAttendanceForUserAsync(GetUserId()));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке посещаемости текущего ученика");
                return InternalServerError("Не удалось загрузить посещаемость ученика");
            }
        }

        /// <summary>
        /// Возвращает сведения о классе текущего ученика.
        /// </summary>
        /// <returns>Краткая информация о классе и ученике.</returns>
        [HttpGet("me/class")]
        [Authorize(Roles = "Ученик,Администратор")]
        public async Task<IActionResult> GetMyClassInfo()
        {
            try
            {
                return Ok(await _studentFacade.GetClassInfoForUserAsync(GetUserId()));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке информации о текущем ученике");
                return InternalServerError("Не удалось загрузить информацию об ученике");
            }
        }
    }
}
