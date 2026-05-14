using ClassBook.Application.DTOs;
using ClassBook.Application.Facades;
using ClassBook.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ClassBook.Controllers
{
    /// <summary>
    /// Контроллер аутентификации пользователей системы ClassBook.
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ApiControllerBase
    {
        private readonly AuthFacade _authFacade;

        public AuthController(AuthFacade authFacade)
        {
            _authFacade = authFacade ?? throw new ArgumentNullException(nameof(authFacade));
        }

        /// <summary>
        /// Выполняет вход пользователя в систему и создаёт аутентифицированную cookie-сессию.
        /// </summary>
        /// <param name="dto">Логин и пароль пользователя.</param>
        /// <returns>Данные текущего пользователя и его роль для последующего редиректа в интерфейсе.</returns>
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _authFacade.LoginAsync(dto.Login, dto.Password);
            if (user == null)
                return UnauthorizedError("Не получилось войти. Проверьте логин и пароль.");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Login),
                new Claim(ClaimTypes.Role, user.Role.Name)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                });

            return Ok(BuildLoginResponse(user));
        }

        /// <summary>
        /// Завершает текущую пользовательскую сессию и удаляет серверную cookie-аутентификацию.
        /// </summary>
        /// <returns>Подтверждение успешного выхода.</returns>
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok(new MessageResponseDto { Message = "Выход выполнен" });
        }

        /// <summary>
        /// Возвращает данные текущего пользователя по активной cookie-сессии.
        /// </summary>
        /// <returns>Данные авторизованного пользователя.</returns>
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return UnauthorizedError("Не удалось определить пользователя");

            var user = await _authFacade.GetUserByIdAsync(userId);
            if (user == null)
                return UnauthorizedError("Пользователь не найден или отключён");

            return Ok(BuildLoginResponse(user));
        }

        /// <summary>
        /// Меняет пароль текущего пользователя и снимает флаг обязательной смены временного пароля.
        /// </summary>
        /// <param name="dto">Текущий и новый пароль пользователя.</param>
        /// <returns>Обновлённые данные пользователя после успешной смены пароля.</returns>
        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return UnauthorizedError("Не удалось определить пользователя");

            try
            {
                var user = await _authFacade.ChangePasswordAsync(userId, dto.CurrentPassword, dto.NewPassword);
                return Ok(BuildLoginResponse(user));
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

        private static AuthUserDto BuildLoginResponse(User user)
        {
            return new AuthUserDto
            {
                Id = user.Id,
                Login = user.Login,
                FullName = user.FullName,
                Role = user.Role.Name,
                IsActive = user.IsActive,
                MustChangePassword = user.MustChangePassword,
                CreatedAt = user.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }
    }

}
