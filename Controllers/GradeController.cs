using ClassBook.Application.DTOs;
using ClassBook.Application.Facades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/teacher/grades")]
    [Authorize(Roles = "Учитель")]
    public class GradeController : ApiControllerBase
    {
        private readonly GradeFacade _facade;
        private readonly ILogger<GradeController> _logger;

        public GradeController(GradeFacade facade, ILogger<GradeController> logger)
        {
            _facade = facade;
            _logger = logger;
        }

        private int GetUserId()
        {
            return int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) ? userId : 0;
        }

        /// <summary>
        /// Добавляет оценку ученику за конкретный урок.
        /// </summary>
        /// <param name="dto">Данные урока, ученика и значение оценки.</param>
        /// <returns>Созданная оценка с основными идентификаторами.</returns>
        [HttpPost]
        public async Task<IActionResult> AddGrade([FromBody] AddGradeRequest dto)
        {
            try
            {
                var userId = GetUserId();
                var grade = await _facade.AddGradeAsync(dto.LessonId, dto.StudentId, dto.Value, userId > 0 ? userId : null);
                return CreatedAtAction(nameof(AddGrade), new { id = grade.GradeId }, grade);
            }
            catch (ArgumentException ex)
            {
                return BadRequestError(ex.Message);
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
        /// Возвращает все оценки, выставленные по выбранному уроку.
        /// </summary>
        /// <param name="lessonId">Идентификатор урока.</param>
        /// <returns>Список оценок с краткими данными учеников.</returns>
        [HttpGet("{lessonId}")]
        public async Task<IActionResult> GetGradesForLesson(int lessonId)
        {
            try
            {
                return Ok(await _facade.GetGradesForLessonAsync(lessonId));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
        }

        /// <summary>
        /// Возвращает все оценки преподавателя за доступный ему период работы.
        /// </summary>
        /// <param name="teacherId">Идентификатор преподавателя.</param>
        /// <returns>Плоский список оценок с привязкой к урокам и ученикам.</returns>
        [HttpGet("all")]
        public async Task<IActionResult> GetAllGrades(int teacherId)
        {
            try
            {
                return Ok(await _facade.GetAllGradesByTeacherAsync(teacherId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке оценок преподавателя {TeacherId}", teacherId);
                return InternalServerError("Не удалось загрузить оценки преподавателя");
            }
        }

        /// <summary>
        /// Возвращает список учеников урока вместе с уже поставленными оценками.
        /// </summary>
        /// <param name="lessonId">Идентификатор урока.</param>
        /// <returns>Список учеников урока и их оценки.</returns>
        [HttpGet("lesson/{lessonId}/students")]
        public async Task<IActionResult> GetStudentsWithGrades(int lessonId)
        {
            return Ok(await _facade.GetStudentsWithGradesAsync(lessonId));
        }

        /// <summary>
        /// Удаляет выбранную оценку из журнала.
        /// </summary>
        /// <param name="gradeId">Идентификатор оценки.</param>
        /// <returns>Пустой ответ при успешном удалении.</returns>
        [HttpDelete("{gradeId}")]
        public async Task<IActionResult> DeleteGrade(int gradeId)
        {
            try
            {
                var userId = GetUserId();
                await _facade.DeleteGradeAsync(gradeId, userId > 0 ? userId : null);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении оценки {GradeId}", gradeId);
                return InternalServerError("Не удалось удалить оценку");
            }
        }
    }
}
