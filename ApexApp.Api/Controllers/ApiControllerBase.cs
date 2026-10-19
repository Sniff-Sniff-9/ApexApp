using ApexApp.Infrastructure.Common;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;

/// <summary>
/// Контроллер, который служит базовым классом для всех контроллеров API в приложении.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Метод, который преобразует результат сервиса в соответствующий HTTP-ответ.
    /// </summary>
    protected ActionResult<T> ToActionResult<T>(ServiceResult<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return ToErrorResult(result.StatusCode, result.Error);
    }

    /// <summary>
    /// Метод, который преобразует код состояния и сообщение об ошибке в соответствующий HTTP-ответ.
    /// </summary>
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
