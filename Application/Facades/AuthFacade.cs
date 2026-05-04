using ClassBook.Domain.Entities;
using ClassBook.Domain.Interfaces;
using ClassBook.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ClassBook.Application.Facades
{
    /// <summary>
    /// Фасад для операций аутентификации и управления паролями.
    /// </summary>
    public class AuthFacade
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher _hasher;

        public AuthFacade(AppDbContext db, IPasswordHasher hasher)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        }

        /// <summary>
        /// Выполняет вход пользователя в систему.
        /// </summary>
        public async Task<User?> LoginAsync(string login, string password)
        {
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
                return null;

            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Login == login && u.IsActive);

            if (user == null)
                return null;

            if (!_hasher.Verify(password, user.PasswordHash))
                return null;

            if (_hasher.NeedsRehash(user.PasswordHash))
            {
                user.PasswordHash = _hasher.Hash(password);
                await _db.SaveChangesAsync();
            }

            return user;
        }

        /// <summary>
        /// Меняет пароль текущего пользователя.
        /// </summary>
        public async Task<User> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
                throw new ArgumentException("Текущий и новый пароль обязательны");

            if (newPassword.Length < 8)
                throw new ArgumentException("Новый пароль должен содержать не менее 8 символов");

            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (user == null)
                throw new KeyNotFoundException("Пользователь не найден");

            if (!_hasher.Verify(currentPassword, user.PasswordHash))
                throw new InvalidOperationException("Текущий пароль указан неверно");

            user.PasswordHash = _hasher.Hash(newPassword);
            user.MustChangePassword = false;

            await _db.SaveChangesAsync();
            return user;
        }

        /// <summary>
        /// Регистрирует нового пользователя.
        /// </summary>
        public async Task<User> RegisterAsync(string login, string fullName, string password, int roleId, bool mustChangePassword = false)
        {
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Все поля обязательны");

            if (await _db.Users.AnyAsync(u => u.Login == login))
                throw new InvalidOperationException("Логин уже занят");

            if (!await _db.Roles.AnyAsync(r => r.Id == roleId))
                throw new InvalidOperationException("Роль не найдена");

            var user = new User
            {
                Login = login.Trim(),
                FullName = fullName.Trim(),
                PasswordHash = _hasher.Hash(password),
                RoleId = roleId,
                IsActive = true,
                MustChangePassword = mustChangePassword,
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return user;
        }
    }
}
