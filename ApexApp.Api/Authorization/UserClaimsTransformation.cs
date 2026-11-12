using System.Security.Claims;
using ApexApp.Api.Diagnostics;
using ApexApp.Api.Extensions;
using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ApexApp.Api.Authorization;

/// <summary>
/// Transforms the authenticated principal so that role claims reflect the current
/// database state rather than only the roles embedded in the access token.
/// </summary>
/// <remarks>
/// The access token is still the source of the user identity and of <c>is_active</c>.
/// Roles are loaded from <c>user_roles</c> and cached for a short interval. Role changes
/// remove the cache entry, so an administrative change takes effect on the next request.
/// If the database is unavailable, the transformation falls back to the token claims.
/// </remarks>
public sealed class UserClaimsTransformation(
    AppDbContext dbContext,
    ICacheService cache,
    ILogger<UserClaimsTransformation> logger) : IClaimsTransformation
{
    private const string TransformedClaim = "apex_transformed";

    /// <summary>Утверждение токена: признак активности пользователя (добавляется хуком).</summary>
    public const string IsActiveClaim = ActiveUserHandler.IsActiveClaim;

    /// <summary>Утверждение токена: код роли пользователя (добавляется хуком).</summary>
    public const string UserRoleClaim = "user_role";

    /// <summary>Разделители на случай, если хук вернул несколько ролей одной строкой.</summary>
    private static readonly char[] RoleSeparators = [',', ';', ' '];

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

        // Только для локальной диагностики: этап больше не содержит обязательных обращений к базе данных.
        using var timingStage = RequestTimingContext.Stage("claims-transform");

        var userId = principal.GetUserId();

        // Роли применяются только к идентифицированному пользователю.
        if (userId is null)
        {
            return principal;
        }

        if (!NormalizeIsActive(identity))
        {
            return principal;
        }

        var roleCodes = await ReadRoleCodesAsync(userId.Value, principal);

        // A token may already contain role claims from another source; rebuild the set
        // from the authoritative database snapshot (or the token fallback).
        foreach (var claim in identity.FindAll(ClaimTypes.Role).ToList())
        {
            identity.RemoveClaim(claim);
        }

        foreach (var roleCode in roleCodes)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, roleCode));
        }

        return principal;
    }

    private async Task<IReadOnlyList<string>> ReadRoleCodesAsync(
        Guid userId,
        ClaimsPrincipal principal)
    {
        var cacheKey = CacheKeys.AuthRoles(userId);

        if (cache.TryGet<IReadOnlyList<string>>(cacheKey, out var cached)
            && cached is not null)
        {
            return cached;
        }

        try
        {
            var roles = await dbContext.UserRoles
                .AsNoTracking()
                .Where(userRole => userRole.UserId == userId)
                .Select(userRole => userRole.Role.Code)
                .Distinct()
                .ToListAsync();

            cache.Set(cacheKey, roles, CacheKeys.AuthRolesTtl);

            return roles;
        }
        catch (Exception exception)
        {
            // Preserve availability when the database is temporarily unavailable.
            // The token was already validated and its role claims are a safe fallback.
            logger.LogWarning(
                exception,
                "Could not load roles for user {UserId}; falling back to token claims.",
                userId);

            return ReadRoleCodes(principal).ToList();
        }
    }

    /// <summary>
    /// Приводит утверждение активности из токена к единственному каноническому значению.
    /// </summary>
    /// <param name="identity">Удостоверение пользователя.</param>
    /// <returns><c>true</c>, если пользователь активен.</returns>
    private static bool NormalizeIsActive(ClaimsIdentity identity)
    {
        var claims = identity.FindAll(IsActiveClaim).ToArray();
        var isActive = claims.Any(claim =>
            string.Equals(claim.Value, "true", StringComparison.OrdinalIgnoreCase));

        var canonicalValue = isActive ? "true" : "false";
        if (claims.Length == 1 && string.Equals(claims[0].Value, canonicalValue, StringComparison.Ordinal))
        {
            return isActive;
        }

        // Отсутствующее утверждение трактуется как «не активен», поэтому запрос будет отклонён политикой.
        foreach (var claim in claims)
        {
            identity.RemoveClaim(claim);
        }

        identity.AddClaim(new Claim(IsActiveClaim, canonicalValue));

        return isActive;
    }

    /// <summary>
    /// Читает коды ролей из утверждений токена без дублей.
    /// </summary>
    /// <param name="principal">Principal пользователя.</param>
    /// <returns>Уникальные коды ролей.</returns>
    private static IEnumerable<string> ReadRoleCodes(ClaimsPrincipal principal)
    {
        var roleCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var claim in principal.FindAll(UserRoleClaim))
        {
            foreach (var roleCode in claim.Value.Split(
                RoleSeparators,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                roleCodes.Add(roleCode);
            }
        }

        return roleCodes;
    }
}
