using System.Security.Claims;
using ApexApp.Api.Extensions;
using ApexApp.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Api.Authorization;

/// <summary>
/// Трансформация утверждений пользователя для добавления дополнительных данных о пользователе, таких как активность и роли.
/// </summary>
/// <remarks>
/// Этот класс реализует интерфейс <see cref="IClaimsTransformation"/> и используется для добавления дополнительных утверждений к объекту 
/// <see cref="ClaimsPrincipal"/> после аутентификации пользователя. 
/// Он проверяет, является ли пользователь активным, и добавляет соответствующее утверждение,
/// а также извлекает роли пользователя из базы данных и добавляет их в качестве утверждений.
/// </remarks>
public sealed class UserClaimsTransformation : IClaimsTransformation
{
    private const string TransformedClaim = "apex_transformed";
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// Конструктор класса <see cref="UserClaimsTransformation"/>.
    /// </summary>
    /// <param name="dbContext">Контекст базы данных.</param>
    public UserClaimsTransformation(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Метод, выполняющий трансформацию утверждений пользователя.
    /// </summary>
    /// <param name="principal">Principal пользователя.</param>
    /// <returns>Principal пользователя с трансформированными утверждениями.</returns>
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return principal;
        }

        if (principal.HasClaim(claim => claim.Type == TransformedClaim))
        {
            return principal;
        }

        identity.AddClaim(new Claim(TransformedClaim, "true"));

        var userId = principal.GetUserId();
        if (userId is null)
        {
            return principal;
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId.Value)
            .Select(u => new { u.IsActive })
            .FirstOrDefaultAsync();

        if (user is null)
        {
            identity.AddClaim(new Claim(ActiveUserHandler.IsActiveClaim, "false"));
            return principal;
        }

        identity.AddClaim(new Claim(ActiveUserHandler.IsActiveClaim, user.IsActive ? "true" : "false"));

        var roleCodes = await _dbContext.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.UserId == userId.Value)
            .Select(userRole => userRole.Role.Code)
            .Distinct()
            .ToListAsync();

        foreach (var roleCode in roleCodes)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, roleCode));
        }

        return principal;
    }
}
