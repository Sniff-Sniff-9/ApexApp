using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text.Json;


namespace ApexApp.Infrastructure.Services;

public sealed class UserProfileService : IUserProfileService
{
    private const int NameMaxLength = 100;

    private readonly AppDbContext _dbContext;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ICacheService _cache;

    public UserProfileService(
        AppDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ICacheService cache)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _cache = cache;
    }

    public async Task<CurrentUserProfile?> GetCurrentAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await GetByIdAsync(userId, includeInactive: true, cancellationToken);
    }

    public async Task<IReadOnlyList<CurrentUserProfile>?> GetByDepartmentAsync(
    Guid managerId,
    CancellationToken cancellationToken = default)
    {
        var manager = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == managerId)
            .Select(user => new { user.Department })
            .SingleOrDefaultAsync(cancellationToken);

        if (manager is null)
        {
            return null;
        }

        // Keyed by department name: the payload does not depend on the caller.
        var cacheKey = CacheKeys.DepartmentUsers(manager.Department);

        if (_cache.TryGet<IReadOnlyList<CurrentUserProfile>>(cacheKey, out var cached) &&
            cached is not null)
        {
            return cached;
        }

        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Department == manager.Department)
            .OrderBy(user => user.LastName)
            .ThenBy(user => user.FirstName)
            .ToListAsync(cancellationToken);

        var userIds = users.Select(user => user.Id).ToList();
        var rolesByUser = await LoadRolesAsync(userIds, cancellationToken);

        IReadOnlyList<CurrentUserProfile> department = users
            .Select(user => Map(user, rolesByUser.GetValueOrDefault(user.Id, [])))
            .ToList();

        _cache.Set(cacheKey, department, CacheKeys.UsersTtl);

        return department;
    }

    public async Task<IReadOnlyList<CurrentUserProfile>> ListAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        // The payload depends on includeInactive, so the variant is part of the key.
        var cacheKey = CacheKeys.UsersList(includeInactive);

        if (_cache.TryGet<IReadOnlyList<CurrentUserProfile>>(cacheKey, out var cached) &&
            cached is not null)
        {
            return cached;
        }

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

        IReadOnlyList<CurrentUserProfile> profiles = users
            .Select(user => Map(user, rolesByUser.GetValueOrDefault(user.Id, [])))
            .ToList();

        _cache.Set(cacheKey, profiles, CacheKeys.UsersTtl);

        return profiles;
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
        var row = await LoadUserWithRolesAsync(userId, cancellationToken);

        if (row is null)
        {
            return ServiceResult<CurrentUserProfile>.Fail(404, "User profile was not found.");
        }

        var user = row.Value.User;

        var validationError = ApplyOwnProfileChanges(user, command);
        if (validationError is not null)
        {
            return ServiceResult<CurrentUserProfile>.Fail(400, validationError);
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _cache.RemoveByPrefix(CacheKeys.UsersPrefix);

        // Роли получены первым запросом, поэтому ответ собирается без чтения БД.
        return ServiceResult<CurrentUserProfile>.Ok(Map(user, row.Value.Roles));
    }

    public async Task<ServiceResult<CurrentUserProfile>> AdminUpdateAsync(
        Guid targetUserId,
        AdminUpdateUserCommand command,
        CancellationToken cancellationToken)
    {
        var row = await LoadUserWithRolesAsync(targetUserId, cancellationToken);

        if (row is null)
        {
            return ServiceResult<CurrentUserProfile>.Fail(404, "User profile was not found.");
        }

        var user = row.Value.User;

        var validationError = ApplyAdminProfileChanges(user, command);
        if (validationError is not null)
        {
            return ServiceResult<CurrentUserProfile>.Fail(400, validationError);
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _cache.RemoveByPrefix(CacheKeys.UsersPrefix);

        // Роли получены первым запросом, поэтому ответ собирается без чтения БД.
        return ServiceResult<CurrentUserProfile>.Ok(Map(user, row.Value.Roles));
    }

    /// <inheritdoc />
    public CurrentUserProfile BuildProfile(
        User user,
        IEnumerable<string> roleCodes)
    {
        return Map(user, roleCodes.Distinct().OrderBy(code => code).ToList());
    }

    /// <summary>
    /// Загружает пользователя вместе с кодами его ролей одним запросом.
    /// Пользователь остаётся отслеживаемым, поэтому последующий SaveChanges
    /// запишет изменения профиля, а роли попадают в ответ без второго чтения.
    /// </summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Пользователь и его роли или <c>null</c>, если пользователь не найден.</returns>
    private async Task<(User User, List<string> Roles)?> LoadUserWithRolesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var row = await _dbContext.Users
            .Where(user => user.Id == userId)
            .Select(user => new
            {
                User = user,
                Roles = _dbContext.UserRoles
                    .Where(userRole => userRole.UserId == user.Id)
                    .Select(userRole => userRole.Role.Code)
                    .Distinct()
                    .OrderBy(code => code)
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        if (_dbContext.Entry(row.User).State == EntityState.Detached)
        {
            // Страховка: без отслеживания изменения профиля не попали бы в SaveChanges.
            _dbContext.Users.Attach(row.User);
        }

        return (row.User, row.Roles);
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

    public async Task<ServiceResult<CurrentUserProfile>> CreateEmployeeAsync(
        CreateEmployeeCommand command,
    CancellationToken cancellationToken)
    {
        var supabaseUrl = _configuration["Supabase:ProjectUrl"];
        var anonKey = _configuration["Supabase:AnonKey"];

        if (string.IsNullOrWhiteSpace(supabaseUrl) ||
            string.IsNullOrWhiteSpace(anonKey))
        {
            return ServiceResult<CurrentUserProfile>.Fail(
                500, "Supabase configuration is missing.");
        }

        var email = command.Email.Trim();
        var password = command.Password;

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(command.FirstName) ||
            string.IsNullOrWhiteSpace(command.LastName) ||
            string.IsNullOrWhiteSpace(command.Department) ||
            string.IsNullOrWhiteSpace(command.Position))
        {
            return ServiceResult<CurrentUserProfile>.Fail(
                400, "Required employee fields are missing.");
        }

        var client = _httpClientFactory.CreateClient(SupabaseHttpClient.Name);
        client.DefaultRequestHeaders.TryAddWithoutValidation("apikey", anonKey);

        HttpResponseMessage response;

        try
        {
            response = await client.PostAsJsonAsync(
                $"{supabaseUrl.TrimEnd('/')}/auth/v1/signup",
                new
                {
                    email,
                    password,
                    data = new
                    {
                        first_name = command.FirstName.Trim(),
                        last_name = command.LastName.Trim(),
                        middle_name = command.MiddleName?.Trim(),
                        department = command.Department.Trim(),
                        position = command.Position.Trim()
                    }
                },
                cancellationToken);
        }
        catch (Exception exception) when (SupabaseHttpClient.IsUnavailable(exception, cancellationToken))
        {
            // Supabase недоступен: это ошибка внешнего сервиса, а не 500.
            return ServiceResult<CurrentUserProfile>.Fail(
                503, "Authentication service is temporarily unavailable.");
        }

        using var responseScope = response;

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(
                cancellationToken);

            return ServiceResult<CurrentUserProfile>.Fail(
                (int)response.StatusCode,
                $"Supabase error: {errorBody}");
        }

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var root = document.RootElement;

        var authUser = root.TryGetProperty("user", out var nestedUser) &&
                       nestedUser.ValueKind == JsonValueKind.Object
            ? nestedUser
            : root;

        if (!authUser.TryGetProperty("id", out var idElement) ||
            !Guid.TryParse(idElement.GetString(), out var userId))
        {
            return ServiceResult<CurrentUserProfile>.Fail(
                502, "Supabase did not return a valid user ID.");
        }

        var row = await LoadUserWithRolesAsync(userId, cancellationToken);

        if (row is null)
        {
            return ServiceResult<CurrentUserProfile>.Fail(
                500,
                "User profile was not created by the Supabase trigger.");
        }

        var user = row.Value.User;

        user.FirstName = command.FirstName.Trim();
        user.LastName = command.LastName.Trim();
        user.MiddleName = string.IsNullOrWhiteSpace(command.MiddleName)
            ? null
            : command.MiddleName.Trim();
        user.Department = command.Department.Trim();
        user.Position = command.Position.Trim();
        user.IsActive = true;
        user.MustChangePassword = true;
        user.UpdatedAt = DateTime.UtcNow;


        await _dbContext.SaveChangesAsync(cancellationToken);

        _cache.RemoveByPrefix(CacheKeys.UsersPrefix);

        return ServiceResult<CurrentUserProfile>.Ok(Map(user, row.Value.Roles));
    }
}
