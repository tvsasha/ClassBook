using ClassBook.Application.DTOs;
using ClassBook.Application.Facades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/teacher")]
    [Authorize(Roles = "Учитель,Администратор")]
    public class TeacherController : ApiControllerBase
    {
        private readonly SubjectFacade _subjectFacade;
        private readonly ClassFacade _classFacade;
        private readonly StudentFacade _studentFacade;
        private readonly LessonFacade _lessonFacade;
        private readonly GradeFacade _gradeFacade;
        private readonly AttendanceFacade _attendanceFacade;

        public TeacherController(
            SubjectFacade subjectFacade,
            ClassFacade classFacade,
            StudentFacade studentFacade,
            LessonFacade lessonFacade,
            GradeFacade gradeFacade,
            AttendanceFacade attendanceFacade)
        {
            _subjectFacade = subjectFacade;
            _classFacade = classFacade;
            _studentFacade = studentFacade;
            _lessonFacade = lessonFacade;
            _gradeFacade = gradeFacade;
            _attendanceFacade = attendanceFacade;
        }

        /// <summary>
        /// Возвращает список предметов преподавателя или выбранного преподавателя для администратора.
        /// </summary>
        /// <param name="teacherId">Необязательный идентификатор преподавателя.</param>
        /// <returns>Список доступных предметов.</returns>
        [HttpGet("subjects")]
        public async Task<IActionResult> GetSubjects(int? teacherId = null)
        {
            var effectiveTeacherId = teacherId ?? GetCurrentUserId();
            return Ok(await _subjectFacade.GetSubjectsForTeacherAsync(effectiveTeacherId));
        }

        /// <summary>
        /// Возвращает список классов преподавателя или выбранного преподавателя для администратора.
        /// </summary>
        /// <param name="teacherId">Необязательный идентификатор преподавателя.</param>
        /// <returns>Список классов.</returns>
        [HttpGet("classes")]
        public async Task<IActionResult> GetClasses(int? teacherId = null)
        {
            var effectiveTeacherId = teacherId ?? GetCurrentUserId();
            return Ok(await _classFacade.GetClassesForTeacherAsync(effectiveTeacherId));
        }

        /// <summary>
        /// Возвращает учеников выбранного класса.
        /// </summary>
        /// <param name="classId">Идентификатор класса.</param>
        /// <returns>Список учеников класса.</returns>
        [HttpGet("classes/{classId}/students")]
        public async Task<IActionResult> GetStudentsByClass(int classId)
        {
            try
            {
                return Ok(await _studentFacade.GetStudentsByClassAsync(classId));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
        }

        /// <summary>
        /// Возвращает уроки преподавателя или выбранного преподавателя для администратора.
        /// </summary>
        /// <param name="teacherId">Необязательный идентификатор преподавателя.</param>
        /// <returns>Список уроков.</returns>
        [HttpGet("lessons")]
        public async Task<IActionResult> GetLessons(int? teacherId = null)
        {
            var effectiveTeacherId = teacherId ?? GetCurrentUserId();
            return Ok(await _lessonFacade.GetLessonsForTeacherAsync(effectiveTeacherId));
        }

        /// <summary>
        /// Создаёт новый урок от имени преподавателя или администратора.
        /// </summary>
        /// <param name="dto">Данные создаваемого урока.</param>
        /// <returns>Созданный урок.</returns>
        [HttpPost("lessons")]
        public async Task<IActionResult> CreateLesson([FromBody] CreateLessonDto dto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var requestedTeacherId = dto.TeacherId == 0 ? currentUserId : dto.TeacherId;
                if (requestedTeacherId != currentUserId && !User.IsInRole("Администратор"))
                    return ForbiddenError("Вы можете создавать только свои уроки");

                var lesson = await _lessonFacade.CreateLessonAsync(dto.SubjectId, dto.ClassId, requestedTeacherId, dto.Topic, dto.Date, dto.Homework);
                return CreatedAtAction(nameof(GetLessons), new { id = lesson.LessonId }, lesson);
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
        /// Обновляет существующий урок.
        /// </summary>
        /// <param name="lessonId">Идентификатор урока.</param>
        /// <param name="dto">Новые данные урока.</param>
        /// <returns>Обновлённый урок.</returns>
        [HttpPut("lessons/{lessonId}")]
        public async Task<IActionResult> UpdateLesson(int lessonId, [FromBody] CreateLessonDto dto)
        {
            try
            {
                var existingLesson = await _lessonFacade.GetLessonByIdAsync(lessonId);
                if (existingLesson == null)
                    return NotFoundError("Урок не найден");

                var currentUserId = GetCurrentUserId();
                if (existingLesson.TeacherId != currentUserId && !User.IsInRole("Администратор"))
                    return ForbiddenError("Вы можете редактировать только свои уроки");

                var updatedLesson = await _lessonFacade.UpdateLessonAsync(lessonId, dto.SubjectId, dto.ClassId, dto.TeacherId, dto.Topic, dto.Date, dto.Homework);
                return Ok(updatedLesson);
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
        /// Удаляет урок преподавателя.
        /// </summary>
        /// <param name="lessonId">Идентификатор урока.</param>
        /// <returns>Пустой ответ при успешном удалении.</returns>
        [HttpDelete("lessons/{lessonId}")]
        public async Task<IActionResult> DeleteLesson(int lessonId)
        {
            try
            {
                var lesson = await _lessonFacade.GetLessonByIdAsync(lessonId);
                if (lesson == null)
                    return NotFoundError("Урок не найден");

                var currentUserId = GetCurrentUserId();
                if (lesson.TeacherId != currentUserId && !User.IsInRole("Администратор"))
                    return ForbiddenError("Вы можете удалять только свои уроки");

                await _lessonFacade.DeleteLessonAsync(lessonId);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
        }

        /// <summary>
        /// Выставляет оценку ученику через преподавательский режим.
        /// </summary>
        /// <param name="dto">Данные оценки, урока и ученика.</param>
        /// <returns>Подтверждение успешного выставления оценки.</returns>
        [HttpPost("grades/mark")]
        public async Task<IActionResult> AddGrade([FromBody] AddGradeDto dto)
        {
            try
            {
                var grade = await _gradeFacade.AddGradeAsync(dto.LessonId, dto.StudentId, dto.Value);
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
        /// Отмечает посещаемость ученика на уроке.
        /// </summary>
        /// <param name="dto">Данные урока, ученика и статуса посещаемости.</param>
        /// <returns>Подтверждение успешной отметки посещаемости.</returns>
        [HttpPost("attendance/mark")]
        public async Task<IActionResult> MarkAttendance([FromBody] MarkAttendanceDto dto)
        {
            try
            {
                await _attendanceFacade.MarkAttendanceAsync(dto.LessonId, dto.StudentId, dto.Status);
                return Ok("Посещаемость успешно отмечена");
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

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                throw new UnauthorizedAccessException("ID пользователя не найден");

            return userId;
        }
    }
}
