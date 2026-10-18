using System.Security.Claims;
using ApexApp.Api.Extensions;
using ApexApp.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Api.Authorization;

public sealed class UserClaimsTransformation : IClaimsTransformation
{
    private const string TransformedClaim = "apex_transformed";
    private readonly AppDbContext _dbContext;

    public UserClaimsTransformation(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return principal;
        }

        if (principal.HasClaim(claim => claim.Type == TransformedClaim))
        {
            return principal;
        }

        identity.AddClaim(new Claim(TransformedClaim, "true"));

        var userId = principal.GetUserId();
        if (userId is null)
        {
            return principal;
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId.Value)
            .Select(u => new { u.IsActive })
            .FirstOrDefaultAsync();

        if (user is null)
        {
            identity.AddClaim(new Claim(ActiveUserHandler.IsActiveClaim, "false"));
            return principal;
        }

        identity.AddClaim(new Claim(ActiveUserHandler.IsActiveClaim, user.IsActive ? "true" : "false"));

        var roleCodes = await _dbContext.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.UserId == userId.Value)
            .Select(userRole => userRole.Role.Code)
            .Distinct()
            .ToListAsync();

        foreach (var roleCode in roleCodes)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, roleCode));
        }

        return principal;
    }
}
