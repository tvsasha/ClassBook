using ClassBook.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClassBook.Controllers
{
    /// <summary>
    /// Контроллер для получения списка ролей пользователей
    /// </summary>
    [ApiController]
    [Route("api/roles")]
    public class RolesController : ControllerBase
    {
        private readonly AppDbContext _db;

        /// <summary>
        /// Конструктор контроллера ролей
        /// </summary>
        /// <param name="db">Контекст базы данных</param>
        public RolesController(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Получить список всех ролей
        /// </summary>
        /// <returns>Список ролей (id, name)</returns>
        [HttpGet]
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _db.Roles
                .Select(r => new
                {
                    id = r.Id,
                    name = r.Name
                })
                .ToListAsync();

            return Ok(roles);
        }
    }
}
