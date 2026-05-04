using ClassBook.Application.Facades;
using Microsoft.AspNetCore.Mvc;

namespace ClassBook.Controllers
{
    /// <summary>
    /// Контроллер для получения списка ролей пользователей.
    /// </summary>
    [ApiController]
    [Route("api/roles")]
    public class RolesController : ControllerBase
    {
        private readonly RoleFacade _roleFacade;

        public RolesController(RoleFacade roleFacade)
        {
            _roleFacade = roleFacade;
        }

        /// <summary>
        /// Получить список всех ролей.
        /// </summary>
        /// <returns>Список ролей.</returns>
        [HttpGet]
        public async Task<IActionResult> GetRoles()
        {
            return Ok(await _roleFacade.GetRolesAsync());
        }
    }
}
