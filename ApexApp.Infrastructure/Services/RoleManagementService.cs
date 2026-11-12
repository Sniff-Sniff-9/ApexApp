using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Auth;
using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ApexApp.Infrastructure.Services;

public sealed class RoleManagementService : IRoleManagementService
{
    private readonly AppDbContext _dbContext;
    private readonly IUserProfileService _userProfileService;
    private readonly ICacheService _cache;
    private readonly ILogger<RoleManagementService> _logger;

    public RoleManagementService(
        AppDbContext dbContext,
        IUserProfileService userProfileService,
        ICacheService cache,
        ILogger<RoleManagementService> logger)
    {
        _dbContext = dbContext;
        _userProfileService = userProfileService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RoleInfo>> ListRolesAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGet<IReadOnlyList<RoleInfo>>(CacheKeys.Roles, out var cached) &&
            cached is not null)
        {
            return cached;
        }

        var roles = await _dbContext.Roles
            .AsNoTracking()
            .OrderBy(role => role.Code)
            .Select(role => new RoleInfo
            {
                Id = role.Id,
                Code = role.Code,
                Name = role.Name,
                Description = role.Description
            })
            .ToListAsync(cancellationToken);

        _cache.Set<IReadOnlyList<RoleInfo>>(CacheKeys.Roles, roles, CacheKeys.RolesTtl);

        return roles;
    }

    public async Task<ServiceResult<CurrentUserProfile>> SetUserRolesAsync(
        Guid actorUserId,
        Guid targetUserId,
        IReadOnlyList<string> roleCodes,
        CancellationToken cancellationToken)
    {
        if (actorUserId == targetUserId)
        {
            return ServiceResult<CurrentUserProfile>.Fail(403, "You cannot modify your own roles.");
        }

        var targetExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == targetUserId, cancellationToken);

        if (!targetExists)
        {
            return ServiceResult<CurrentUserProfile>.Fail(404, "User profile was not found.");
        }

        var existing = await _dbContext.UserRoles
            .Where(userRole => userRole.UserId == targetUserId)
            .ToListAsync(cancellationToken);

        var existingRoleIds = existing.Select(userRole => userRole.RoleId).ToList();

        var existingRoleCodes = await _dbContext.Roles
            .AsNoTracking()
            .Where(role => existingRoleIds.Contains(role.Id))
            .Select(role => role.Code)
            .ToListAsync(cancellationToken);

        var targetIsSuperAdmin = existingRoleCodes
            .Contains(RoleCodes.SuperAdmin, StringComparer.OrdinalIgnoreCase);

        // A superadmin's roles are immutable to every other superadmin. Self-modification
        // is already blocked above, so this protects the highest-privilege accounts.
        if (targetIsSuperAdmin && actorUserId != targetUserId)
        {
            return ServiceResult<CurrentUserProfile>.Fail(
                403,
                "A superadmin cannot change another superadmin's roles.");
        }

        var distinctCodes = roleCodes
            .Select(code => code.Trim().ToLowerInvariant())
            .Where(code => code.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var roles = await _dbContext.Roles
            .Where(role => distinctCodes.Contains(role.Code.ToLower()))
            .ToListAsync(cancellationToken);

        if (roles.Count != distinctCodes.Count)
        {
            return ServiceResult<CurrentUserProfile>.Fail(400, "One or more role codes are invalid.");
        }

        var incomingIsSuperAdmin = roles.Any(role =>
            string.Equals(role.Code, RoleCodes.SuperAdmin, StringComparison.OrdinalIgnoreCase));

        if (targetIsSuperAdmin && !incomingIsSuperAdmin)
        {
            var activeSuperAdmins = await (
                    from userRole in _dbContext.UserRoles.AsNoTracking()
                    join user in _dbContext.Users.AsNoTracking() on userRole.UserId equals user.Id
                    where user.IsActive
                          && userRole.Role.Code.ToLower() == RoleCodes.SuperAdmin
                    select userRole.UserId)
                .Distinct()
                .CountAsync(cancellationToken);

            if (activeSuperAdmins <= 1)
            {
                return ServiceResult<CurrentUserProfile>.Fail(
                    409,
                    "The last active superadmin cannot lose the superadmin role.");
            }
        }

        var incomingRoleIds = roles.Select(role => role.Id).ToHashSet();
        var toRemove = existing
            .Where(userRole => !incomingRoleIds.Contains(userRole.RoleId))
            .ToList();

        if (toRemove.Count > 0)
        {
            _dbContext.UserRoles.RemoveRange(toRemove);
        }

        var currentRoleIds = existing.Select(userRole => userRole.RoleId).ToHashSet();

        foreach (var role in roles.Where(role => !currentRoleIds.Contains(role.Id)))
        {
            _dbContext.UserRoles.Add(new UserRole
            {
                UserId = targetUserId,
                RoleId = role.Id,
                AssignedAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User roles changed. Actor={ActorUserId}, Target={TargetUserId}, OldRoles={OldRoles}, NewRoles={NewRoles}",
            actorUserId,
            targetUserId,
            string.Join(",", existingRoleCodes.OrderBy(code => code)),
            string.Join(",", roles.Select(role => role.Code).OrderBy(code => code)));

        // Cached authorization roles and directory entries must reflect the change immediately.
        _cache.Remove(CacheKeys.AuthRoles(targetUserId));
        _cache.RemoveByPrefix(CacheKeys.UsersPrefix);

        var profile = await _userProfileService.GetCurrentAsync(targetUserId, cancellationToken);
        return ServiceResult<CurrentUserProfile>.Ok(profile!);
    }
}