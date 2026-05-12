using ClassBook.Application.Facades;
using ClassBook.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/class-teacher")]
    [Authorize(Roles = "Учитель,Администратор")]
    public class ClassTeacherController : ApiControllerBase
    {
        private readonly ClassTeacherFacade _facade;

        public ClassTeacherController(ClassTeacherFacade facade)
        {
            _facade = facade;
        }

        private int GetCurrentUserId()
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : 0;
        }

        /// <summary>
        /// Возвращает сводку классного руководителя по закрепленным классам.
        /// </summary>
        /// <returns>Сводка по предметам, ученикам и урокам собственного преподавателя.</returns>
        [HttpGet("me/dashboard")]
        public async Task<IActionResult> GetMyDashboard()
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId <= 0)
                    return UnauthorizedError("Не удалось определить пользователя");

                return Ok(await _facade.GetDashboardAsync(currentUserId));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
        }

        /// <summary>
        /// Возвращает все связи классных руководителей с классами.
        /// </summary>
        /// <returns>Список назначений классных руководителей.</returns>
        [HttpGet("assignments")]
        [Authorize(Roles = "Администратор")]
        public async Task<IActionResult> GetAssignments()
        {
            return Ok(await _facade.GetAssignmentsAsync());
        }

        /// <summary>
        /// Назначает учителя классным руководителем выбранного класса.
        /// </summary>
        /// <param name="request">Идентификаторы класса и учителя.</param>
        /// <returns>Обновленный список назначений.</returns>
        [HttpPost("assignments")]
        [Authorize(Roles = "Администратор")]
        public async Task<IActionResult> Assign(AssignClassTeacherRequest request)
        {
            try
            {
                await _facade.AssignAsync(request.ClassId, request.TeacherId);
                return Ok(await _facade.GetAssignmentsAsync());
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
        }

        /// <summary>
        /// Снимает учителя с классного руководства по выбранному классу.
        /// </summary>
        /// <param name="classId">Идентификатор класса.</param>
        /// <param name="teacherId">Идентификатор учителя.</param>
        /// <returns>Обновленный список назначений.</returns>
        [HttpDelete("assignments/{classId:int}/{teacherId:int}")]
        [Authorize(Roles = "Администратор")]
        public async Task<IActionResult> Unassign(int classId, int teacherId)
        {
            try
            {
                await _facade.UnassignAsync(classId, teacherId);
                return Ok(await _facade.GetAssignmentsAsync());
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundError(ex.Message);
            }
        }
    }
}
