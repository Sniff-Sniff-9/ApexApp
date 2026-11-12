using System.Security.Claims;
using ApexApp.Api.Authorization;
using ApexApp.Api.Extensions;
using ApexApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;

namespace ApexApp.Api.Analytics;

/// <summary>
/// Результат проверки области доступа к аналитике.
/// </summary>
/// <param name="Allowed">Признак разрешённого доступа.</param>
/// <param name="StatusCode">HTTP-код отказа.</param>
/// <param name="Error">Сообщение об отказе.</param>
/// <param name="DepartmentId">Разрешённый фильтр отдела; <c>null</c> — все отделы.</param>
public sealed record AnalyticsScope(bool Allowed, int StatusCode, string? Error, int? DepartmentId)
{
    /// <summary>Создаёт разрешённую область доступа.</summary>
    /// <param name="departmentId">Фильтр отдела.</param>
    /// <returns>Область доступа.</returns>
    public static AnalyticsScope All(int? departmentId = null) => new(true, 200, null, departmentId);

    /// <summary>Создаёт отказ в доступе.</summary>
    /// <param name="statusCode">HTTP-код.</param>
    /// <param name="error">Сообщение.</param>
    /// <returns>Область доступа с отказом.</returns>
    public static AnalyticsScope Denied(int statusCode, string error) => new(false, statusCode, error, null);
}

/// <summary>
/// Определяет, какие отделы доступны вызывающему коду. Руководитель видит только
/// свой отдел; администраторы, суперадминистраторы и HR видят все отделы.
/// </summary>
public interface IAnalyticsScopeResolver
{
    /// <summary>
    /// Проверяет запрошенный отдел и возвращает разрешённый фильтр.
    /// </summary>
    /// <param name="user">Пользователь запроса.</param>
    /// <param name="requestedDepartmentId">Запрошенный отдел или <c>null</c>.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Область доступа.</returns>
    Task<AnalyticsScope> ResolveAsync(
        ClaimsPrincipal user,
        int? requestedDepartmentId,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class AnalyticsScopeResolver(
    IAuthorizationService authorizationService,
    IUserProfileService userProfileService) : IAnalyticsScopeResolver
{
    /// <inheritdoc />
    public async Task<AnalyticsScope> ResolveAsync(
        ClaimsPrincipal user,
        int? requestedDepartmentId,
        CancellationToken cancellationToken = default)
    {
        var userId = user.GetUserId();

        if (userId is null)
        {
            return AnalyticsScope.Denied(401, "Authentication is required.");
        }

        // Полный доступ к аналитике отделов имеют те же роли, что и к управлению организацией.
        var canViewAll = await authorizationService.AuthorizeAsync(user, AppPolicies.ManageOrg);

        if (canViewAll.Succeeded)
        {
            return AnalyticsScope.All(requestedDepartmentId);
        }

        var departmentId = await userProfileService.GetDepartmentIdAsync(
            userId.Value,
            cancellationToken);

        if (departmentId is null)
        {
            return AnalyticsScope.Denied(
                403,
                "The caller has no department, so department analytics is unavailable.");
        }

        if (requestedDepartmentId is int requested && requested != departmentId.Value)
        {
            return AnalyticsScope.Denied(
                403,
                "Managers can only view analytics of their own department.");
        }

        return AnalyticsScope.All(departmentId);
    }
}
