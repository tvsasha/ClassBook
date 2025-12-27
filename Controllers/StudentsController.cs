using ClassBook.Application.Facades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/admin/students")]
    [Authorize(Policy = "AdminOnly")]
    public class StudentController : ControllerBase
    {
        private readonly StudentFacade _facade;

        public StudentController(StudentFacade facade)
        {
            _facade = facade;
        }

        [HttpPost]
        public async Task<IActionResult> CreateStudent([FromBody] CreateStudentDto dto)
        {
            try
            {
                var student = await _facade.CreateStudentAsync(dto.FirstName, dto.LastName, dto.BirthDate, dto.ClassId);
                return Ok(student);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpGet("{classId}")]
        public async Task<IActionResult> GetStudentsByClass(int classId)
        {
            try
            {
                var students = await _facade.GetStudentsByClassAsync(classId);
                return Ok(students);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateStudent(int id, [FromBody] CreateStudentDto dto)
        {
            try
            {
                var updated = await _facade.UpdateStudentAsync(id, dto.FirstName, dto.LastName, dto.BirthDate, dto.ClassId);
                return Ok(updated);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }
    }

    public class CreateStudentDto
    {
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public DateTime BirthDate { get; set; }
        public int? ClassId { get; set; }
    }
}