using ClassBook.Application.DTOs;
using ClassBook.Application.Facades;
using ClassBook.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using System.Security.Cryptography;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ApiControllerBase
    {
        private const int MaxFailedLoginAttempts = 8;
        private const string CsrfCookieName = "ClassBook.Csrf";
        private static readonly TimeSpan LoginAttemptWindow = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan LoginLockoutWindow = TimeSpan.FromMinutes(5);

        private readonly AuthFacade _authFacade;
        private readonly IMemoryCache _cache;

        public AuthController(AuthFacade authFacade, IMemoryCache cache)
        {
            _authFacade = authFacade ?? throw new ArgumentNullException(nameof(authFacade));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var loginKey = BuildLoginAttemptKey(dto.Login);
            if (IsLoginTemporarilyBlocked(loginKey, out var retryAfter))
            {
                Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                return StatusCode(StatusCodes.Status429TooManyRequests, new ApiErrorResponse(
                    "Слишком много неудачных попыток входа. Подождите несколько минут и попробуйте снова.",
                    "login_rate_limited"));
            }

            var user = await _authFacade.LoginAsync(dto.Login, dto.Password);
            if (user == null)
            {
                RegisterFailedLogin(loginKey);
                return UnauthorizedError("Не получилось войти. Проверьте логин и пароль.");
            }

            _cache.Remove(loginKey);
            SetCsrfCookie();

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Login),
                new(ClaimTypes.Role, user.Role.Name)
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

        [AllowAnonymous]
        [HttpGet("csrf")]
        public IActionResult Csrf()
        {
            var token = SetCsrfCookie();
            return Ok(new { token });
        }

        [Authorize]
        [HttpPost("heartbeat")]
        public async Task<IActionResult> Heartbeat()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return UnauthorizedError("Не удалось определить пользователя");

            await _authFacade.MarkOnlineAsync(userId);
            return NoContent();
        }

        [Authorize]
        [HttpPost("offline")]
        public async Task<IActionResult> Offline()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return UnauthorizedError("Не удалось определить пользователя");

            await _authFacade.MarkOfflineAsync(userId);
            return NoContent();
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                await _authFacade.MarkOfflineAsync(userId);
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            Response.Cookies.Delete(CsrfCookieName, new CookieOptions { Path = "/" });
            return Ok(new MessageResponseDto { Message = "Выход выполнен" });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return UnauthorizedError("Не удалось определить пользователя");

            var user = await _authFacade.GetUserByIdAsync(userId);
            if (user == null)
                return UnauthorizedError("Пользователь не найден или отключен");

            SetCsrfCookie();
            return Ok(BuildLoginResponse(user));
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return UnauthorizedError("Не удалось определить пользователя");

            try
            {
                var user = await _authFacade.ChangePasswordAsync(userId, dto.CurrentPassword, dto.NewPassword);
                SetCsrfCookie();
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

        private string BuildLoginAttemptKey(string login)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return $"login-attempt:{ip}:{(login ?? string.Empty).Trim().ToLowerInvariant()}";
        }

        private bool IsLoginTemporarilyBlocked(string key, out TimeSpan retryAfter)
        {
            retryAfter = TimeSpan.Zero;
            if (!_cache.TryGetValue<LoginAttemptState>(key, out var state) || state == null || state.LockedUntilUtc == null)
                return false;

            var remaining = state.LockedUntilUtc.Value - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                _cache.Remove(key);
                return false;
            }

            retryAfter = remaining;
            return true;
        }

        private void RegisterFailedLogin(string key)
        {
            var state = _cache.Get<LoginAttemptState>(key) ?? new LoginAttemptState();
            state.Count++;
            state.FirstFailedAtUtc ??= DateTimeOffset.UtcNow;

            if (state.Count >= MaxFailedLoginAttempts)
                state.LockedUntilUtc = DateTimeOffset.UtcNow.Add(LoginLockoutWindow);

            _cache.Set(key, state, LoginAttemptWindow);
        }

        private string SetCsrfCookie()
        {
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            Response.Cookies.Append(CsrfCookieName, token, new CookieOptions
            {
                HttpOnly = false,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/"
            });
            return token;
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
                CreatedAt = user.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                LastSeenAt = user.LastSeenAt?.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

        private sealed class LoginAttemptState
        {
            public int Count { get; set; }
            public DateTimeOffset? FirstFailedAtUtc { get; set; }
            public DateTimeOffset? LockedUntilUtc { get; set; }
        }
    }
}
