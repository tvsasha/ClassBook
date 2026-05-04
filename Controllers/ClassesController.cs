using ClassBook.Application.DTOs;
using ClassBook.Application.Facades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/classes")]
    [Authorize(Policy = "AdminOnly")]
    public class ClassesController : ApiControllerBase
    {
        private readonly ClassFacade _classFacade;

        public ClassesController(ClassFacade classFacade)
        {
            _classFacade = classFacade;
        }

        /// <summary>
        /// Возвращает список всех учебных классов, доступных в системе.
        /// </summary>
        /// <returns>Краткий список классов для справочников и административных форм.</returns>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _classFacade.GetAllClassesAsync());
        }

        /// <summary>
        /// Создаёт новый учебный класс в системе.
        /// </summary>
        /// <param name="dto">Название создаваемого класса.</param>
        /// <returns>Созданный класс для немедленного отображения в интерфейсе.</returns>
        [HttpPost]
        public async Task<IActionResult> CreateClass([FromBody] CreateClassDto dto)
        {
            try
            {
                return Ok(await _classFacade.CreateClassAsync(dto.Name));
            }
            catch (ArgumentException ex)
            {
                return BadRequestError(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequestError(ex.Message);
            }
        }

        /// <summary>
        /// Удаляет учебный класс, если к нему не привязаны ученики и уроки.
        /// </summary>
        /// <param name="id">Идентификатор класса.</param>
        /// <returns>Пустой ответ при успешном удалении.</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteClass(int id)
        {
            try
            {
                await _classFacade.DeleteClassAsync(id);
                return NoContent();
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
    }
}
