using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services;

public sealed class UserProfileService : IUserProfileService
{
    private const int NameMaxLength = 100;

    private readonly AppDbContext _dbContext;

    public UserProfileService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CurrentUserProfile?> GetCurrentAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await GetByIdAsync(userId, includeInactive: true, cancellationToken);
    }

    public async Task<IReadOnlyList<CurrentUserProfile>> ListAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Users.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(user => user.IsActive);
        }

        var users = await query
            .OrderBy(user => user.LastName)
            .ThenBy(user => user.FirstName)
            .ToListAsync(cancellationToken);

        var userIds = users.Select(user => user.Id).ToList();
        var rolesByUser = await LoadRolesAsync(userIds, cancellationToken);

        return users
            .Select(user => Map(user, rolesByUser.GetValueOrDefault(user.Id, [])))
            .ToList();
    }

    public async Task<CurrentUserProfile?> GetByIdAsync(
        Guid userId,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Users.AsNoTracking().Where(user => user.Id == userId);
        if (!includeInactive)
        {
            query = query.Where(user => user.IsActive);
        }

        var user = await query.FirstOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return null;
        }

        var roles = await _dbContext.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.UserId == userId)
            .Select(userRole => userRole.Role.Code)
            .Distinct()
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);

        return Map(user, roles);
    }

    public async Task<ServiceResult<CurrentUserProfile>> UpdateOwnProfileAsync(
        Guid userId,
        UpdateOwnProfileCommand command,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult<CurrentUserProfile>.Fail(404, "User profile was not found.");
        }

        var validationError = ApplyOwnProfileChanges(user, command);
        if (validationError is not null)
        {
            return ServiceResult<CurrentUserProfile>.Fail(400, validationError);
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var profile = await GetCurrentAsync(userId, cancellationToken);
        return ServiceResult<CurrentUserProfile>.Ok(profile!);
    }

    public async Task<ServiceResult<CurrentUserProfile>> AdminUpdateAsync(
        Guid targetUserId,
        AdminUpdateUserCommand command,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);
        if (user is null)
        {
            return ServiceResult<CurrentUserProfile>.Fail(404, "User profile was not found.");
        }

        var validationError = ApplyAdminProfileChanges(user, command);
        if (validationError is not null)
        {
            return ServiceResult<CurrentUserProfile>.Fail(400, validationError);
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var profile = await GetCurrentAsync(targetUserId, cancellationToken);
        return ServiceResult<CurrentUserProfile>.Ok(profile!);
    }

    private async Task<Dictionary<Guid, List<string>>> LoadRolesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var rows = await _dbContext.UserRoles
            .AsNoTracking()
            .Where(userRole => userIds.Contains(userRole.UserId))
            .Select(userRole => new { userRole.UserId, userRole.Role.Code })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => row.Code).Distinct().OrderBy(code => code).ToList());
    }

    private static CurrentUserProfile Map(User user, IReadOnlyList<string> roles)
    {
        return new CurrentUserProfile
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            MiddleName = user.MiddleName,
            AvatarUrl = user.AvatarUrl,
            Department = user.Department,
            Position = user.Position,
            IsActive = user.IsActive,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            Roles = roles
        };
    }

    private static string? ApplyOwnProfileChanges(User user, UpdateOwnProfileCommand command)
    {
        if (command.FirstName is not null)
        {
            var firstName = command.FirstName.Trim();
            if (firstName.Length is 0 or > NameMaxLength)
            {
                return "First name is required and must be at most 100 characters.";
            }

            user.FirstName = firstName;
        }

        if (command.LastName is not null)
        {
            var lastName = command.LastName.Trim();
            if (lastName.Length is 0 or > NameMaxLength)
            {
                return "Last name is required and must be at most 100 characters.";
            }

            user.LastName = lastName;
        }

        if (command.MiddleName is not null)
        {
            var middleName = command.MiddleName.Trim();
            if (middleName.Length > NameMaxLength)
            {
                return "Middle name must be at most 100 characters.";
            }

            user.MiddleName = middleName.Length == 0 ? null : middleName;
        }

        if (command.AvatarUrl is not null)
        {
            var avatarUrl = command.AvatarUrl.Trim();
            user.AvatarUrl = avatarUrl.Length == 0 ? null : avatarUrl;
        }

        return null;
    }

    private static string? ApplyAdminProfileChanges(User user, AdminUpdateUserCommand command)
    {
        var ownProfileError = ApplyOwnProfileChanges(user, new UpdateOwnProfileCommand
        {
            FirstName = command.FirstName,
            LastName = command.LastName,
            MiddleName = command.MiddleName,
            AvatarUrl = command.AvatarUrl
        });

        if (ownProfileError is not null)
        {
            return ownProfileError;
        }

        if (command.Department is not null)
        {
            var department = command.Department.Trim();
            if (department.Length == 0)
            {
                return "Department is required.";
            }

            user.Department = department;
        }

        if (command.Position is not null)
        {
            var position = command.Position.Trim();
            if (position.Length == 0)
            {
                return "Position is required.";
            }

            user.Position = position;
        }

        if (command.IsActive is not null)
        {
            user.IsActive = command.IsActive.Value;
        }

        return null;
    }
}
