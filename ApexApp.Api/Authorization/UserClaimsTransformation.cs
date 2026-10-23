using System.Security.Claims;
using ApexApp.Api.Diagnostics;
using ApexApp.Api.Extensions;
using Microsoft.AspNetCore.Authentication;

namespace ApexApp.Api.Authorization;

/// <summary>
/// Трансформация утверждений пользователя для добавления дополнительных данных о пользователе, таких как активность и роли.
/// </summary>
/// <remarks>
/// Этот класс реализует интерфейс <see cref="IClaimsTransformation"/> и используется для добавления дополнительных утверждений к объекту
/// <see cref="ClaimsPrincipal"/> после аутентификации пользователя.
/// Данные берутся из уже проверенного токена доступа, который выдаёт Supabase Custom Access Token Hook,
/// поэтому запросы к базе данных при каждом обращении не выполняются:
/// <list type="bullet">
/// <item>
/// <description>
/// утверждение <c>is_active</c> приводится к единственному каноническому значению <c>true</c>/<c>false</c>
/// и читается обработчиком <see cref="ActiveUserHandler"/>;
/// </description>
/// </item>
/// <item>
/// <description>
/// утверждение <c>user_role</c> отображается в утверждения <see cref="ClaimTypes.Role"/>
/// для обработчика <see cref="AnyRoleHandler"/>.
/// </description>
/// </item>
/// </list>
/// Если <c>is_active</c> отсутствует или равно <c>false</c>, утверждения ролей не добавляются,
/// и <see cref="ActiveUserHandler"/> отклоняет запрос.
/// </remarks>
public sealed class UserClaimsTransformation : IClaimsTransformation
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
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        if (principal.HasClaim(claim => claim.Type == TransformedClaim))
        {
            return Task.FromResult(principal);
        }

        identity.AddClaim(new Claim(TransformedClaim, "true"));

        // Только для локальной диагностики: этап больше не содержит обращений к базе данных.
        using var timingStage = RequestTimingContext.Stage("claims-transform");

        // Роли применяются только к идентифицированному пользователю:
        // без sub роли из токена не добавляются, а признак активности остаётся таким, как в токене.
        if (principal.GetUserId() is null)
        {
            return Task.FromResult(principal);
        }

        if (!NormalizeIsActive(identity))
        {
            return Task.FromResult(principal);
        }

        foreach (var roleCode in ReadRoleCodes(principal))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, roleCode));
        }

        return Task.FromResult(principal);
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
