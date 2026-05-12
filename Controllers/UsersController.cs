using ClassBook.Application.DTOs;
using ClassBook.Application.Facades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ClassBook.Controllers
{
    /// <summary>
    /// Контроллер для управления пользователями общего административного потока.
    /// </summary>
    [ApiController]
    [Route("api/users")]
    [Authorize(Policy = "AdminOnly")]
    public class UsersController : ApiControllerBase
    {
        private readonly UserFacade _userFacade;
        private readonly AuditFacade _auditFacade;

        /// <summary>
        /// Создает экземпляр контроллера пользователей.
        /// </summary>
        /// <param name="userFacade">Фасад работы с учетными записями.</param>
        /// <param name="auditFacade">Фасад аудита административных действий.</param>
        public UsersController(UserFacade userFacade, AuditFacade auditFacade)
        {
            _userFacade = userFacade ?? throw new ArgumentNullException(nameof(userFacade));
            _auditFacade = auditFacade ?? throw new ArgumentNullException(nameof(auditFacade));
        }

        private int GetCurrentUserId()
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : 0;
        }

        /// <summary>
        /// Получает список всех пользователей.
        /// </summary>
        /// <returns>Список пользователей с ролями и состоянием доступа.</returns>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _userFacade.GetAllUsersAsync());
        }

        /// <summary>
        /// Получает список учителей.
        /// </summary>
        /// <returns>Упрощенный список преподавателей для выбора в интерфейсе.</returns>
        [HttpGet("teachers")]
        public async Task<IActionResult> GetTeachers()
        {
            return Ok(await _userFacade.GetTeachersAsync());
        }

        /// <summary>
        /// Получает пользователя по идентификатору.
        /// </summary>
        /// <param name="id">Идентификатор пользователя.</param>
        /// <returns>Подробные данные выбранной учетной записи.</returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var user = await _userFacade.GetUserByIdAsync(id);
            if (user == null)
                return NotFoundError("Пользователь не найден");

            return Ok(user);
        }

        /// <summary>
        /// Создает нового пользователя общего административного потока.
        /// </summary>
        /// <param name="dto">Логин, ФИО, пароль и роль новой учетной записи.</param>
        /// <returns>Созданная учетная запись.</returns>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
        {
            try
            {
                var result = await _userFacade.CreateUserAsync(dto);

                var currentUserId = GetCurrentUserId();
                if (currentUserId > 0)
                {
                    await _auditFacade.LogActionAsync<UserMutationAuditDto>(currentUserId, "User", result.User.Id, "Create", null, result.AuditValues);
                }

                return CreatedAtAction(nameof(GetById), new { id = result.User.Id }, result.User);
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
        /// Обновляет данные существующего пользователя.
        /// </summary>
        /// <param name="id">Идентификатор пользователя.</param>
        /// <param name="dto">Новые значения учетной записи.</param>
        /// <returns>Подтверждение успешного обновления.</returns>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUserDto dto)
        {
            try
            {
                var result = await _userFacade.UpdateUserAsync(id, dto);

                var currentUserId = GetCurrentUserId();
                if (currentUserId > 0)
                {
                    await _auditFacade.LogActionAsync<UserMutationAuditDto>(currentUserId, "User", result.User.Id, "Update", result.OldValues, result.NewValues);
                }

                return Ok(new MessageResponseDto { Message = "Пользователь обновлён" });
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
        /// Создает новый временный пароль для существующего пользователя.
        /// </summary>
        /// <param name="id">Идентификатор пользователя.</param>
        /// <returns>Логин и временный пароль, который нужно передать пользователю.</returns>
        [HttpPost("{id}/reset-password")]
        public async Task<IActionResult> ResetPassword(int id)
        {
            try
            {
                var access = await _userFacade.ResetTemporaryPasswordAsync(id);

                var currentUserId = GetCurrentUserId();
                if (currentUserId > 0)
                {
                    await _auditFacade.LogActionAsync<UserMutationAuditDto>(currentUserId, "User", id, "ResetPassword", null, new UserMutationAuditDto
                    {
                        Login = access.Login,
                        FullName = access.FullName,
                        IsActive = true,
                        MustChangePassword = access.MustChangePassword,
                        PasswordReset = true
                    });
                }

                return Ok(access);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
        }


        /// <summary>
        /// Получает список учеников, привязанных к выбранному родителю.
        /// </summary>
        /// <param name="parentId">Идентификатор родительской учетной записи.</param>
        /// <returns>Список связанных учеников.</returns>
        [HttpGet("{parentId}/students")]
        public async Task<IActionResult> GetParentStudents(int parentId)
        {
            try
            {
                return Ok(await _userFacade.GetParentStudentsAsync(parentId));
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
        /// Удаляет пользователя, если это допустимо по связям данных.
        /// </summary>
        /// <param name="id">Идентификатор пользователя.</param>
        /// <returns>Пустой ответ при успешном удалении.</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var result = await _userFacade.DeleteUserAsync(id);

                var currentUserId = GetCurrentUserId();
                if (currentUserId > 0)
                {
                    await _auditFacade.LogActionAsync<UserMutationAuditDto>(currentUserId, "User", id, "Delete", result.OldValues, null);
                }

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
