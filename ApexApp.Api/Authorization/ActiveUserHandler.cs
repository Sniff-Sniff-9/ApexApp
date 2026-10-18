using Microsoft.AspNetCore.Authorization;

namespace ApexApp.Api.Authorization;

public sealed class ActiveUserHandler : AuthorizationHandler<ActiveUserRequirement>
{
    public const string IsActiveClaim = "is_active";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveUserRequirement requirement)
    {
        var isActive = context.User.FindFirst(IsActiveClaim)?.Value;
        if (string.Equals(isActive, "true", StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
