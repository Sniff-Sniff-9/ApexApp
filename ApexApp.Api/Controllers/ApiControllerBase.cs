using ApexApp.Infrastructure.Common;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult<T> ToActionResult<T>(ServiceResult<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return ToErrorResult(result.StatusCode, result.Error);
    }

    protected ActionResult ToErrorResult(int statusCode, string? error)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => BadRequest(new { message = error }),
            StatusCodes.Status401Unauthorized => Unauthorized(),
            StatusCodes.Status403Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = error }),
            StatusCodes.Status404NotFound => NotFound(new { message = error }),
            StatusCodes.Status409Conflict => Conflict(new { message = error }),
            _ => Problem(detail: error, statusCode: statusCode)
        };
    }
}
