using ClassBook.Application.Facades;
using ClassBook.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/teacher/grades")]
    [Authorize(Roles = "Учитель")]
    public class GradeController : ControllerBase
    {
        private readonly GradeFacade _facade;

        public GradeController(GradeFacade facade)
        {
            _facade = facade;
        }

        [HttpPost]
        public async Task<IActionResult> AddGrade([FromBody] AddGradeRequest dto)
        {
            try
            {
                var grade = await _facade.AddGradeAsync(dto.LessonId, dto.StudentId, dto.Value);
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
                return BadRequest(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
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
                return NotFound(ex.Message);
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
        public StudentDto Student { get; set; }
    }

    public class StudentDto
    {
        public int StudentId { get; set; }
        public string FullName { get; set; }
    }
}