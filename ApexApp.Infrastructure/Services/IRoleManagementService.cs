using ApexApp.Infrastructure.Common;

namespace ApexApp.Infrastructure.Services;

public interface IRoleManagementService
{
    Task<IReadOnlyList<RoleInfo>> ListRolesAsync(CancellationToken cancellationToken);

    Task<ServiceResult<CurrentUserProfile>> SetUserRolesAsync(
        Guid actorUserId,
        Guid targetUserId,
        IReadOnlyList<string> roleCodes,
        CancellationToken cancellationToken);
}
