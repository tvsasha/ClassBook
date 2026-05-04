using ClassBook.Application.Facades;
using ClassBook.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

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

        // ── Просмотр данных ─────────────────────────────────────────────────────────────────────────────

        [HttpGet("subjects")]
        public async Task<IActionResult> GetSubjects(int? teacherId = null)
        {
            var effectiveTeacherId = teacherId ?? GetCurrentUserId();
            var subjects = await _subjectFacade.GetSubjectsForTeacherAsync(effectiveTeacherId);
            return Ok(subjects);
        }

        [HttpGet("classes")]
        public async Task<IActionResult> GetClasses(int? teacherId = null)
        {
            var effectiveTeacherId = teacherId ?? GetCurrentUserId();
            var classes = await _classFacade.GetClassesForTeacherAsync(effectiveTeacherId);
            return Ok(classes);
        }

        [HttpGet("classes/{classId}/students")]
        public async Task<IActionResult> GetStudentsByClass(int classId)
        {
            try
            {
                var students = await _studentFacade.GetStudentsByClassAsync(classId);
                return Ok(students);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
        }

        [HttpGet("lessons")]
        public async Task<IActionResult> GetLessons(int? teacherId = null)
        {
            var effectiveTeacherId = teacherId ?? GetCurrentUserId();
            var lessons = await _lessonFacade.GetLessonsForTeacherAsync(effectiveTeacherId);
            return Ok(lessons);
        }

        // ── Создание урока ──────────────────────────────────────────────────────────────────────────────

        [HttpPost("lessons")]
        public async Task<IActionResult> CreateLesson([FromBody] CreateLessonDto dto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (dto.TeacherId != currentUserId && !User.IsInRole("Администратор"))
                    return ForbiddenError("Вы можете создавать только свои уроки");

                var lesson = await _lessonFacade.CreateLessonAsync(
                    dto.SubjectId,
                    dto.ClassId,
                    dto.TeacherId,
                    dto.Topic,
                    dto.Date,
                    dto.Homework
                );

                var response = new
                {
                    lesson.LessonId,
                    lesson.SubjectId,
                    lesson.ClassId,
                    lesson.TeacherId,
                    lesson.Topic,
                    lesson.Date,
                    lesson.Homework
                };

                return CreatedAtAction(nameof(GetLessons), new { id = lesson.LessonId }, response);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequestError(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
        }

        // ── Удаление урока для учителя/админа ───────────────────────────────────────────────────────────
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

                var updatedLesson = await _lessonFacade.UpdateLessonAsync(
                    lessonId,
                    dto.SubjectId,
                    dto.ClassId,
                    dto.TeacherId,
                    dto.Topic,
                    dto.Date,
                    dto.Homework
                );

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

        // ── Удаление урока из панели учителя ────────────────────────────────────────────────────────
        

        // ── Оценки и посещаемость ──────────────────────────────────────────────────────────────────────

        [HttpPost("grades/mark")]           // ← ИЗМЕНЕНО ЗДЕСЬ
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

        

        // Было: api/teacher/attendance
        // Стало: api/teacher/attendance/mark
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
        }

        // ── Вспомогательные методы ─────────────────────────────────────────────────────────────────────

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                throw new UnauthorizedAccessException("ID пользователя не найден");
            }
            return userId;
        }
    }


    // DTO-шки остаются без изменений
    public class CreateLessonDto
    {
        public int SubjectId { get; set; }
        public int ClassId { get; set; }
        public int TeacherId { get; set; }
        public string Topic { get; set; } = null!;
        public DateTime Date { get; set; }
        public string? Homework { get; set; }
    }

    public class AddGradeDto
    {
        public int LessonId { get; set; }
        public int StudentId { get; set; }
        public int Value { get; set; }
    }

    public class MarkAttendanceDto
    {
        public int LessonId { get; set; }
        public int StudentId { get; set; }
        public byte Status { get; set; }
    }
}
