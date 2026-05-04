using ClassBook.Application.Facades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/admin/students")]
    [Authorize(Policy = "AdminOnly")]
    public class AdminStudentController : ApiControllerBase
    {
        private readonly StudentFacade _facade;
        private readonly ParentFacade _parentFacade;
        private readonly AuditFacade _auditFacade;

        public AdminStudentController(StudentFacade facade, ParentFacade parentFacade, AuditFacade auditFacade)
        {
            _facade = facade;
            _parentFacade = parentFacade;
            _auditFacade = auditFacade;
        }

        private int GetCurrentUserId()
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : 0;
        }

        [HttpPost]
        public async Task<IActionResult> CreateStudent([FromBody] CreateStudentDto dto)
        {
            try
            {
                var student = await _facade.CreateStudentAsync(dto.FirstName, dto.LastName, dto.BirthDate, dto.ClassId);
                var currentUserId = GetCurrentUserId();
                if (currentUserId > 0)
                {
                    await _auditFacade.LogActionAsync(currentUserId, "Student", student.StudentId, "Create", null, new
                    {
                        student.FirstName,
                        student.LastName,
                        student.BirthDate,
                        student.ClassId
                    });
                }
                return Ok(student);
            }
            catch (ArgumentException ex)
            {
                return BadRequestError(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
        }

        [HttpPost("{studentId}/account")]
        public async Task<IActionResult> CreateStudentAccount(int studentId, [FromBody] CreateStudentAccountDto dto)
        {
            try
            {
                var user = await _facade.CreateStudentAccountAsync(studentId, dto.Login, dto.Password);
                var currentUserId = GetCurrentUserId();
                if (currentUserId > 0)
                {
                    await _auditFacade.LogActionAsync(currentUserId, "User", user.Id, "IssueStudentAccess", null, new
                    {
                        StudentId = studentId,
                        user.Login,
                        user.FullName,
                        user.MustChangePassword
                    });
                }
                return Ok(new
                {
                    user.Id,
                    user.Login,
                    user.FullName,
                    user.MustChangePassword,
                    message = "Учетная запись ученика создана"
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequestError(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
        }

        [HttpPost("{studentId}/parent-account")]
        public async Task<IActionResult> CreateParentAccountForStudent(int studentId, [FromBody] CreateParentAccountDto dto)
        {
            try
            {
                var parent = await _parentFacade.CreateParentAccountForStudentAsync(studentId, dto.FullName, dto.Login, dto.Password);
                var currentUserId = GetCurrentUserId();
                if (currentUserId > 0)
                {
                    await _auditFacade.LogActionAsync(currentUserId, "User", parent.Id, "IssueParentAccess", null, new
                    {
                        StudentId = studentId,
                        parent.Login,
                        parent.FullName,
                        parent.MustChangePassword
                    });
                }
                return Ok(new
                {
                    parent.Id,
                    parent.Login,
                    parent.FullName,
                    parent.MustChangePassword,
                    message = "Учетная запись родителя создана и привязана к ученику"
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequestError(ex.Message);
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

        [HttpGet]
        public async Task<IActionResult> GetAllStudents()
        {
            try
            {
                var students = await _facade.GetAllStudentsAsync();
                return Ok(students);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdminStudentController.GetAllStudents] Exception: {ex.Message}");
                Console.WriteLine($"[AdminStudentController.GetAllStudents] StackTrace: {ex.StackTrace}");
                return InternalServerError("Не удалось загрузить список учеников");
            }
        }

        [HttpPost("parent-students")]
        public async Task<IActionResult> AttachStudentToParent([FromBody] AttachStudentToParentDto dto)
        {
            try
            {
                if (dto.ParentId <= 0 || dto.StudentId <= 0)
                    return BadRequestError("ParentId и StudentId обязательны и должны быть больше 0");

                await _facade.AttachStudentToParentAsync(dto.ParentId, dto.StudentId);
                var currentUserId = GetCurrentUserId();
                if (currentUserId > 0)
                {
                    await _auditFacade.LogActionAsync(currentUserId, "StudentParent", dto.StudentId, "AttachParent", null, new
                    {
                        dto.ParentId,
                        dto.StudentId
                    });
                }
                return Ok(new { message = "Ученик успешно привязан к родителю" });
            }
            catch (ArgumentException ex)
            {
                return BadRequestError(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
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
                return NotFoundError(ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateStudent(int id, [FromBody] CreateStudentDto dto)
        {
            try
            {
                var updated = await _facade.UpdateStudentAsync(id, dto.FirstName, dto.LastName, dto.BirthDate, dto.ClassId);
                var currentUserId = GetCurrentUserId();
                if (currentUserId > 0)
                {
                    await _auditFacade.LogActionAsync(currentUserId, "Student", id, "Update", null, new
                    {
                        dto.FirstName,
                        dto.LastName,
                        dto.BirthDate,
                        dto.ClassId
                    });
                }
                return Ok(updated);
            }
            catch (ArgumentException ex)
            {
                return BadRequestError(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            try
            {
                await _facade.DeleteStudentAsync(id);
                var currentUserId = GetCurrentUserId();
                if (currentUserId > 0)
                {
                    await _auditFacade.LogActionAsync(currentUserId, "Student", id, "Delete", new
                    {
                        StudentId = id
                    }, null);
                }
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
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

    public class CreateStudentAccountDto
    {
        public string Login { get; set; } = null!;
        public string Password { get; set; } = null!;
    }

    public class CreateParentAccountDto
    {
        public string FullName { get; set; } = null!;
        public string Login { get; set; } = null!;
        public string Password { get; set; } = null!;
    }

    public class AttachStudentToParentDto
    {
        public int ParentId { get; set; }
        public int StudentId { get; set; }
    }
}
