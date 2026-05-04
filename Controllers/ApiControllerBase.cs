using Microsoft.AspNetCore.Mvc;

namespace ClassBook.Controllers
{
    public abstract class ApiControllerBase : ControllerBase
    {
        protected IActionResult BadRequestError(string message, string? code = null)
            => BadRequest(new ApiErrorResponse(message, code));

        protected IActionResult NotFoundError(string message, string? code = null)
            => NotFound(new ApiErrorResponse(message, code));

        protected IActionResult ForbiddenError(string message, string? code = null)
            => StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse(message, code));

        protected IActionResult UnauthorizedError(string message, string? code = null)
            => Unauthorized(new ApiErrorResponse(message, code));

        protected IActionResult InternalServerError(string message, string? code = null)
            => StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse(message, code));
    }

    public sealed record ApiErrorResponse(string Error, string? Code = null);
}
