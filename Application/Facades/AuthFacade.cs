// Application/Facades/AuthFacade.cs
using ClassBook.Domain.Entities;
using ClassBook.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using ClassBook.Infrastructure.Data;

namespace ClassBook.Application.Facades
{
    /// <summary>
    /// Фасад для операций аутентификации и регистрации пользователей.
    /// </summary>
    public class AuthFacade
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher _hasher;

        public AuthFacade(AppDbContext db, IPasswordHasher hasher)
        {
            _db = db;
            _hasher = hasher;
        }

        /// <summary>
        /// Выполняет вход пользователя в систему.
        /// </summary>
        /// <param name="login">Логин</param>
        /// <param name="password">Пароль</param>
        /// <returns>Пользователь, если авторизация успешна; иначе null</returns>
        public async Task<User?> LoginAsync(string login, string password)
        {
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
                return null;

            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Login == login && u.IsActive);

            if (user == null)
                return null;

            return _hasher.Verify(password, user.PasswordHash) ? user : null;
        }

        /// <summary>
        /// Регистрирует нового пользователя.
        /// </summary>
        /// <param name="login">Логин</param>
        /// <param name="fullName">Полное имя</param>
        /// <param name="password">Пароль</param>
        /// <param name="roleId">ID роли</param>
        /// <returns>Созданный пользователь</returns>
        public async Task<User> RegisterAsync(string login, string fullName, string password, int roleId)
        {
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Все поля обязательны");

            if (await _db.Users.AnyAsync(u => u.Login == login))
                throw new InvalidOperationException("Логин уже занят");

            if (!await _db.Roles.AnyAsync(r => r.Id == roleId))
                throw new InvalidOperationException("Роль не найдена");

            var user = new User
            {
                Login = login,
                FullName = fullName,
                PasswordHash = _hasher.Hash(password),
                RoleId = roleId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return user;
        }
    }
}