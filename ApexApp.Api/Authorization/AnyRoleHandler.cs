using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace ApexApp.Api.Authorization;

public sealed class AnyRoleHandler : AuthorizationHandler<AnyRoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AnyRoleRequirement requirement)
    {
        var roles = context.User.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (requirement.RoleCodes.Any(role => roles.Contains(role)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
