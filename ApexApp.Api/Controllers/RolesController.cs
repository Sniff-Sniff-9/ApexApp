using ApexApp.Api.Authorization;
using ApexApp.Api.DTOs;
using ApexApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;


/// <summary>
/// Контроллер, отвечающий за управление ролями пользователей в приложении.
/// </summary>
[Authorize(Policy = AppPolicies.ManageRoles)]
[Route("api/roles")]
public sealed class RolesController : ApiControllerBase
{
    private readonly IRoleManagementService _roleManagementService;

    /// <summary>
    /// Конструктор класса <see cref="RolesController"/>, инициализирующий сервис управления ролями.
    /// </summary>
    public RolesController(IRoleManagementService roleManagementService)
    {
        _roleManagementService = roleManagementService;
    }

    /// <summary>
    /// Получить список всех ролей в системе.
    /// </summary>
    /// <returns>Список ролей.</returns>
    /// <response code="200">Список успешно получен.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> ListRoles(CancellationToken cancellationToken)
    {
        var roles = await _roleManagementService.ListRolesAsync(cancellationToken);
        return Ok(roles.Select(RoleDto.From).ToList());
    }
}
