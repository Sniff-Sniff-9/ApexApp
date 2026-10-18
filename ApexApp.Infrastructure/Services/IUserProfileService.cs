using ApexApp.Infrastructure.Common;

namespace ApexApp.Infrastructure.Services;

public interface IUserProfileService
{
    Task<CurrentUserProfile?> GetCurrentAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CurrentUserProfile>> ListAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<CurrentUserProfile?> GetByIdAsync(
        Guid userId,
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ServiceResult<CurrentUserProfile>> UpdateOwnProfileAsync(
        Guid userId,
        UpdateOwnProfileCommand command,
        CancellationToken cancellationToken);

    Task<ServiceResult<CurrentUserProfile>> AdminUpdateAsync(
        Guid targetUserId,
        AdminUpdateUserCommand command,
        CancellationToken cancellationToken);
}
