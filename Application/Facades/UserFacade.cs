// Application/Facades/UserFacade.cs
using ClassBook.Application.DTOs;
using ClassBook.Domain.Entities;
using ClassBook.Domain.Interfaces;
using ClassBook.Domain.Constants;
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
        public async Task<IEnumerable<UserListItemDto>> GetAllUsersAsync()
        {
            return await _db.Users
                .Include(u => u.Role)
                .Select(u => new UserListItemDto
                {
                    Id = u.Id,
                    Login = u.Login,
                    FullName = u.FullName,
                    RoleId = u.RoleId,
                    RoleName = u.Role.Name,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();
        }

        /// <summary>
        /// Получает учителей.
        /// </summary>
        public async Task<IEnumerable<TeacherLookupDto>> GetTeachersAsync()
        {
            return await _db.Users
                .Where(u => u.RoleId == SystemRoleIds.Teacher)
                .Select(u => new TeacherLookupDto
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Login = u.Login
                })
                .OrderBy(u => u.FullName)
                .ToListAsync();
        }

        /// <summary>
        /// Получает пользователя по ID.
        /// </summary>
        public async Task<UserListItemDto?> GetUserByIdAsync(int id)
        {
            var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id);
            return user == null ? null : new UserListItemDto
            {
                Id = user.Id,
                Login = user.Login,
                FullName = user.FullName,
                RoleId = user.RoleId,
                RoleName = user.Role.Name,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
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
                // Проверяем что роль существует в БД (1-6)
                if (roleId < 1 || roleId > 6)
                    throw new InvalidOperationException("Недопустимая роль");
                user.RoleId = roleId.Value;
            }
            if (!string.IsNullOrEmpty(password))
            {
                user.PasswordHash = _hasher.Hash(password);
                user.MustChangePassword = true;
            }
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
