using ClassBook.Application.DTOs;
using ClassBook.Application.Facades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/lessons")]
    public class LessonController : ApiControllerBase
    {
        private readonly LessonFacade _facade;

        public LessonController(LessonFacade facade)
        {
            _facade = facade;
        }

        /// <summary>
        /// Возвращает полный список уроков для административного режима.
        /// </summary>
        /// <returns>Список уроков системы.</returns>
        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _facade.GetAllLessonsAsync());
        }

        /// <summary>
        /// Создаёт новый урок с привязкой к классу, предмету и преподавателю.
        /// </summary>
        /// <param name="dto">Данные создаваемого урока.</param>
        /// <returns>Созданный урок с развернутыми справочными значениями.</returns>
        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> CreateLesson([FromBody] CreateLessonRequest dto)
        {
            try
            {
                var currentUserId = TryGetCurrentUserId();
                var result = await _facade.CreateLessonAsync(dto.SubjectId, dto.ClassId, dto.TeacherId, dto.Topic, dto.Date, dto.Homework, currentUserId);
                return CreatedAtAction(nameof(GetAll), new { id = result.LessonId }, result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequestError(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequestError(ex.Message);
            }
        }

        /// <summary>
        /// Обновляет существующий урок. Учитель может изменять только свои уроки.
        /// </summary>
        /// <param name="id">Идентификатор урока.</param>
        /// <param name="dto">Новые данные урока.</param>
        /// <returns>Обновлённый урок.</returns>
        [HttpPut("{id}")]
        [Authorize(Roles = "Учитель,Администратор")]
        public async Task<IActionResult> UpdateLesson(int id, [FromBody] CreateLessonRequest dto)
        {
            try
            {
                var lesson = await _facade.GetLessonByIdAsync(id);
                if (lesson == null)
                    return NotFoundError("Урок не найден");

                var currentUserId = TryGetCurrentUserId();
                if (!currentUserId.HasValue)
                    return UnauthorizedError("Не удалось определить пользователя");

                if (User.IsInRole("Учитель") && lesson.TeacherId != currentUserId.Value)
                    return ForbiddenError("Вы можете редактировать только свои уроки");

                var result = await _facade.UpdateLessonAsync(id, dto.SubjectId, dto.ClassId, dto.TeacherId, dto.Topic, dto.Date, dto.Homework, currentUserId);
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
            catch (ArgumentException ex)
            {
                return BadRequestError(ex.Message);
            }
        }

        /// <summary>
        /// Удаляет урок. Учитель может удалять только собственные уроки.
        /// </summary>
        /// <param name="lessonId">Идентификатор урока.</param>
        /// <returns>Пустой ответ при успешном удалении.</returns>
        [HttpDelete("{lessonId}")]
        [Authorize(Roles = "Учитель,Администратор")]
        public async Task<IActionResult> DeleteLesson(int lessonId)
        {
            try
            {
                var lesson = await _facade.GetLessonByIdAsync(lessonId);
                if (lesson == null)
                    return NotFoundError("Урок не найден");

                var currentUserId = TryGetCurrentUserId();
                if (!currentUserId.HasValue)
                    return UnauthorizedError("Не удалось определить пользователя");

                if (User.IsInRole("Учитель") && lesson.TeacherId != currentUserId.Value)
                    return ForbiddenError("Вы можете удалять только свои уроки");

                await _facade.DeleteLessonAsync(lessonId, currentUserId);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
        }

        private int? TryGetCurrentUserId()
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId)
                ? currentUserId
                : null;
        }
    }
}
