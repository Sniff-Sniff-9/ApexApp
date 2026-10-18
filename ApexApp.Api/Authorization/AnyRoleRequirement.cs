using Microsoft.AspNetCore.Authorization;

namespace ApexApp.Api.Authorization;

public sealed class AnyRoleRequirement : IAuthorizationRequirement
{
    public AnyRoleRequirement(params string[] roleCodes)
    {
        RoleCodes = roleCodes;
    }

    public IReadOnlyList<string> RoleCodes { get; }
}
