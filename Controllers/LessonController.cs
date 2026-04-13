using ClassBook.Application.Facades;
using ClassBook.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/lessons")]
    public class LessonController : ControllerBase
    {
        private readonly LessonFacade _facade;
        private readonly AppDbContext _db;

        public LessonController(LessonFacade facade, AppDbContext db)
        {
            _facade = facade;
            _db = db;
        }

        // GET: api/lessons — все уроки (для админа)
        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetAll()
        {
            var lessons = await _facade.GetAllLessonsAsync();
            return Ok(lessons);
        }

        // POST: api/lessons — создание урока
        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> CreateLesson([FromBody] CreateLessonRequest dto)
        {
            try
            {
                var currentUserIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                int.TryParse(currentUserIdClaim, out var currentUserId);

                var lesson = await _facade.CreateLessonAsync(
                    dto.SubjectId,
                    dto.ClassId,
                    dto.TeacherId,
                    dto.Topic,
                    dto.Date,
                    dto.Homework,
                    currentUserId > 0 ? currentUserId : null
                );

                // Загружаем данные одним запросом
                var result = await _db.Lessons
                    .Include(l => l.Subject)
                    .Include(l => l.Class)
                    .Include(l => l.Teacher)
                    .Where(l => l.LessonId == lesson.LessonId)
                    .Select(l => new LessonResponse
                    {
                        LessonId = l.LessonId,
                        SubjectId = l.SubjectId,
                        SubjectName = l.Subject.Name,
                        ClassId = l.ClassId,
                        ClassName = l.Class.Name,
                        TeacherId = l.TeacherId,
                        TeacherName = l.Teacher.FullName,
                        Topic = l.Topic,
                        Date = l.Date,
                        Homework = l.Homework
                    })
                    .FirstOrDefaultAsync();

                return CreatedAtAction(nameof(GetAll), new { id = result.LessonId }, result);
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

        // PUT: api/lessons/{id} — обновление урока (админ/учитель)
        [HttpPut("{id}")]
        [Authorize(Roles = "Учитель,Администратор")]
        public async Task<IActionResult> UpdateLesson(int id, [FromBody] CreateLessonRequest dto)
        {
            try
            {
                var lesson = await _db.Lessons.FindAsync(id);
                if (lesson == null) return NotFound("Урок не найден");

                var currentUserIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(currentUserIdClaim, out var currentUserId))
                {
                    return Unauthorized("Не удалось определить пользователя");
                }

                if (User.IsInRole("Учитель") && lesson.TeacherId != currentUserId)
                {
                    return Forbid("Вы можете редактировать только свои уроки");
                }

                var updated = await _facade.UpdateLessonAsync(id, dto.SubjectId, dto.ClassId, dto.TeacherId, dto.Topic, dto.Date, dto.Homework, currentUserId);

                var result = await _db.Lessons
                    .Include(l => l.Subject)
                    .Include(l => l.Class)
                    .Include(l => l.Teacher)
                    .Where(l => l.LessonId == id)
                    .Select(l => new LessonResponse
                    {
                        LessonId = l.LessonId,
                        SubjectId = l.SubjectId,
                        SubjectName = l.Subject.Name,
                        ClassId = l.ClassId,
                        ClassName = l.Class.Name,
                        TeacherId = l.TeacherId,
                        TeacherName = l.Teacher.FullName,
                        Topic = l.Topic,
                        Date = l.Date,
                        Homework = l.Homework
                    })
                    .FirstOrDefaultAsync();

                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // DELETE: api/lessons/{lessonId} — удаление урока (Учитель/Админ)
        [HttpDelete("{lessonId}")]
        [Authorize(Roles = "Учитель,Администратор")]
        public async Task<IActionResult> DeleteLesson(int lessonId)
        {
            try
            {
                var lesson = await _facade.GetLessonByIdAsync(lessonId);
                if (lesson == null)
                    return NotFound("Урок не найден");

                var currentUserIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(currentUserIdClaim, out var currentUserId))
                {
                    return Unauthorized("Не удалось определить пользователя");
                }

                if (User.IsInRole("Учитель") && lesson.TeacherId != currentUserId)
                    return Forbid("Вы можете удалять только свои уроки");

                await _facade.DeleteLessonAsync(lessonId, currentUserId);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }
    }

    // DTO для создания/обновления урока
    public class CreateLessonRequest
    {
        public int SubjectId { get; set; }
        public int ClassId { get; set; }
        public int TeacherId { get; set; }
        public string Topic { get; set; } = null!;
        public DateTime Date { get; set; }
        public string? Homework { get; set; }
    }

    // DTO для ответа (без циклов)
    public class LessonResponse
    {
        public int LessonId { get; set; }
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = null!;
        public int ClassId { get; set; }
        public string ClassName { get; set; } = null!;
        public int TeacherId { get; set; }
        public string TeacherName { get; set; } = null!;
        public string Topic { get; set; } = null!;
        public DateTime Date { get; set; }
        public string? Homework { get; set; }
    }
}