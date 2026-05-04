using ClassBook.Domain.Entities;
using ClassBook.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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

        private async Task<Student?> GetCurrentStudentAsync(bool includeClass = false)
        {
            var query = _db.Students.AsQueryable();

            if (includeClass)
                query = query.Include(s => s.Class);

            return await query.FirstOrDefaultAsync(s => s.UserId == GetUserId());
        }

        [HttpGet("me/schedule")]
        [Authorize(Roles = "Ученик,Администратор")]
        public async Task<IActionResult> GetMySchedule()
        {
            try
            {
                var student = await GetCurrentStudentAsync();
                if (student == null)
                    return NotFound("Карточка ученика не привязана к учетной записи");

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

        [HttpGet("me/grades")]
        [Authorize(Roles = "Ученик,Администратор")]
        public async Task<IActionResult> GetMyGrades()
        {
            try
            {
                var student = await GetCurrentStudentAsync();
                if (student == null)
                    return NotFound("Карточка ученика не привязана к учетной записи");

                var grades = await _db.Grades
                    .Where(g => g.StudentId == student.StudentId)
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

        [HttpGet("me/homework")]
        [Authorize(Roles = "Ученик,Администратор")]
        public async Task<IActionResult> GetMyHomework()
        {
            try
            {
                var student = await GetCurrentStudentAsync();
                if (student == null)
                    return NotFound("Карточка ученика не привязана к учетной записи");

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

        [HttpGet("me/attendance")]
        [Authorize(Roles = "Ученик,Администратор")]
        public async Task<IActionResult> GetMyAttendance()
        {
            try
            {
                var student = await GetCurrentStudentAsync();
                if (student == null)
                    return NotFound("Карточка ученика не привязана к учетной записи");

                var attendance = await _db.Attendances
                    .Where(a => a.StudentId == student.StudentId)
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

        [HttpGet("me/class")]
        [Authorize(Roles = "Ученик,Администратор")]
        public async Task<IActionResult> GetMyClassInfo()
        {
            try
            {
                var student = await GetCurrentStudentAsync(includeClass: true);
                if (student == null)
                    return NotFound("Карточка ученика не привязана к учетной записи");

                return Ok(new
                {
                    student.StudentId,
                    Name = $"{student.FirstName} {student.LastName}",
                    Class = student.Class.Name
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
