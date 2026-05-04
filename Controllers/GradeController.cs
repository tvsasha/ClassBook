using ClassBook.Application.Facades;
using ClassBook.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Threading.Tasks;

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

        [HttpPost]
        public async Task<IActionResult> AddGrade([FromBody] AddGradeRequest dto)
        {
            try
            {
                var userId = GetUserId();
                var grade = await _facade.AddGradeAsync(dto.LessonId, dto.StudentId, dto.Value, userId > 0 ? userId : null);
                var result = new GradeDto
                {
                    GradeId = grade.GradeId,
                    LessonId = grade.LessonId,
                    StudentId = grade.StudentId,
                    Value = grade.Value
                };
                return CreatedAtAction(nameof(AddGrade), new { id = grade.GradeId }, result);
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

        [HttpGet("{lessonId}")]
        public async Task<IActionResult> GetGradesForLesson(int lessonId)
        {
            try
            {
                var grades = await _facade.GetGradesForLessonAsync(lessonId);
                var result = grades.Cast<Grade>()
                    .Select(g => new GradeDto
                    {
                        GradeId = g.GradeId,
                        LessonId = g.LessonId,
                        StudentId = g.StudentId,
                        Value = g.Value,
                        Student = g.Student != null ? new StudentDto
                        {
                            StudentId = g.Student.StudentId,
                            FullName = $"{g.Student.FirstName} {g.Student.LastName}"
                        } : null
                    })
                    .ToList();

                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAllGrades(int teacherId)
        {
            try
            {
                var grades = await _facade.GetAllGradesByTeacherAsync(teacherId);

                var result = grades.Select(g => new
                {
                    lessonId = g.LessonId,
                    studentId = g.StudentId,
                    value = g.Value,
                    student = g.Student != null ? new
                    {
                        fullName = $"{g.Student.FirstName} {g.Student.LastName}"
                    } : null,
                    lesson = g.Lesson != null ? new
                    {
                        id = g.Lesson.LessonId,
                        date = g.Lesson.Date
                    } : null
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке оценок преподавателя {TeacherId}", teacherId);
                return InternalServerError("Не удалось загрузить оценки преподавателя");
            }
        }
        [HttpGet("lesson/{lessonId}/students")]
        public async Task<IActionResult> GetStudentsWithGrades(int lessonId)
        {
            var result = await _facade.GetStudentsWithGradesAsync(lessonId);
            return Ok(result);
        }

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

    public class AddGradeRequest
    {
        public int LessonId { get; set; }
        public int StudentId { get; set; }
        public int Value { get; set; }
    }


    public class GradeDto
    {
        public int GradeId { get; set; }
        public int LessonId { get; set; }
        public int StudentId { get; set; }
        public int Value { get; set; }
        public StudentDto? Student { get; set; }
    }

    public class StudentDto
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = string.Empty;
    }
}
