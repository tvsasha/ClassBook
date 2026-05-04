using ClassBook.Domain.Entities;
using ClassBook.Infrastructure.Data;
using ClassBook.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/classes")]
    [Authorize(Policy = "AdminOnly")]
    public class ClassesController : ApiControllerBase
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
                .Select(c => new ClassListItemDto
                {
                    ClassId = c.ClassId,
                    Name = c.Name
                })
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

            return Ok(new ClassListItemDto
            {
                ClassId = classEntity.ClassId,
                Name = classEntity.Name
            });
        }

        // DELETE: api/classes/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteClass(int id)
        {
            var classEntity = await _db.Classes.FindAsync(id);
            if (classEntity == null)
                return NotFoundError("Класс не найден");

            // Проверка на связанные данные
            if (await _db.Students.AnyAsync(s => s.ClassId == id) ||
                await _db.Lessons.AnyAsync(l => l.ClassId == id))
            {
                return BadRequestError("Нельзя удалить класс с привязанными учениками или уроками");
            }

            _db.Classes.Remove(classEntity);
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }

    public class CreateClassDto
    {
        public string Name { get; set; } = null!; // например "10А"
    }
   
    
}
