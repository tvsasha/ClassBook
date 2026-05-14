using ClassBook.Application.DTOs;
using ClassBook.Application.Facades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/subjects")]
    [Authorize(Policy = "AdminOnly")]
    public class SubjectsController : ApiControllerBase
    {
        private readonly SubjectFacade _subjectFacade;

        public SubjectsController(SubjectFacade subjectFacade)
        {
            _subjectFacade = subjectFacade;
        }

        /// <summary>
        /// Получает классы, в которых преподаётся предмет.
        /// </summary>
        /// <param name="subjectId">Идентификатор предмета.</param>
        /// <returns>Список классов и привязанных преподавателей.</returns>
        [HttpGet("{subjectId}/classes")]
        public async Task<IActionResult> GetClassesForSubject(int subjectId)
        {
            try
            {
                return Ok(await _subjectFacade.GetClassesForSubjectAsync(subjectId));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
        }

        /// <summary>
        /// Получает все предметы.
        /// </summary>
        /// <returns>Список предметов с преподавателями.</returns>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _subjectFacade.GetAllSubjectsAsync());
        }

        /// <summary>
        /// Создаёт новый предмет.
        /// </summary>
        /// <param name="dto">Название предмета и преподаватель.</param>
        /// <returns>Созданный предмет.</returns>
        [HttpPost]
        public async Task<IActionResult> CreateSubject([FromBody] CreateSubjectDto dto)
        {
            try
            {
                return Ok(await _subjectFacade.CreateSubjectAsync(dto.Name, dto.TeacherId, dto.ClassId));
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
        /// Обновляет предмет.
        /// </summary>
        /// <param name="id">Идентификатор предмета.</param>
        /// <param name="dto">Новые данные предмета.</param>
        /// <returns>Обновленный предмет.</returns>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSubject(int id, [FromBody] CreateSubjectDto dto)
        {
            try
            {
                return Ok(await _subjectFacade.UpdateSubjectAsync(id, dto.Name, dto.TeacherId));
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
        }

        /// <summary>
        /// Прикрепляет учителя к предмету.
        /// </summary>
        /// <param name="subjectId">Идентификатор предмета.</param>
        /// <param name="dto">Идентификатор преподавателя.</param>
        /// <returns>Результат привязки.</returns>
        [HttpPost("{subjectId}/teachers")]
        public async Task<IActionResult> AttachTeacherToSubject(int subjectId, [FromBody] AttachTeacherDto dto)
        {
            try
            {
                return Ok(await _subjectFacade.AttachTeacherToSubjectAsync(subjectId, dto.TeacherId));
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

        [HttpPost("{subjectId}/classes")]
        public async Task<IActionResult> AssignSubjectToClass(int subjectId, [FromBody] SubjectClassAssignmentRequestDto dto)
        {
            try
            {
                return Ok(await _subjectFacade.AssignSubjectToClassAsync(subjectId, dto.ClassId, dto.TeacherId));
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

        [HttpDelete("{subjectId}/classes/{classId}/teachers/{teacherId}")]
        public async Task<IActionResult> RemoveSubjectClassAssignment(int subjectId, int classId, int teacherId)
        {
            try
            {
                await _subjectFacade.RemoveSubjectClassAssignmentAsync(subjectId, classId, teacherId);
                return NoContent();
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
        /// Удаляет предмет.
        /// </summary>
        /// <param name="id">Идентификатор предмета.</param>
        /// <returns>Пустой ответ при успешном удалении.</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSubject(int id)
        {
            try
            {
                await _subjectFacade.DeleteSubjectAsync(id);
                return NoContent();
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
    }
}
