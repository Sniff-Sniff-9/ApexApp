using ApexApp.Infrastructure.Auth;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Enforces the "one manager per department" business rule.
/// A department manager is an active user who holds the <c>manager</c> role and belongs to
/// that department, so both role assignment and profile updates share the same check.
/// </summary>
internal static class DepartmentManagerGuard
{
    /// <summary>Message returned when a department already has a manager.</summary>
    public const string DepartmentHasManagerMessage = "The department already has a manager.";

    /// <summary>
    /// Returns <c>true</c> when an active user other than <paramref name="excludeUserId"/>
    /// already manages <paramref name="departmentId"/>.
    /// </summary>
    /// <param name="dbContext">Database context.</param>
    /// <param name="departmentId">Department to check.</param>
    /// <param name="excludeUserId">User who is being assigned or moved and must not count.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task<bool> HasOtherActiveManagerAsync(
        AppDbContext dbContext,
        int departmentId,
        Guid excludeUserId,
        CancellationToken cancellationToken) =>
        (from userRole in dbContext.UserRoles.AsNoTracking()
         join user in dbContext.Users.AsNoTracking() on userRole.UserId equals user.Id
         join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
         where userRole.UserId != excludeUserId
               && user.IsActive
               && user.DepartmentId == departmentId
               && role.Code.ToLower() == RoleCodes.Manager
         select userRole.UserId)
        .AnyAsync(cancellationToken);

    /// <summary>
    /// Returns <c>true</c> when the user currently holds the <c>manager</c> role.
    /// </summary>
    /// <param name="dbContext">Database context.</param>
    /// <param name="userId">User to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task<bool> IsManagerAsync(
        AppDbContext dbContext,
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.UserRoles
            .AsNoTracking()
            .AnyAsync(
                userRole => userRole.UserId == userId
                            && userRole.Role.Code.ToLower() == RoleCodes.Manager,
                cancellationToken);
}
