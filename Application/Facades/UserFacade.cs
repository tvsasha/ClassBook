// Application/Facades/UserFacade.cs
using ClassBook.Application.DTOs;
using ClassBook.Domain.Interfaces;
using ClassBook.Domain.Constants;
using ClassBook.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

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
                .OrderBy(u => u.FullName)
                .Select(u => new UserListItemDto
                {
                    Id = u.Id,
                    Login = u.Login,
                    FullName = u.FullName,
                    RoleId = u.RoleId,
                    RoleName = u.Role.Name,
                    IsActive = u.IsActive,
                    MustChangePassword = u.MustChangePassword,
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
                MustChangePassword = user.MustChangePassword,
                CreatedAt = user.CreatedAt
            };
        }

        /// <summary>
        /// Создаёт пользователя общего административного потока.
        /// </summary>
        public async Task<UserCreateResultDto> CreateUserAsync(CreateUserDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Login) ||
                string.IsNullOrWhiteSpace(dto.Password) ||
                string.IsNullOrWhiteSpace(dto.FullName))
            {
                throw new ArgumentException("Логин, пароль и ФИО обязательны");
            }

            if (await _db.Users.AnyAsync(u => u.Login == dto.Login))
                throw new InvalidOperationException("Логин уже занят");

            if (dto.RoleId < 1 || dto.RoleId > 6)
                throw new ArgumentException("Недопустимая роль");

            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == dto.RoleId);
            if (role == null)
                throw new InvalidOperationException("Роль не найдена");

            if (role.Name == "Родитель")
                throw new InvalidOperationException("Новые учетные записи родителей создавайте только из карточки конкретного ученика");

            if (role.Name == "Ученик")
                throw new InvalidOperationException("Новые учетные записи учеников создавайте только из карточки конкретного ученика");

            var user = new Domain.Entities.User
            {
                Login = dto.Login.Trim(),
                FullName = dto.FullName.Trim(),
                PasswordHash = _hasher.Hash(dto.Password),
                RoleId = role.Id,
                IsActive = true,
                MustChangePassword = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return new UserCreateResultDto
            {
                User = await GetRequiredUserDtoAsync(user.Id),
                AuditValues = BuildUserAuditDto(user)
            };
        }

        /// <summary>
        /// Обновляет пользователя.
        /// </summary>
        public async Task<UserUpdateResultDto> UpdateUserAsync(int id, UpdateUserDto dto)
        {
            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                throw new KeyNotFoundException("Пользователь не найден");

            var currentRoleName = user.Role?.Name ?? string.Empty;
            var oldValues = BuildUserAuditDto(user);

            if (!string.IsNullOrWhiteSpace(dto.Login) && dto.Login != user.Login)
            {
                if (await _db.Users.AnyAsync(u => u.Login == dto.Login))
                    throw new InvalidOperationException("Логин уже занят");
                user.Login = dto.Login.Trim();
            }

            if (!string.IsNullOrWhiteSpace(dto.FullName))
                user.FullName = dto.FullName.Trim();

            if (dto.RoleId.HasValue && dto.RoleId != user.RoleId)
            {
                if (dto.RoleId < 1 || dto.RoleId > 6)
                    throw new ArgumentException("Недопустимая роль");

                var targetRole = await _db.Roles.FirstOrDefaultAsync(r => r.Id == dto.RoleId.Value);
                if (targetRole == null)
                    throw new InvalidOperationException("Роль не найдена");

                var targetRoleName = targetRole.Name;
                var touchesStrictRole =
                    currentRoleName == "Родитель" ||
                    currentRoleName == "Ученик" ||
                    targetRoleName == "Родитель" ||
                    targetRoleName == "Ученик";

                if (touchesStrictRole)
                {
                    throw new InvalidOperationException("Менять роль на 'Родитель' или 'Ученик' и обратно через общую форму пользователей нельзя. Используйте карточку ученика и специализированные сценарии выдачи доступа.");
                }

                user.RoleId = targetRole.Id;
                user.Role = targetRole;
            }

            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                user.PasswordHash = _hasher.Hash(dto.Password);
                user.MustChangePassword = true;
            }

            if (dto.IsActive.HasValue)
                user.IsActive = dto.IsActive.Value;

            await _db.SaveChangesAsync();

            return new UserUpdateResultDto
            {
                User = await GetRequiredUserDtoAsync(user.Id),
                OldValues = oldValues,
                NewValues = BuildUserAuditDto(user, !string.IsNullOrWhiteSpace(dto.Password))
            };
        }

        public async Task<IssuedAccessDto> ResetTemporaryPasswordAsync(int id)
        {
            var user = await _db.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                throw new KeyNotFoundException("Пользователь не найден");

            var temporaryPassword = GenerateTemporaryPassword();
            user.PasswordHash = _hasher.Hash(temporaryPassword);
            user.MustChangePassword = true;
            user.IsActive = true;
            await _db.SaveChangesAsync();

            return new IssuedAccessDto
            {
                Id = user.Id,
                Login = user.Login,
                FullName = user.FullName,
                TemporaryPassword = temporaryPassword,
                MustChangePassword = user.MustChangePassword,
                Message = "Временный пароль создан. Он показывается только сейчас."
            };
        }

        public async Task<string> GenerateUniqueLoginAsync(string fullName)
        {
            var baseLogin = Transliterate(fullName)
                .ToLowerInvariant()
                .Replace(" ", ".");
            baseLogin = Regex.Replace(baseLogin, @"[^a-z0-9\.]", string.Empty).Trim('.');
            if (string.IsNullOrWhiteSpace(baseLogin))
                baseLogin = "user";

            var login = baseLogin;
            var index = 1;
            while (await _db.Users.AnyAsync(u => u.Login == login))
            {
                index++;
                login = $"{baseLogin}{index}";
            }

            return login;
        }

        public static string GenerateTemporaryPassword()
        {
            Span<byte> bytes = stackalloc byte[6];
            RandomNumberGenerator.Fill(bytes);
            return $"Cb-{Convert.ToHexString(bytes)}!";
        }

        /// <summary>
        /// Возвращает учеников выбранного родителя.
        /// </summary>
        public async Task<List<ParentStudentListItemDto>> GetParentStudentsAsync(int parentId)
        {
            var parent = await _db.Users
                .Include(u => u.Role)
                .Include(u => u.StudentParents!)
                .ThenInclude(sp => sp.Student)
                .ThenInclude(s => s.Class)
                .FirstOrDefaultAsync(u => u.Id == parentId);

            if (parent == null)
                throw new KeyNotFoundException("Родитель не найден");

            if (parent.Role.Name != "Родитель")
                throw new InvalidOperationException("Пользователь не является родителем");

            return (parent.StudentParents ?? [])
                .Select(sp => new ParentStudentListItemDto
                {
                    StudentId = sp.Student.StudentId,
                    FirstName = sp.Student.FirstName,
                    LastName = sp.Student.LastName,
                    BirthDate = sp.Student.BirthDate,
                    ClassId = sp.Student.ClassId,
                    ClassName = sp.Student.Class?.Name ?? "не определен"
                })
                .ToList();
        }

        /// <summary>
         /// Удаляет пользователя.
        /// </summary>
        public async Task<UserDeleteResultDto> DeleteUserAsync(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                throw new KeyNotFoundException("Пользователь не найден");

            if (await _db.Subjects.AnyAsync(s => s.TeacherId == id) ||
                await _db.Lessons.AnyAsync(l => l.TeacherId == id))
            {
                throw new InvalidOperationException("Нельзя удалить учителя с привязанными предметами или уроками");
            }

            var oldValues = BuildUserAuditDto(user);

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();

            return new UserDeleteResultDto
            {
                UserId = id,
                OldValues = oldValues
            };
        }

        private async Task<UserListItemDto> GetRequiredUserDtoAsync(int id)
        {
            var user = await GetUserByIdAsync(id);
            if (user == null)
                throw new KeyNotFoundException("Пользователь не найден");

            return user;
        }

        private static UserMutationAuditDto BuildUserAuditDto(Domain.Entities.User user, bool passwordReset = false)
        {
            return new UserMutationAuditDto
            {
                Login = user.Login,
                FullName = user.FullName,
                RoleId = user.RoleId,
                IsActive = user.IsActive,
                MustChangePassword = user.MustChangePassword,
                PasswordReset = passwordReset
            };
        }

        private static string Transliterate(string value)
        {
            var map = new Dictionary<char, string>
            {
                ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "e",
                ['ж'] = "zh", ['з'] = "z", ['и'] = "i", ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m",
                ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u",
                ['ф'] = "f", ['х'] = "h", ['ц'] = "c", ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "sch", ['ъ'] = "",
                ['ы'] = "y", ['ь'] = "", ['э'] = "e", ['ю'] = "yu", ['я'] = "ya"
            };
            var builder = new System.Text.StringBuilder();
            foreach (var symbol in value.ToLowerInvariant())
            {
                builder.Append(map.TryGetValue(symbol, out var replacement) ? replacement : symbol);
            }

            return builder.ToString();
        }
    }
}
