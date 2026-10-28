using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;

namespace ApexApp.Infrastructure.Services;

public interface IUserProfileService
{
    Task<CurrentUserProfile?> GetCurrentAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Собирает профиль из уже загруженной сущности пользователя и набора ролей,
    /// чтобы вызывающий код мог не перечитывать пользователя после SaveChanges.
    /// </summary>
    /// <param name="user">Сущность пользователя.</param>
    /// <param name="roleCodes">Коды ролей пользователя.</param>
    CurrentUserProfile BuildProfile(User user, IEnumerable<string> roleCodes);

    Task<IReadOnlyList<CurrentUserProfile>> ListAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CurrentUserProfile>?> GetByDepartmentAsync(
    Guid managerId,
    CancellationToken cancellationToken = default);

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

    Task<ServiceResult<CurrentUserProfile>> CreateEmployeeAsync(
        CreateEmployeeCommand command,
        CancellationToken cancellationToken);
}
