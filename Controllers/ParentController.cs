using ClassBook.Application.DTOs;
using ClassBook.Application.Facades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/parent")]
    public class ParentController : ApiControllerBase
    {
        private readonly ParentFacade _parentFacade;
        private readonly ILogger<ParentController> _logger;

        public ParentController(ParentFacade parentFacade, ILogger<ParentController> logger)
        {
            _parentFacade = parentFacade;
            _logger = logger;
        }

        private int GetUserId()
        {
            return int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) ? userId : 0;
        }

        /// <summary>
        /// Получить всех учеников родителя
        /// </summary>
        /// <summary>
        /// Возвращает список детей, связанных с текущей родительской учетной записью.
        /// </summary>
        /// <returns>Список доступных родителю учеников.</returns>
        [HttpGet("students")]
        [Authorize(Roles = "Родитель,Администратор")]
        public async Task<IActionResult> GetMyStudents()
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized();

            try
            {
                var students = await _parentFacade.GetStudentsForParentAsync(userId);
                _logger.LogDebug("Для родителя {UserId} найдено {Count} учеников", userId, students?.Count ?? 0);
                return Ok(students ?? new List<ClassBook.Application.DTOs.PortalStudentInfoDto>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке детей родителя {UserId}", userId);
                return InternalServerError("Не удалось загрузить список учеников");
            }
        }

        /// <summary>
        /// Получить расписание ученика
        /// </summary>
        /// <summary>
        /// Возвращает расписание выбранного ученика для родителя или администратора.
        /// </summary>
        /// <param name="studentId">Идентификатор ученика.</param>
        /// <returns>Список занятий ученика.</returns>
        [HttpGet("student/{studentId}/schedule")]
        [Authorize(Roles = "Родитель,Администратор")]
        public async Task<IActionResult> GetStudentSchedule(int studentId)
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized();

            try
            {
                // Проверяем, что родитель имеет доступ к этому ученику
                var isParent = await _parentFacade.IsParentOfStudentAsync(userId, studentId);
                if (!isParent && !User.IsInRole("Администратор"))
                {
                    return ForbiddenError("У вас нет доступа к этому ученику");
                }
                var schedule = await _parentFacade.GetStudentScheduleAsync(studentId);

                return Ok(schedule);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке расписания ученика {StudentId} для родителя {UserId}", studentId, userId);
                return InternalServerError("Не удалось загрузить расписание ученика");
            }
        }

        /// <summary>
        /// Получить оценки ученика
        /// </summary>
        /// <summary>
        /// Возвращает оценки выбранного ученика.
        /// </summary>
        /// <param name="studentId">Идентификатор ученика.</param>
        /// <returns>Список оценок ученика.</returns>
        [HttpGet("student/{studentId}/grades")]
        [Authorize(Roles = "Родитель,Администратор")]
        public async Task<IActionResult> GetStudentGrades(int studentId)
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized();

            try
            {
                // Проверяем доступ
                var isParent = await _parentFacade.IsParentOfStudentAsync(userId, studentId);
                if (!isParent && !User.IsInRole("Администратор"))
                {
                    return ForbiddenError("У вас нет доступа к этому ученику");
                }
                var grades = await _parentFacade.GetStudentGradesAsync(studentId);

                return Ok(grades);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке оценок ученика {StudentId} для родителя {UserId}", studentId, userId);
                return InternalServerError("Не удалось загрузить оценки ученика");
            }
        }

        /// <summary>
        /// Получить домашние задания ученика
        /// </summary>
        /// <summary>
        /// Возвращает домашние задания выбранного ученика.
        /// </summary>
        /// <param name="studentId">Идентификатор ученика.</param>
        /// <returns>Список домашних заданий ученика.</returns>
        [HttpGet("student/{studentId}/homework")]
        [Authorize(Roles = "Родитель,Администратор")]
        public async Task<IActionResult> GetStudentHomework(int studentId)
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized();

            try
            {
                // Проверяем доступ
                var isParent = await _parentFacade.IsParentOfStudentAsync(userId, studentId);
                if (!isParent && !User.IsInRole("Администратор"))
                {
                    return ForbiddenError("У вас нет доступа к этому ученику");
                }
                var homework = await _parentFacade.GetStudentHomeworkAsync(studentId);

                return Ok(homework);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке домашнего задания ученика {StudentId} для родителя {UserId}", studentId, userId);
                return InternalServerError("Не удалось загрузить домашние задания ученика");
            }
        }

        /// <summary>
        /// Получить посещаемость ученика (все уроки с момента добавления ученика в систему)
        /// </summary>
        /// <summary>
        /// Возвращает посещаемость выбранного ученика.
        /// </summary>
        /// <param name="studentId">Идентификатор ученика.</param>
        /// <returns>Список записей посещаемости ученика.</returns>
        [HttpGet("student/{studentId}/attendance")]
        [Authorize(Roles = "Родитель,Администратор")]
        public async Task<IActionResult> GetStudentAttendance(int studentId)
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized();

            try
            {
                // Проверяем доступ
                var isParent = await _parentFacade.IsParentOfStudentAsync(userId, studentId);
                if (!isParent && !User.IsInRole("Администратор"))
                {
                    return ForbiddenError("У вас нет доступа к этому ученику");
                }
                var result = await _parentFacade.GetStudentAttendanceAsync(studentId);

                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке посещаемости ученика {StudentId} для родителя {UserId}", studentId, userId);
                return InternalServerError("Не удалось загрузить посещаемость ученика");
            }
        }

        /// <summary>
        /// Добавить ученика к родителю (только для админа)
        /// </summary>
        /// <summary>
        /// Привязывает родителя к ученику из административного режима.
        /// </summary>
        /// <param name="studentId">Идентификатор ученика.</param>
        /// <param name="request">Идентификатор родителя для привязки.</param>
        /// <returns>Данные созданной связи ученик-родитель.</returns>
        [HttpPost("student/{studentId}")]
        [Authorize(Roles = "Администратор")]
        public async Task<IActionResult> AddParentToStudent(int studentId, [FromBody] AddParentRequest request)
        {
            try
            {
                var result = await _parentFacade.AddParentToStudentAsync(studentId, request.ParentId);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequestError(ex.Message);
            }
        }

        /// <summary>
        /// Получить родителей ученика (только для админа)
        /// </summary>
        /// <summary>
        /// Возвращает список родителей, привязанных к выбранному ученику.
        /// </summary>
        /// <param name="studentId">Идентификатор ученика.</param>
        /// <returns>Список родительских учетных записей.</returns>
        [HttpGet("student/{studentId}/parents")]
        [Authorize(Roles = "Администратор")]
        public async Task<IActionResult> GetStudentParents(int studentId)
        {
            try
            {
                var parents = await _parentFacade.GetParentsForStudentAsync(studentId);
                return Ok(parents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке родителей ученика {StudentId}", studentId);
                return InternalServerError("Не удалось загрузить список родителей ученика");
            }
        }

        /// <summary>
        /// Удалить связь ученик-родитель (только для админа)
        /// </summary>
        /// <summary>
        /// Удаляет связь между учеником и родителем.
        /// </summary>
        /// <param name="studentId">Идентификатор ученика.</param>
        /// <param name="parentId">Идентификатор родителя.</param>
        /// <returns>Пустой ответ при успешном удалении связи.</returns>
        [HttpDelete("student/{studentId}/parent/{parentId}")]
        [Authorize(Roles = "Администратор")]
        public async Task<IActionResult> RemoveParentFromStudent(int studentId, int parentId)
        {
            try
            {
                await _parentFacade.RemoveParentFromStudentAsync(studentId, parentId);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
        }
    }

}
