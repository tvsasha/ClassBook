using ClassBook.Application.Facades;
using ClassBook.Domain.Entities;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ClassBook.Controllers
{
    /// <summary>
    /// Контроллер аутентификации пользователей системы ClassBook.
    /// Отвечает за вход в систему (авторизацию).
    /// Регистрация пользователей осуществляется только администратором через отдельный контроллер.
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthFacade _authFacade;

        /// <summary>
        /// Инициализирует новый экземпляр <see cref="AuthController"/>.
        /// </summary>
        /// <param name="authFacade">Фасад для операций аутентификации</param>
        public AuthController(AuthFacade authFacade)
        {
            _authFacade = authFacade ?? throw new ArgumentNullException(nameof(authFacade));
        }

        /// <summary>
        /// Выполняет аутентификацию пользователя в системе.
        /// </summary>
        /// <param name="dto">Данные для входа (логин и пароль)</param>
        /// <returns>Данные пользователя при успешной авторизации или ошибку</returns>
        /// <response code="200">Успешный вход</response>
        /// <response code="400">Некорректные данные</response>
        /// <response code="401">Неверный логин или пароль</response>
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

            // Устанавливаем куки
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true, // сохранять после закрытия браузера
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                });

            return Ok(new { user.Id, user.Login, role = user.Role.Name });
        }

        /// <summary>
        /// Формирует объект ответа при успешной авторизации.
        /// </summary>
        /// <param name="user">Авторизованный пользователь</param>
        /// <returns>Анонимный объект с данными пользователя</returns>
        private static object BuildLoginResponse(User user)
        {
            return new
            {
                user.Id,
                user.Login,
                user.FullName,
                Role = user.Role.Name,
                user.IsActive,
                CreatedAt = user.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }
    }

    /// <summary>
    /// DTO для аутентификации пользователя
    /// </summary>
    public class LoginDto
    {
        /// <summary>
        /// Логин пользователя
        /// </summary>
        public string Login { get; set; } = null!;

        /// <summary>
        /// Пароль пользователя
        /// </summary>
        public string Password { get; set; } = null!;
    }
}