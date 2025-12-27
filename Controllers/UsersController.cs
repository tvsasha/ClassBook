using ClassBook.Domain.Entities;
using ClassBook.Domain.Interfaces;
using ClassBook.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace ClassBook.Controllers
{
    /// <summary>
    /// Контроллер для управления пользователями (учителя и администраторы).
    /// Все операции доступны только для авторизованных администраторов.
    /// </summary>
    [ApiController]
    [Route("api/users")]
    [Authorize(Policy = "AdminOnly")] // Только администратор может работать с пользователями
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher _hasher;

        /// <summary>
        /// Конструктор контроллера.
        /// </summary>
        /// <param name="db">Контекст базы данных</param>
        /// <param name="passwordHasher">Сервис хэширования паролей</param>
        public UsersController(AppDbContext db, IPasswordHasher passwordHasher)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _hasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
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
                return NotFound("Пользователь не найден");

            return Ok(new
            {
                user.Id,
                user.Login,
                user.FullName,
                RoleId = user.RoleId,
                RoleName = user.Role.Name,
                user.IsActive,
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
                return BadRequest("Логин, пароль и ФИО обязательны");

            if (await _db.Users.AnyAsync(u => u.Login == dto.Login))
                return BadRequest("Логин уже занят");

            if (dto.RoleId != 1 && dto.RoleId != 2)
                return BadRequest("Недопустимая роль (только Администратор или Учитель)");

            var user = new User
            {
                Login = dto.Login,
                FullName = dto.FullName,
                PasswordHash = _hasher.Hash(dto.Password),
                RoleId = dto.RoleId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = user.Id }, new
            {
                user.Id,
                user.Login,
                user.FullName,
                user.RoleId
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
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return NotFound("Пользователь не найден");

            if (!string.IsNullOrEmpty(dto.Login) && dto.Login != user.Login)
            {
                if (await _db.Users.AnyAsync(u => u.Login == dto.Login))
                    return BadRequest("Логин уже занят");
                user.Login = dto.Login;
            }

            if (!string.IsNullOrEmpty(dto.FullName))
                user.FullName = dto.FullName;

            if (dto.RoleId.HasValue && dto.RoleId != user.RoleId)
            {
                if (dto.RoleId != 1 && dto.RoleId != 2)
                    return BadRequest("Недопустимая роль");
                user.RoleId = dto.RoleId.Value;
            }

            if (!string.IsNullOrEmpty(dto.Password))
                user.PasswordHash = _hasher.Hash(dto.Password);

            if (dto.IsActive.HasValue)
                user.IsActive = dto.IsActive.Value;

            await _db.SaveChangesAsync();
            return Ok(new { message = "Пользователь обновлён" });
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
                return NotFound("Пользователь не найден");

            // Проверка на связанные данные
            if (await _db.Subjects.AnyAsync(s => s.TeacherId == id) ||
                await _db.Lessons.AnyAsync(l => l.TeacherId == id))
            {
                return BadRequest("Нельзя удалить учителя с привязанными предметами или уроками");
            }

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();

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