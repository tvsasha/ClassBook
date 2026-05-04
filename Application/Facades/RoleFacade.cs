using ClassBook.Application.DTOs;
using ClassBook.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ClassBook.Application.Facades
{
    public class RoleFacade
    {
        private readonly AppDbContext _db;

        public RoleFacade(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<RoleListItemDto>> GetRolesAsync()
        {
            return await _db.Roles
                .OrderBy(r => r.Id)
                .Select(r => new RoleListItemDto
                {
                    Id = r.Id,
                    Name = r.Name
                })
                .ToListAsync();
        }
    }
}
