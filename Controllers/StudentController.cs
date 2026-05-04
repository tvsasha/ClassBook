using ClassBook.Domain.Entities;
using ClassBook.Infrastructure.Data;
using ClassBook.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/student")]
    public class StudentController : ApiControllerBase
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
                    return NotFoundError("Карточка ученика не привязана к учетной записи");

                var schedule = await _db.Lessons
                    .Where(l => l.ClassId == student.ClassId)
                    .Include(l => l.Subject)
                    .Include(l => l.Teacher)
                    .Include(l => l.Schedule)
                    .OrderBy(l => l.Date)
                    .ThenBy(l => l.Schedule != null ? l.Schedule.LessonNumber : int.MaxValue)
                    .Select(l => new PortalScheduleEntryDto
                    {
                        LessonId = l.LessonId,
                        Subject = l.Subject.Name,
                        Teacher = l.Teacher.FullName,
                        Date = l.Date,
                        Topic = l.Topic,
                        Homework = l.Homework,
                        ScheduleId = l.ScheduleId,
                        LessonNumber = l.Schedule != null ? l.Schedule.LessonNumber : (int?)null,
                        StartTime = l.Schedule != null ? l.Schedule.StartTime.ToString(@"hh\:mm") : null,
                        EndTime = l.Schedule != null ? l.Schedule.EndTime.ToString(@"hh\:mm") : null
                    })
                    .ToListAsync();

                return Ok(schedule);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StudentController.GetMySchedule] Exception: {ex.Message}");
                Console.WriteLine($"[StudentController.GetMySchedule] StackTrace: {ex.StackTrace}");
                return InternalServerError("Не удалось загрузить расписание ученика");
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
                    return NotFoundError("Карточка ученика не привязана к учетной записи");

                var grades = await _db.Grades
                    .Where(g => g.StudentId == student.StudentId)
                    .Include(g => g.Lesson)
                    .ThenInclude(l => l.Subject)
                    .ThenInclude(s => s.Teacher)
                    .OrderByDescending(g => g.Lesson.Date)
                    .Select(g => new PortalGradeEntryDto
                    {
                        GradeId = g.GradeId,
                        Subject = g.Lesson.Subject.Name,
                        Teacher = g.Lesson.Subject.Teacher.FullName,
                        Value = g.Value,
                        Date = g.Lesson.Date,
                        Topic = g.Lesson.Topic
                    })
                    .ToListAsync();

                return Ok(grades);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StudentController.GetMyGrades] Exception: {ex.Message}");
                Console.WriteLine($"[StudentController.GetMyGrades] StackTrace: {ex.StackTrace}");
                return InternalServerError("Не удалось загрузить оценки ученика");
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
                    return NotFoundError("Карточка ученика не привязана к учетной записи");

                var homework = await _db.Lessons
                    .Where(l => l.ClassId == student.ClassId && !string.IsNullOrEmpty(l.Homework))
                    .Include(l => l.Subject)
                    .Include(l => l.Teacher)
                    .OrderByDescending(l => l.Date)
                    .Select(l => new PortalHomeworkEntryDto
                    {
                        LessonId = l.LessonId,
                        Subject = l.Subject.Name,
                        Teacher = l.Teacher.FullName,
                        Date = l.Date,
                        Topic = l.Topic,
                        Homework = l.Homework!
                    })
                    .ToListAsync();

                return Ok(homework);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StudentController.GetMyHomework] Exception: {ex.Message}");
                Console.WriteLine($"[StudentController.GetMyHomework] StackTrace: {ex.StackTrace}");
                return InternalServerError("Не удалось загрузить домашние задания ученика");
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
                    return NotFoundError("Карточка ученика не привязана к учетной записи");

                var attendance = await _db.Attendances
                    .Where(a => a.StudentId == student.StudentId)
                    .Include(a => a.Lesson)
                    .ThenInclude(l => l.Subject)
                    .OrderByDescending(a => a.Lesson.Date)
                    .Select(a => new PortalAttendanceEntryDto
                    {
                        LessonId = a.LessonId,
                        AttendanceId = a.AttendanceId,
                        Subject = a.Lesson.Subject.Name,
                        Status = a.Status,
                        StatusLabel = a.Status == 1 ? "Присутствовал" : (a.Status == 0 ? "Отсутствовал" : "Отсутствовал по уважительной причине"),
                        Date = a.Lesson.Date,
                        Topic = a.Lesson.Topic
                    })
                    .ToListAsync();

                return Ok(attendance);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StudentController.GetMyAttendance] Exception: {ex.Message}");
                Console.WriteLine($"[StudentController.GetMyAttendance] StackTrace: {ex.StackTrace}");
                return InternalServerError("Не удалось загрузить посещаемость ученика");
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
                    return NotFoundError("Карточка ученика не привязана к учетной записи");

                return Ok(new PortalStudentInfoDto
                {
                    StudentId = student.StudentId,
                    FirstName = student.FirstName,
                    LastName = student.LastName,
                    BirthDate = student.BirthDate,
                    Class = new PortalClassDto
                    {
                        Name = student.Class.Name
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StudentController.GetMyClassInfo] Exception: {ex.Message}");
                Console.WriteLine($"[StudentController.GetMyClassInfo] StackTrace: {ex.StackTrace}");
                return InternalServerError("Не удалось загрузить информацию об ученике");
            }
        }
    }
}
