using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services;

public sealed class RoleManagementService : IRoleManagementService
{
    private readonly AppDbContext _dbContext;
    private readonly IUserProfileService _userProfileService;
    private readonly ICacheService _cache;

    public RoleManagementService(
        AppDbContext dbContext,
        IUserProfileService userProfileService,
        ICacheService cache)
    {
        _dbContext = dbContext;
        _userProfileService = userProfileService;
        _cache = cache;
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

        // Пользователь загружается один раз: наличие, 404 и итоговый профиль
        // строятся из этой же сущности без повторных запросов.
        var targetUser = await _dbContext.Users
            .FirstOrDefaultAsync(user => user.Id == targetUserId, cancellationToken);

        if (targetUser is null)
        {
            return ServiceResult<CurrentUserProfile>.Fail(404, "User profile was not found.");
        }

        var distinctCodes = roleCodes
            .Select(code => code.Trim())
            .Where(code => code.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var roles = await _dbContext.Roles
            .Where(role => distinctCodes.Contains(role.Code))
            .ToListAsync(cancellationToken);

        if (roles.Count != distinctCodes.Count)
        {
            return ServiceResult<CurrentUserProfile>.Fail(400, "One or more role codes are invalid.");
        }

        var existing = await _dbContext.UserRoles
            .Where(userRole => userRole.UserId == targetUserId)
            .ToListAsync(cancellationToken);

        var incomingRoleIds = roles.Select(role => role.Id).ToHashSet();
        var toRemove = existing.Where(userRole => !incomingRoleIds.Contains(userRole.RoleId)).ToList();
        if (toRemove.Count > 0)
        {
            _dbContext.UserRoles.RemoveRange(toRemove);
        }

        var existingRoleIds = existing.Select(userRole => userRole.RoleId).ToHashSet();
        foreach (var role in roles.Where(role => !existingRoleIds.Contains(role.Id)))
        {
            _dbContext.UserRoles.Add(new UserRole
            {
                UserId = targetUserId,
                RoleId = role.Id,
                AssignedAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Cached user directory entries embed role codes.
        _cache.RemoveByPrefix(CacheKeys.UsersPrefix);

        // Профиль собирается из уже загруженных данных: повторное чтение
        // пользователя и его ролей после SaveChanges не требуется.
        var profile = _userProfileService.BuildProfile(
            targetUser,
            roles.Select(role => role.Code));

        return ServiceResult<CurrentUserProfile>.Ok(profile);
    }
}