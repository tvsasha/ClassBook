// Application/Facades/UserFacade.cs
using ClassBook.Domain.Entities;
using ClassBook.Domain.Interfaces;
using ClassBook.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ClassBook.Application.Facades
{
    /// <summary>
    /// Фасад для управления пользователями (учителя, админы).
    /// </summary>
    public class UserFacade
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher _hasher;

        public UserFacade(AppDbContext db, IPasswordHasher hasher)
        {
            _db = db;
            _hasher = hasher;
        }

        /// <summary>
        /// Получает всех пользователей.
        /// </summary>
        public async Task<IEnumerable<object>> GetAllUsersAsync()
        {
            return await _db.Users
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
        }

        /// <summary>
        /// Получает учителей.
        /// </summary>
        public async Task<IEnumerable<object>> GetTeachersAsync()
        {
            return await _db.Users
                .Where(u => u.RoleId == 2)
                .Select(u => new { u.Id, u.FullName, u.Login })
                .OrderBy(u => u.FullName)
                .ToListAsync();
        }

        /// <summary>
        /// Получает пользователя по ID.
        /// </summary>
        public async Task<object?> GetUserByIdAsync(int id)
        {
            var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id);
            return user == null ? null : new
            {
                user.Id,
                user.Login,
                user.FullName,
                RoleId = user.RoleId,
                RoleName = user.Role.Name,
                user.IsActive,
                user.CreatedAt
            };
        }

        /// <summary>
        /// Обновляет пользователя.
        /// </summary>
        public async Task UpdateUserAsync(int id, string? login, string? fullName, string? password, int? roleId, bool? isActive)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) throw new KeyNotFoundException("Пользователь не найден");

            if (!string.IsNullOrEmpty(login) && login != user.Login)
            {
                if (await _db.Users.AnyAsync(u => u.Login == login))
                    throw new InvalidOperationException("Логин занят");
                user.Login = login;
            }

            if (!string.IsNullOrEmpty(fullName)) user.FullName = fullName;
            if (roleId.HasValue && roleId != user.RoleId)
            {
                if (roleId != 1 && roleId != 2)
                    throw new InvalidOperationException("Недопустимая роль");
                user.RoleId = roleId.Value;
            }
            if (!string.IsNullOrEmpty(password)) user.PasswordHash = _hasher.Hash(password);
            if (isActive.HasValue) user.IsActive = isActive.Value;

            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Удаляет пользователя.
        /// </summary>
        public async Task DeleteUserAsync(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) throw new KeyNotFoundException("Пользователь не найден");

            if (await _db.Subjects.AnyAsync(s => s.TeacherId == id) ||
                await _db.Lessons.AnyAsync(l => l.TeacherId == id))
                throw new InvalidOperationException("Нельзя удалить учителя с привязанными предметами/уроками");

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
        }
    }
}