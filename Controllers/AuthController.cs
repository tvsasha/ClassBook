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
    public class AuthController : ControllerBase
    {
        private readonly AuthFacade _authFacade;

        public AuthController(AuthFacade authFacade)
        {
            _authFacade = authFacade ?? throw new ArgumentNullException(nameof(authFacade));
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _authFacade.LoginAsync(dto.Login, dto.Password);
            if (user == null)
                return Unauthorized("Неверный логин или пароль");

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

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok(new { message = "Выход выполнен" });
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized("Не удалось определить пользователя");

            try
            {
                var user = await _authFacade.ChangePasswordAsync(userId, dto.CurrentPassword, dto.NewPassword);
                return Ok(BuildLoginResponse(user));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        private static object BuildLoginResponse(User user)
        {
            return new
            {
                user.Id,
                user.Login,
                user.FullName,
                role = user.Role.Name,
                user.IsActive,
                user.MustChangePassword,
                createdAt = user.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }
    }

    public class LoginDto
    {
        public string Login { get; set; } = null!;
        public string Password { get; set; } = null!;
    }

    public class ChangePasswordDto
    {
        public string CurrentPassword { get; set; } = null!;
        public string NewPassword { get; set; } = null!;
    }
}
