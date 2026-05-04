using ClassBook.Domain.Entities;
using ClassBook.Domain.Interfaces;
using ClassBook.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

using ClassBook.Application.Facades;

namespace ClassBook.Controllers
{
    /// <summary>
    /// Контроллер для управления пользователями (учителя и администраторы).
    /// Все операции доступны только для авторизованных администраторов.
    /// </summary>
    [ApiController]
    [Route("api/users")]
    [Authorize(Policy = "AdminOnly")] // Только администратор может работать с пользователями
    public class UsersController : ApiControllerBase
    {
        
        private readonly AppDbContext _db;
        private readonly IPasswordHasher _hasher;
        private readonly AuditFacade _auditFacade;

        /// <summary>
        /// Конструктор контроллера.
        /// </summary>
        /// <param name="db">Контекст базы данных</param>
        /// <param name="passwordHasher">Сервис хэширования паролей</param>
        public UsersController(AppDbContext db, IPasswordHasher passwordHasher, AuditFacade auditFacade)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _hasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _auditFacade = auditFacade ?? throw new ArgumentNullException(nameof(auditFacade));
        }

        private int GetCurrentUserId()
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : 0;
        }

        /// <summary>
        /// Получает список всех пользователей.
        /// </summary>
        /// <returns>Список пользователей с ролями</returns>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var users = await _db.Users
                .Include(u => u.Role)
                .Select(u => new
                {
                    u.Id,
                    u.Login,
                    u.FullName,
                    RoleId = u.RoleId,
                    RoleName = u.Role.Name,
                    u.IsActive,
                    u.MustChangePassword,
                    u.CreatedAt
                })
                .ToListAsync();

            return Ok(users);
        }

        /// <summary>
        /// Получает список учителей.
        /// </summary>
        /// <returns>Список учителей</returns>
        [HttpGet("teachers")]
        public async Task<IActionResult> GetTeachers()
        {
            var teachers = await _db.Users
                .Where(u => u.RoleId == 2) // 2 = Учитель
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.Login
                })
                .OrderBy(u => u.FullName)
                .ToListAsync();

            return Ok(teachers);
        }

        /// <summary>
        /// Получает пользователя по идентификатору.
        /// </summary>
        /// <param name="id">Идентификатор пользователя</param>
        /// <returns>Данные пользователя</returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
                return NotFoundError("Пользователь не найден");

            return Ok(new
            {
                user.Id,
                user.Login,
                user.FullName,
                RoleId = user.RoleId,
                RoleName = user.Role.Name,
                user.IsActive,
                user.MustChangePassword,
                user.CreatedAt
            });
        }

        /// <summary>
        /// Создаёт нового пользователя (только администратор).
        /// </summary>
        /// <param name="dto">Данные для создания</param>
        /// <returns>Созданный пользователь</returns>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Login) ||
                string.IsNullOrWhiteSpace(dto.Password) ||
                string.IsNullOrWhiteSpace(dto.FullName))
                return BadRequestError("Логин, пароль и ФИО обязательны");

            if (await _db.Users.AnyAsync(u => u.Login == dto.Login))
                return BadRequestError("Логин уже занят");

            // Проверяем что роль существует в БД (1-6)
            if (dto.RoleId < 1 || dto.RoleId > 6)
                return BadRequestError("Недопустимая роль");

            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == dto.RoleId);
            if (role == null)
                return BadRequestError("Роль не найдена");

            if (role.Name == "Родитель")
                return BadRequestError("Новые учетные записи родителей создавайте только из карточки конкретного ученика");

            if (role.Name == "Ученик")
                return BadRequestError("Новые учетные записи учеников создавайте только из карточки конкретного ученика");

            var user = new User
            {
                Login = dto.Login,
                FullName = dto.FullName,
                PasswordHash = _hasher.Hash(dto.Password),
                RoleId = role.Id,
                IsActive = true,
                MustChangePassword = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var currentUserId = GetCurrentUserId();
            if (currentUserId > 0)
            {
                await _auditFacade.LogActionAsync(currentUserId, "User", user.Id, "Create", null, new
                {
                    user.Login,
                    user.FullName,
                    user.RoleId,
                    user.IsActive,
                    user.MustChangePassword
                });
            }

            return CreatedAtAction(nameof(GetById), new { id = user.Id }, new
            {
                user.Id,
                user.Login,
                user.FullName,
                user.RoleId,
                user.MustChangePassword
            });
        }

        /// <summary>
        /// Обновляет данные пользователя (только администратор).
        /// </summary>
        /// <param name="id">Идентификатор пользователя</param>
        /// <param name="dto">Данные для обновления</param>
        /// <returns>Сообщение об успехе</returns>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUserDto dto)
        {
            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return NotFoundError("Пользователь не найден");

            var currentRoleName = user.Role?.Name ?? string.Empty;
            var oldValues = new
            {
                user.Login,
                user.FullName,
                RoleId = user.RoleId,
                user.IsActive,
                user.MustChangePassword
            };

            if (!string.IsNullOrEmpty(dto.Login) && dto.Login != user.Login)
            {
                if (await _db.Users.AnyAsync(u => u.Login == dto.Login))
                    return BadRequestError("Логин уже занят");
                user.Login = dto.Login;
            }

            if (!string.IsNullOrEmpty(dto.FullName))
                user.FullName = dto.FullName;

            if (dto.RoleId.HasValue && dto.RoleId != user.RoleId)
            {
                // Проверяем что роль существует в БД (1-6)
                if (dto.RoleId < 1 || dto.RoleId > 6)
                    return BadRequestError("Недопустимая роль");

                var targetRole = await _db.Roles.FirstOrDefaultAsync(r => r.Id == dto.RoleId.Value);
                if (targetRole == null)
                    return BadRequestError("Роль не найдена");

                var targetRoleName = targetRole.Name;
                var touchesStrictRole =
                    currentRoleName == "Родитель" ||
                    currentRoleName == "Ученик" ||
                    targetRoleName == "Родитель" ||
                    targetRoleName == "Ученик";

                if (touchesStrictRole)
                    return BadRequestError("Менять роль на 'Родитель' или 'Ученик' и обратно через общую форму пользователей нельзя. Используйте карточку ученика и специализированные сценарии выдачи доступа.");

                user.RoleId = targetRole.Id;
            }

            if (!string.IsNullOrEmpty(dto.Password))
            {
                user.PasswordHash = _hasher.Hash(dto.Password);
                user.MustChangePassword = true;
            }

            if (dto.IsActive.HasValue)
                user.IsActive = dto.IsActive.Value;

            await _db.SaveChangesAsync();

            var currentUserId = GetCurrentUserId();
            if (currentUserId > 0)
            {
                await _auditFacade.LogActionAsync(currentUserId, "User", user.Id, "Update", oldValues, new
                {
                    user.Login,
                    user.FullName,
                    RoleId = user.RoleId,
                    user.IsActive,
                    user.MustChangePassword,
                    PasswordReset = !string.IsNullOrEmpty(dto.Password)
                });
            }
            return Ok(new { message = "Пользователь обновлён" });
        }

        /// <summary>
        /// Получает студентов конкретного родителя (для админа).
        /// </summary>
        /// <param name="parentId">ID родителя</param>
        /// <returns>Список студентов родителя</returns>
        [HttpGet("{parentId}/students")]
        public async Task<IActionResult> GetParentStudents(int parentId)
        {
            // Проверяем что пользователь существует и является родителем
            var parent = await _db.Users
                .Include(u => u.Role)
                .Include(u => u.StudentParents!)
                .ThenInclude(sp => sp.Student)
                .ThenInclude(s => s.Class)
                .FirstOrDefaultAsync(u => u.Id == parentId);
            
            if (parent == null)
                return NotFoundError("Родитель не найден");

            if (parent.Role.Name != "Родитель")
                return BadRequestError("Пользователь не является родителем");

            // Получаем всех студентов этого родителя через навигационное свойство
            var students = (parent.StudentParents ?? new List<StudentParent>())
                .Select(sp => new
                {
                    sp.Student.StudentId,
                    sp.Student.FirstName,
                    sp.Student.LastName,
                    sp.Student.BirthDate,
                    sp.Student.ClassId,
                    ClassName = sp.Student.Class?.Name ?? "не определен"
                })
                .ToList();

            return Ok(students);
        }

        /// <summary>
        /// Удаляет пользователя (только администратор).
        /// </summary>
        /// <param name="id">Идентификатор пользователя</param>
        /// <returns>NoContent</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return NotFoundError("Пользователь не найден");

            // Проверка на связанные данные
            if (await _db.Subjects.AnyAsync(s => s.TeacherId == id) ||
                await _db.Lessons.AnyAsync(l => l.TeacherId == id))
            {
                return BadRequestError("Нельзя удалить учителя с привязанными предметами или уроками");
            }

            var currentUserId = GetCurrentUserId();
            var oldValues = new
            {
                user.Login,
                user.FullName,
                user.RoleId,
                user.IsActive,
                user.MustChangePassword
            };

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();

            if (currentUserId > 0)
            {
                await _auditFacade.LogActionAsync(currentUserId, "User", id, "Delete", oldValues, null);
            }

            return NoContent();
        }
    }

    /// <summary>
    /// DTO для создания пользователя
    /// </summary>
    public class CreateUserDto
    {
        /// <summary>
        /// Логин пользователя
        /// </summary>
        public string Login { get; set; } = null!;

        /// <summary>
        /// Полное имя пользователя
        /// </summary>
        public string FullName { get; set; } = null!;

        /// <summary>
        /// Пароль пользователя
        /// </summary>
        public string Password { get; set; } = null!;

        /// <summary>
        /// Идентификатор роли (1 = Администратор, 2 = Учитель)
        /// </summary>
        public int RoleId { get; set; }
    }

    /// <summary>
    /// DTO для обновления пользователя (все поля опциональные)
    /// </summary>
    public class UpdateUserDto
    {
        /// <summary>
        /// Новый логин (опционально)
        /// </summary>
        public string? Login { get; set; }

        /// <summary>
        /// Новое полное имя (опционально)
        /// </summary>
        public string? FullName { get; set; }

        /// <summary>
        /// Новый пароль (опционально)
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Новая роль (опционально)
        /// </summary>
        public int? RoleId { get; set; }

        /// <summary>
        /// Новый статус активности (опционально)
        /// </summary>
        public bool? IsActive { get; set; }
    }
}
