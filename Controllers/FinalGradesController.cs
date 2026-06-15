using ClassBook.Application.DTOs;
using ClassBook.Application.Facades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/final-grades")]
    [Authorize(Roles = "Администратор,Директор,Учитель,Ученик,Родитель")]
    public class FinalGradesController : ApiControllerBase
    {
        private readonly FinalGradeFacade _facade;

        public FinalGradesController(FinalGradeFacade facade)
        {
            _facade = facade;
        }

        private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        private string Role => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        [HttpGet("classes")]
        public async Task<IActionResult> GetClasses() => Ok(await _facade.GetAvailableClassesAsync(UserId, Role));

        [HttpGet("me")]
        [Authorize(Roles = "Ученик")]
        public async Task<IActionResult> GetMine(int periodId)
        {
            try { return Ok(await _facade.GetMyReportAsync(UserId, periodId)); }
            catch (KeyNotFoundException ex) { return NotFoundError(ex.Message); }
        }

        [HttpGet("student/{studentId:int}")]
        public async Task<IActionResult> GetStudent(int studentId, int periodId)
        {
            try { return Ok(await _facade.GetStudentReportAsync(UserId, Role, studentId, periodId)); }
            catch (KeyNotFoundException ex) { return NotFoundError(ex.Message); }
            catch (UnauthorizedAccessException ex) { return ForbiddenError(ex.Message); }
        }

        [HttpGet("class/{classId:int}")]
        [Authorize(Roles = "Администратор,Директор,Учитель")]
        public async Task<IActionResult> GetClass(int classId, int periodId)
        {
            try { return Ok(await _facade.GetClassReportAsync(UserId, Role, classId, periodId)); }
            catch (KeyNotFoundException ex) { return NotFoundError(ex.Message); }
            catch (UnauthorizedAccessException ex) { return ForbiddenError(ex.Message); }
        }

        [HttpPut]
        [Authorize(Roles = "Администратор,Учитель")]
        public async Task<IActionResult> SetGrade([FromBody] SetFinalGradeDto dto)
        {
            try
            {
                await _facade.SetFinalGradeAsync(UserId, Role, dto);
                return NoContent();
            }
            catch (ArgumentException ex) { return BadRequestError(ex.Message); }
            catch (KeyNotFoundException ex) { return NotFoundError(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequestError(ex.Message); }
            catch (UnauthorizedAccessException ex) { return ForbiddenError(ex.Message); }
        }

        [HttpGet("me/export")]
        [Authorize(Roles = "Ученик")]
        public async Task<IActionResult> ExportMine(int periodId)
        {
            try
            {
                var file = await _facade.ExportMyCsvAsync(UserId, periodId);
                return File(file.Content, "text/csv; charset=utf-8", file.FileName);
            }
            catch (KeyNotFoundException ex) { return NotFoundError(ex.Message); }
        }

        [HttpGet("student/{studentId:int}/export")]
        public async Task<IActionResult> ExportStudent(int studentId, int periodId)
        {
            try
            {
                var file = await _facade.ExportStudentCsvAsync(UserId, Role, studentId, periodId);
                return File(file.Content, "text/csv; charset=utf-8", file.FileName);
            }
            catch (KeyNotFoundException ex) { return NotFoundError(ex.Message); }
            catch (UnauthorizedAccessException ex) { return ForbiddenError(ex.Message); }
        }

        [HttpGet("class/{classId:int}/export")]
        [Authorize(Roles = "Администратор,Директор,Учитель")]
        public async Task<IActionResult> ExportClass(int classId, int periodId)
        {
            try
            {
                var file = await _facade.ExportClassCsvAsync(UserId, Role, classId, periodId);
                return File(file.Content, "text/csv; charset=utf-8", file.FileName);
            }
            catch (KeyNotFoundException ex) { return NotFoundError(ex.Message); }
            catch (UnauthorizedAccessException ex) { return ForbiddenError(ex.Message); }
        }
    }
}
