using ClassBook.Domain.Entities;
using ClassBook.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/classes")]
    public class ClassesController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ClassesController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var classes = await _db.Classes
                .Select(c => new { c.ClassId, c.Name })
                .ToListAsync();
            return Ok(classes);
        }

        // POST: api/admin/classes
        [HttpPost]
        public async Task<IActionResult> CreateClass([FromBody] CreateClassDto dto)
        {
            var classEntity = new Class
            {
                Name = dto.Name
            };

            _db.Classes.Add(classEntity);
            await _db.SaveChangesAsync();

            return Ok(new { classEntity.ClassId, classEntity.Name });
        }
    }

    public class CreateClassDto
    {
        public string Name { get; set; } = null!; // например "10А"
    }
   
    
}
