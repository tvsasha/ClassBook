using ClassBook.Application.Facades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClassBook.Infrastructure.Data;
using ClassBook.Domain.Entities;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/subjects")]
    [Authorize(Policy = "AdminOnly")]
    public class SubjectsController : ApiControllerBase
    {
        private readonly AppDbContext _db;
        private readonly SubjectFacade _subjectFacade;

        public SubjectsController(AppDbContext db, SubjectFacade subjectFacade)
        {
            _db = db;
            _subjectFacade = subjectFacade;
        }

        /// <summary>
        /// Получает классы, в которых преподаётся предмет (с учителем).
        /// </summary>
        [HttpGet("{subjectId}/classes")]
        public async Task<IActionResult> GetClassesForSubject(int subjectId)
        {
            try
            {
                var classes = await _subjectFacade.GetClassesForSubjectAsync(subjectId);
                return Ok(classes);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
        }

        /// <summary>
        /// Получает все предметы.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var subjects = await _db.Subjects
                .Include(s => s.Teacher)
                .Select(s => new
                {
                    s.SubjectId,
                    s.Name,
                    s.TeacherId,
                    TeacherName = s.Teacher != null ? s.Teacher.FullName : "Не назначен"
                })
                .ToListAsync();

            return Ok(subjects);
        }

        /// <summary>
        /// Создаёт новый предмет.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateSubject([FromBody] CreateSubjectDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequestError("Название предмета обязательно");

            var teacher = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == dto.TeacherId && u.RoleId == 2);

            if (teacher == null)
                return BadRequestError("Учитель не найден или это не учитель");

            var subject = await _subjectFacade.CreateSubjectAsync(dto.Name, dto.TeacherId);

            return Ok(new { subject.SubjectId, subject.Name, TeacherName = teacher.FullName });
        }

        /// <summary>
        /// Обновляет предмет.
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSubject(int id, [FromBody] CreateSubjectDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequestError("Название предмета обязательно");

            var subject = await _db.Subjects.FindAsync(id);
            if (subject == null)
                return NotFoundError("Предмет не найден");

            var teacher = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == dto.TeacherId && u.RoleId == 2);

            if (teacher == null)
                return BadRequestError("Учитель не найден или это не учитель");

            subject.Name = dto.Name;
            subject.TeacherId = dto.TeacherId;

            await _db.SaveChangesAsync();

            return Ok(new { subject.SubjectId, subject.Name, TeacherName = teacher.FullName });
        }

        /// <summary>
        /// Прикрепляет учителя к предмету.
        /// </summary>
        [HttpPost("{subjectId}/teachers")]
        public async Task<IActionResult> AttachTeacherToSubject(int subjectId, [FromBody] AttachTeacherDto dto)
        {
            try
            {
                await _subjectFacade.AttachTeacherToSubjectAsync(subjectId, dto.TeacherId);
                var teacher = await _db.Users.FindAsync(dto.TeacherId);
                return Ok(new { message = "Учитель успешно прикреплён", TeacherName = teacher?.FullName });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequestError(ex.Message);
            }
        }

        /// <summary>
        /// Удаляет предмет.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSubject(int id)
        {
            var subject = await _db.Subjects.FindAsync(id);
            if (subject == null)
                return NotFoundError("Предмет не найден");

            if (await _db.Lessons.AnyAsync(l => l.SubjectId == id))
                return BadRequestError("Нельзя удалить предмет, к которому привязаны уроки");

            _db.Subjects.Remove(subject);
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }

    public class CreateSubjectDto
    {
        public string Name { get; set; } = null!;
        public int TeacherId { get; set; }
    }

    public class AttachTeacherDto
    {
        public int TeacherId { get; set; }
    }
}
