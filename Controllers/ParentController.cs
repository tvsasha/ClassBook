using ClassBook.Application.Facades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/parent")]
    public class ParentController : ControllerBase
    {
        private readonly ParentFacade _parentFacade;

        public ParentController(ParentFacade parentFacade)
        {
            _parentFacade = parentFacade;
        }

        private int GetUserId()
        {
            return int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) ? userId : 0;
        }

        private static IActionResult InternalServerError(string message)
        {
            return new ObjectResult(new { error = message })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
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
                return InternalServerError("Не удалось загрузить список учеников");
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
                var schedule = await _parentFacade.GetStudentScheduleAsync(studentId);

                return Ok(schedule);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ParentController.GetStudentSchedule] Exception: {ex.Message}");
                Console.WriteLine($"[ParentController.GetStudentSchedule] StackTrace: {ex.StackTrace}");
                return InternalServerError("Не удалось загрузить расписание ученика");
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
                var grades = await _parentFacade.GetStudentGradesAsync(studentId);

                return Ok(grades);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ParentController.GetStudentGrades] Exception: {ex.Message}");
                Console.WriteLine($"[ParentController.GetStudentGrades] StackTrace: {ex.StackTrace}");
                return InternalServerError("Не удалось загрузить оценки ученика");
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
                var homework = await _parentFacade.GetStudentHomeworkAsync(studentId);

                return Ok(homework);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ParentController.GetStudentHomework] Exception: {ex.Message}");
                Console.WriteLine($"[ParentController.GetStudentHomework] StackTrace: {ex.StackTrace}");
                return InternalServerError("Не удалось загрузить домашние задания ученика");
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
                var result = await _parentFacade.GetStudentAttendanceAsync(studentId);

                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ParentController.GetStudentAttendance] Exception: {ex.Message}");
                Console.WriteLine($"[ParentController.GetStudentAttendance] StackTrace: {ex.StackTrace}");
                return InternalServerError("Не удалось загрузить посещаемость ученика");
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
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
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
                Console.WriteLine($"[ParentController.GetStudentParents] Exception: {ex.Message}");
                Console.WriteLine($"[ParentController.GetStudentParents] StackTrace: {ex.StackTrace}");
                return InternalServerError("Не удалось загрузить список родителей ученика");
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
                return NotFound(new { error = ex.Message });
            }
        }
    }

    public class AddParentRequest
    {
        public int ParentId { get; set; }
    }
}
