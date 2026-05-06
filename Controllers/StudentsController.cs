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
    [Route("api/admin/students")]
    [Authorize(Policy = "AdminOnly")]
    public class AdminStudentController : ApiControllerBase
    {
        private readonly StudentFacade _facade;
        private readonly ParentFacade _parentFacade;
        private readonly AuditFacade _auditFacade;
        private readonly ILogger<AdminStudentController> _logger;

        public AdminStudentController(StudentFacade facade, ParentFacade parentFacade, AuditFacade auditFacade, ILogger<AdminStudentController> logger)
        {
            _facade = facade;
            _parentFacade = parentFacade;
            _auditFacade = auditFacade;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : 0;
        }

        /// <summary>
        /// Создаёт карточку ученика без создания учетной записи.
        /// </summary>
        /// <param name="dto">Основные данные ученика.</param>
        /// <returns>Созданная карточка ученика.</returns>
        [HttpPost]
        public async Task<IActionResult> CreateStudent([FromBody] CreateStudentDto dto)
        {
            try
            {
                var student = await _facade.CreateStudentAsync(dto.FirstName, dto.LastName, dto.BirthDate, dto.ClassId);
                var currentUserId = GetCurrentUserId();
                if (currentUserId > 0)
                {
                    await _auditFacade.LogActionAsync<StudentAuditDto>(currentUserId, "Student", student.StudentId, "Create", null, new StudentAuditDto
                    {
                        FirstName = student.FirstName,
                        LastName = student.LastName,
                        BirthDate = student.BirthDate,
                        ClassId = student.ClassId
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

        /// <summary>
        /// Создаёт учетную запись для уже существующей карточки ученика.
        /// </summary>
        /// <param name="studentId">Идентификатор ученика.</param>
        /// <param name="dto">Логин и временный пароль для выдачи доступа.</param>
        /// <returns>Данные созданной ученической учетной записи.</returns>
        [HttpPost("{studentId}/account")]
        public async Task<IActionResult> CreateStudentAccount(int studentId, [FromBody] CreateStudentAccountDto dto)
        {
            try
            {
                var user = await _facade.CreateStudentAccountAsync(studentId, dto.Login, dto.Password);
                var currentUserId = GetCurrentUserId();
                if (currentUserId > 0)
                {
                    await _auditFacade.LogActionAsync<StudentAccessAuditDto>(currentUserId, "User", user.Id, "IssueStudentAccess", null, new StudentAccessAuditDto
                    {
                        StudentId = studentId,
                        Login = user.Login,
                        FullName = user.FullName,
                        MustChangePassword = user.MustChangePassword
                    });
                }
                return Ok(user);
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

        /// <summary>
        /// Привязывает существующую учетную запись ученика к карточке ученика.
        /// </summary>
        /// <param name="studentId">Идентификатор карточки ученика.</param>
        /// <param name="dto">Идентификатор существующего пользователя с ролью ученика.</param>
        /// <returns>Данные привязанной учетной записи.</returns>
        [HttpPost("{studentId}/attach-account")]
        public async Task<IActionResult> AttachStudentAccount(int studentId, [FromBody] AttachStudentAccountDto dto)
        {
            try
            {
                if (dto.UserId <= 0)
                    return BadRequestError("UserId обязателен и должен быть больше 0");

                var user = await _facade.AttachStudentAccountAsync(studentId, dto.UserId);
                var currentUserId = GetCurrentUserId();
                if (currentUserId > 0)
                {
                    await _auditFacade.LogActionAsync<StudentAccessAuditDto>(currentUserId, "User", user.Id, "AttachStudentAccess", null, new StudentAccessAuditDto
                    {
                        StudentId = studentId,
                        Login = user.Login,
                        FullName = user.FullName,
                        MustChangePassword = user.MustChangePassword
                    });
                }
                return Ok(user);
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

        /// <summary>
        /// Создаёт родительскую учетную запись сразу в контексте выбранного ученика.
        /// </summary>
        /// <param name="studentId">Идентификатор ученика.</param>
        /// <param name="dto">Логин, временный пароль и данные родителя.</param>
        /// <returns>Данные созданной родительской учетной записи.</returns>
        [HttpPost("{studentId}/parent-account")]
        public async Task<IActionResult> CreateParentAccountForStudent(int studentId, [FromBody] CreateParentAccountDto dto)
        {
            try
            {
                var parent = await _parentFacade.CreateParentAccountForStudentAsync(studentId, dto.FullName, dto.Login, dto.Password);
                var currentUserId = GetCurrentUserId();
                if (currentUserId > 0)
                {
                    await _auditFacade.LogActionAsync<StudentAccessAuditDto>(currentUserId, "User", parent.Id, "IssueParentAccess", null, new StudentAccessAuditDto
                    {
                        StudentId = studentId,
                        Login = parent.Login,
                        FullName = parent.FullName,
                        MustChangePassword = parent.MustChangePassword
                    });
                }
                return Ok(new IssuedParentAccountDto
                {
                    Id = parent.Id,
                    Login = parent.Login,
                    FullName = parent.FullName,
                    MustChangePassword = parent.MustChangePassword,
                    Message = "Учетная запись родителя создана и привязана к ученику"
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

        /// <summary>
        /// Возвращает полный список учеников для административного режима.
        /// </summary>
        /// <returns>Список учеников с данными классов и доступов.</returns>
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
                _logger.LogError(ex, "Ошибка при загрузке списка учеников");
                return InternalServerError("Не удалось загрузить список учеников");
            }
        }

        /// <summary>
        /// Привязывает существующего ученика к родительской учетной записи.
        /// </summary>
        /// <param name="dto">Идентификаторы ученика и родителя.</param>
        /// <returns>Подтверждение успешной привязки.</returns>
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
                    await _auditFacade.LogActionAsync<StudentParentLinkAuditDto>(currentUserId, "StudentParent", dto.StudentId, "AttachParent", null, new StudentParentLinkAuditDto
                    {
                        ParentId = dto.ParentId,
                        StudentId = dto.StudentId
                    });
                }
                return Ok(new MessageResponseDto { Message = "Ученик успешно привязан к родителю" });
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

        /// <summary>
        /// Возвращает учеников выбранного класса.
        /// </summary>
        /// <param name="classId">Идентификатор класса.</param>
        /// <returns>Список учеников класса.</returns>
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

        /// <summary>
        /// Обновляет карточку ученика.
        /// </summary>
        /// <param name="id">Идентификатор ученика.</param>
        /// <param name="dto">Новые данные ученика.</param>
        /// <returns>Обновлённая карточка ученика.</returns>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateStudent(int id, [FromBody] CreateStudentDto dto)
        {
            try
            {
                var updated = await _facade.UpdateStudentAsync(id, dto.FirstName, dto.LastName, dto.BirthDate, dto.ClassId);
                var currentUserId = GetCurrentUserId();
                if (currentUserId > 0)
                {
                    await _auditFacade.LogActionAsync<StudentAuditDto>(currentUserId, "Student", id, "Update", null, new StudentAuditDto
                    {
                        FirstName = dto.FirstName,
                        LastName = dto.LastName,
                        BirthDate = dto.BirthDate,
                        ClassId = dto.ClassId
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

        /// <summary>
        /// Удаляет карточку ученика и связанные доступы, если это допустимо по бизнес-логике.
        /// </summary>
        /// <param name="id">Идентификатор ученика.</param>
        /// <returns>Пустой ответ при успешном удалении.</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            try
            {
                await _facade.DeleteStudentAsync(id);
                var currentUserId = GetCurrentUserId();
                if (currentUserId > 0)
                {
                    await _auditFacade.LogActionAsync<StudentDeleteAuditDto>(currentUserId, "Student", id, "Delete", new StudentDeleteAuditDto
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

}
