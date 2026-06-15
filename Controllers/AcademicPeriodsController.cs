using ClassBook.Application.DTOs;
using ClassBook.Application.Facades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClassBook.Controllers
{
    [ApiController]
    [Route("api/academic-periods")]
    [Authorize]
    public class AcademicPeriodsController : ApiControllerBase
    {
        private readonly FinalGradeFacade _facade;

        public AcademicPeriodsController(FinalGradeFacade facade)
        {
            _facade = facade;
        }

        [HttpGet]
        public async Task<IActionResult> GetYears() => Ok(await _facade.GetYearsAsync());

        [HttpPost("years")]
        [Authorize(Roles = "Администратор")]
        public async Task<IActionResult> SaveYear([FromBody] SaveAcademicYearDto dto)
        {
            try { return Ok(await _facade.SaveYearAsync(dto)); }
            catch (ArgumentException ex) { return BadRequestError(ex.Message); }
            catch (KeyNotFoundException ex) { return NotFoundError(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequestError(ex.Message); }
        }

        [HttpPost]
        [Authorize(Roles = "Администратор")]
        public async Task<IActionResult> SavePeriod([FromBody] SaveAcademicPeriodDto dto)
        {
            try { return Ok(await _facade.SavePeriodAsync(dto)); }
            catch (ArgumentException ex) { return BadRequestError(ex.Message); }
            catch (KeyNotFoundException ex) { return NotFoundError(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequestError(ex.Message); }
        }

        [HttpDelete("{periodId:int}")]
        [Authorize(Roles = "Администратор")]
        public async Task<IActionResult> DeletePeriod(int periodId)
        {
            try
            {
                await _facade.DeletePeriodAsync(periodId);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFoundError(ex.Message); }
        }
    }
}
