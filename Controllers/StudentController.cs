using ClassBook.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/student")]
    public class StudentController : ControllerBase
    {
        private readonly AppDbContext _db;

        public StudentController(AppDbContext db)
        {
            _db = db;
        }

        private int GetUserId()
        {
            return int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) ? userId : 0;
        }

        /// <summary>
        /// Получить моё расписание (для ученика, привязанного к User)
        /// </summary>
        [HttpGet("me/schedule")]
        [Authorize(Roles = "Ученик,Администратор")]
        public async Task<IActionResult> GetMySchedule()
        {
            try
            {
                // Получить студента по User ID (если ученик зарегистрирован как Student)
                var student = await _db.Students.FirstOrDefaultAsync(s => s.StudentId == GetUserId());
                if (student == null)
                    return NotFound("Ученик не найден");

                var schedule = await _db.Lessons
                    .Where(l => l.ClassId == student.ClassId)
                    .Include(l => l.Subject)
                    .Include(l => l.Teacher)
                    .Include(l => l.Schedule)
                    .OrderBy(l => l.Date)
                    .ThenBy(l => l.Schedule != null ? l.Schedule.LessonNumber : int.MaxValue)
                    .Select(l => new
                    {
                        l.LessonId,
                        Subject = l.Subject.Name,
                        Teacher = l.Teacher.FullName,
                        l.Date,
                        l.Topic,
                        l.Homework,
                        l.ScheduleId,
                        LessonNumber = l.Schedule != null ? l.Schedule.LessonNumber : (int?)null,
                        StartTime = l.Schedule != null ? l.Schedule.StartTime.ToString(@"hh\:mm") : null,
                        EndTime = l.Schedule != null ? l.Schedule.EndTime.ToString(@"hh\:mm") : null
                    })
                    .ToListAsync();

                return Ok(schedule);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Получить мои оценки
        /// </summary>
        [HttpGet("me/grades")]
        [Authorize(Roles = "Ученик,Администратор")]
        public async Task<IActionResult> GetMyGrades()
        {
            try
            {
                var userId = GetUserId();

                // Здесь можно доставить grades для конкретного ученика
                // Предполагаем, что StudentId может совпадать с UserId для простоты
                var grades = await _db.Grades
                    .Where(g => g.StudentId == userId)
                    .Include(g => g.Lesson)
                    .ThenInclude(l => l.Subject)
                    .ThenInclude(s => s.Teacher)
                    .OrderByDescending(g => g.Lesson.Date)
                    .Select(g => new
                    {
                        g.GradeId,
                        Subject = g.Lesson.Subject.Name,
                        Teacher = g.Lesson.Subject.Teacher.FullName,
                        g.Value,
                        Date = g.Lesson.Date,
                        Topic = g.Lesson.Topic
                    })
                    .ToListAsync();

                return Ok(grades);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Получить мои домашние задания
        /// </summary>
        [HttpGet("me/homework")]
        [Authorize(Roles = "Ученик,Администратор")]
        public async Task<IActionResult> GetMyHomework()
        {
            try
            {
                var student = await _db.Students.FirstOrDefaultAsync(s => s.StudentId == GetUserId());
                if (student == null)
                    return NotFound("Ученик не найден");

                var homework = await _db.Lessons
                    .Where(l => l.ClassId == student.ClassId && !string.IsNullOrEmpty(l.Homework))
                    .Include(l => l.Subject)
                    .Include(l => l.Teacher)
                    .OrderByDescending(l => l.Date)
                    .Select(l => new
                    {
                        l.LessonId,
                        Subject = l.Subject.Name,
                        Teacher = l.Teacher.FullName,
                        l.Date,
                        l.Topic,
                        l.Homework
                    })
                    .ToListAsync();

                return Ok(homework);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Получить мою посещаемость
        /// </summary>
        [HttpGet("me/attendance")]
        [Authorize(Roles = "Ученик,Администратор")]
        public async Task<IActionResult> GetMyAttendance()
        {
            try
            {
                var userId = GetUserId();

                var attendance = await _db.Attendances
                    .Where(a => a.StudentId == userId)
                    .Include(a => a.Lesson)
                    .ThenInclude(l => l.Subject)
                    .OrderByDescending(a => a.Lesson.Date)
                    .Select(a => new
                    {
                        a.AttendanceId,
                        Subject = a.Lesson.Subject.Name,
                        a.Status,
                        StatusLabel = a.Status == 1 ? "Присутствовал" : (a.Status == 0 ? "Отсутствовал" : "Отсутствовал по уважительной причине"),
                        Date = a.Lesson.Date,
                        Topic = a.Lesson.Topic
                    })
                    .ToListAsync();

                return Ok(attendance);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Получить информацию о моём классе
        /// </summary>
        [HttpGet("me/class")]
        [Authorize(Roles = "Ученик,Администратор")]
        public async Task<IActionResult> GetMyClassInfo()
        {
            try
            {
                var student = await _db.Students
                    .Where(s => s.StudentId == GetUserId())
                    .Include(s => s.Class)
                    .FirstOrDefaultAsync();

                if (student == null)
                    return NotFound("Ученик не найден");

                return Ok(new { student.StudentId, Name = $"{student.FirstName} {student.LastName}", Class = student.Class.Name });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
