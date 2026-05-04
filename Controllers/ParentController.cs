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
    [Route("api/parent")]
    public class ParentController : ControllerBase
    {
        private readonly ParentFacade _parentFacade;
        private readonly LessonFacade _lessonFacade;
        private readonly GradeFacade _gradeFacade;
        private readonly AppDbContext _db;

        public ParentController(ParentFacade parentFacade, LessonFacade lessonFacade, GradeFacade gradeFacade, AppDbContext db)
        {
            _parentFacade = parentFacade;
            _lessonFacade = lessonFacade;
            _gradeFacade = gradeFacade;
            _db = db;
        }

        private int GetUserId()
        {
            return int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) ? userId : 0;
        }

        /// <summary>
        /// Получить всех учеников родителя
        /// </summary>
        [HttpGet("students")]
        [Authorize(Roles = "Родитель,Администратор")]
        public async Task<IActionResult> GetMyStudents()
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized();

            try
            {
                Console.WriteLine($"[ParentController.GetMyStudents] userId={userId}");
                var students = await _parentFacade.GetStudentsForParentAsync(userId);
                Console.WriteLine($"[ParentController.GetMyStudents] Found {students?.Count ?? 0} students");
                return Ok(students ?? new List<dynamic>());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ParentController.GetMyStudents] Exception: {ex.Message}");
                Console.WriteLine($"[ParentController.GetMyStudents] StackTrace: {ex.StackTrace}");
                return StatusCode(500, new { error = "Не удалось загрузить список учеников" });
            }
        }

        /// <summary>
        /// Получить расписание ученика
        /// </summary>
        [HttpGet("student/{studentId}/schedule")]
        [Authorize(Roles = "Родитель,Администратор")]
        public async Task<IActionResult> GetStudentSchedule(int studentId)
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized();

            try
            {
                // Проверяем, что родитель имеет доступ к этому ученику
                var isParent = await _parentFacade.IsParentOfStudentAsync(userId, studentId);
                if (!isParent && !User.IsInRole("Администратор"))
                {
                    return Forbid("У вас нет доступа к этому ученику");
                }

                var schedule = await _db.Lessons
                    .Where(l => l.Class.Students.Any(s => s.StudentId == studentId))
                    .Include(l => l.Subject)
                    .Include(l => l.Teacher)
                    .Include(l => l.Schedule)
                    .OrderBy(l => l.Date)
                    .ThenBy(l => l.Schedule != null ? l.Schedule.LessonNumber : int.MaxValue)
                    .Select(l => new
                    {
                        l.LessonId,
                        l.Subject.Name,
                        l.Teacher.FullName,
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
        /// Получить оценки ученика
        /// </summary>
        [HttpGet("student/{studentId}/grades")]
        [Authorize(Roles = "Родитель,Администратор")]
        public async Task<IActionResult> GetStudentGrades(int studentId)
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized();

            try
            {
                // Проверяем доступ
                var isParent = await _parentFacade.IsParentOfStudentAsync(userId, studentId);
                if (!isParent && !User.IsInRole("Администратор"))
                {
                    return Forbid("У вас нет доступа к этому ученику");
                }

                var grades = await _db.Grades
                    .Where(g => g.StudentId == studentId)
                    .Include(g => g.Lesson)
                    .ThenInclude(l => l.Subject)
                    .OrderBy(g => g.Lesson.Date)
                    .Select(g => new
                    {
                        g.GradeId,
                        Subject = g.Lesson.Subject.Name,
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
        /// Получить домашние задания ученика
        /// </summary>
        [HttpGet("student/{studentId}/homework")]
        [Authorize(Roles = "Родитель,Администратор")]
        public async Task<IActionResult> GetStudentHomework(int studentId)
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized();

            try
            {
                // Проверяем доступ
                var isParent = await _parentFacade.IsParentOfStudentAsync(userId, studentId);
                if (!isParent && !User.IsInRole("Администратор"))
                {
                    return Forbid("У вас нет доступа к этому ученику");
                }

                var homework = await _db.Lessons
                    .Where(l => l.Class.Students.Any(s => s.StudentId == studentId) && !string.IsNullOrEmpty(l.Homework))
                    .Include(l => l.Subject)
                    .Include(l => l.Teacher)
                    .OrderBy(l => l.Date)
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
        /// Получить посещаемость ученика (все уроки с момента добавления ученика в систему)
        /// </summary>
        [HttpGet("student/{studentId}/attendance")]
        [Authorize(Roles = "Родитель,Администратор")]
        public async Task<IActionResult> GetStudentAttendance(int studentId)
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized();

            try
            {
                // Проверяем доступ
                var isParent = await _parentFacade.IsParentOfStudentAsync(userId, studentId);
                if (!isParent && !User.IsInRole("Администратор"))
                {
                    return Forbid("У вас нет доступа к этому ученику");
                }

                Console.WriteLine($"[ParentController.GetStudentAttendance] studentId={studentId}");
                
                // Получаем дату добавления ученика в систему
                var studentAddedDate = await _db.StudentParents
                    .Where(sp => sp.StudentId == studentId)
                    .OrderBy(sp => sp.CreatedAt)
                    .Select(sp => sp.CreatedAt)
                    .FirstOrDefaultAsync();

                Console.WriteLine($"[ParentController.GetStudentAttendance] Student added date: {studentAddedDate}");

                // Получаем ученика и его класс
                var student = await _db.Students
                    .Include(s => s.Class)
                    .FirstOrDefaultAsync(s => s.StudentId == studentId);

                if (student == null)
                    return NotFound("Ученик не найден");

                // Получаем все уроки класса со времени добавления ученика
                var lessons = await _db.Lessons
                    .Where(l => l.ClassId == student.ClassId && l.Date >= studentAddedDate)
                    .Include(l => l.Subject)
                    .OrderBy(l => l.Date)
                    .ToListAsync();

                Console.WriteLine($"[ParentController.GetStudentAttendance] Found {lessons.Count} lessons since {studentAddedDate}");

                // Получаем посещаемость ученика
                var attendanceRecords = await _db.Attendances
                    .Where(a => a.StudentId == studentId)
                    .ToListAsync();

                // Объединяем: для каждого урока показываем посещаемость или "Не отмечено"
                var result = lessons.Select(lesson =>
                {
                    var attendance = attendanceRecords.FirstOrDefault(a => a.LessonId == lesson.LessonId);
                    
                    return new
                    {
                        LessonId = lesson.LessonId,
                        AttendanceId = attendance?.AttendanceId,
                        Subject = lesson.Subject.Name,
                        Status = attendance?.Status,
                        StatusLabel = attendance == null 
                            ? "Не отмечено" 
                            : (attendance.Status == 1 ? "Присутствовал" 
                                : (attendance.Status == 0 ? "Отсутствовал" 
                                    : (attendance.Status == 2 ? "Опоздание" 
                                        : "Отсутствовал по уважительной причине"))),
                        Date = lesson.Date,
                        Topic = lesson.Topic
                    };
                }).OrderByDescending(l => l.Date).ToList();

                Console.WriteLine($"[ParentController.GetStudentAttendance] Returning {result.Count} records");
                foreach (var r in result.Take(10))
                {
                    Console.WriteLine($"  - {r.Subject} ({r.StatusLabel}) {r.Date}");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ParentController.GetStudentAttendance] Exception: {ex.Message}");
                Console.WriteLine($"[ParentController.GetStudentAttendance] StackTrace: {ex.StackTrace}");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Добавить ученика к родителю (только для админа)
        /// </summary>
        [HttpPost("student/{studentId}")]
        [Authorize(Roles = "Администратор")]
        public async Task<IActionResult> AddParentToStudent(int studentId, [FromBody] AddParentRequest request)
        {
            try
            {
                var result = await _parentFacade.AddParentToStudentAsync(studentId, request.ParentId);
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
        }

        /// <summary>
        /// Получить родителей ученика (только для админа)
        /// </summary>
        [HttpGet("student/{studentId}/parents")]
        [Authorize(Roles = "Администратор")]
        public async Task<IActionResult> GetStudentParents(int studentId)
        {
            try
            {
                var parents = await _parentFacade.GetParentsForStudentAsync(studentId);
                return Ok(parents);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Удалить связь ученик-родитель (только для админа)
        /// </summary>
        [HttpDelete("student/{studentId}/parent/{parentId}")]
        [Authorize(Roles = "Администратор")]
        public async Task<IActionResult> RemoveParentFromStudent(int studentId, int parentId)
        {
            try
            {
                await _parentFacade.RemoveParentFromStudentAsync(studentId, parentId);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }
    }

    public class AddParentRequest
    {
        public int ParentId { get; set; }
    }
}
