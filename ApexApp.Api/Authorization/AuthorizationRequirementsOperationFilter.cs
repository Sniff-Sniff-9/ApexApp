
using ApexApp.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ApexApp.Api.Swagger;

public sealed class AuthorizationRequirementsOperationFilter(
    IAuthorizationPolicyProvider policyProvider) : IOperationFilter
{
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;

        if (metadata.OfType<IAllowAnonymous>().Any())
        {
            return;
        }

        var authorizeData = metadata.OfType<IAuthorizeData>().ToList();

        if (authorizeData.Count == 0)
        {
            return;
        }

        var policies = new List<AuthorizationPolicy>();

        foreach (var data in authorizeData)
        {
            AuthorizationPolicy? policy = null;

            if (!string.IsNullOrWhiteSpace(data.Policy))
            {
                policy = policyProvider.GetPolicyAsync(data.Policy)
                    .GetAwaiter()
                    .GetResult();
            }
            else if (!string.IsNullOrWhiteSpace(data.Roles))
            {
                var roles = data.Roles
                    .Split(',', StringSplitOptions.TrimEntries |
                                StringSplitOptions.RemoveEmptyEntries);

                policy = new AuthorizationPolicyBuilder()
                    .RequireRole(roles)
                    .Build();
            }
            else
            {
                policy = policyProvider.GetDefaultPolicyAsync()
                    .GetAwaiter()
                    .GetResult();
            }

            if (policy is not null)
            {
                policies.Add(policy);
            }
        }

        if (policies.Count == 0)
        {
            return;
        }

        var roleRequirements = policies
            .SelectMany(p => p.Requirements)
            .OfType<AnyRoleRequirement>()
            .SelectMany(r => r.RoleCodes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var descriptions = new List<string>
        {
            "Требуется аутентификация."
        };

        if (roleRequirements.Length > 0)
        {
            descriptions.Add(
                $"**Требуемая роль:** {string.Join(" или ", roleRequirements)}.");
        }

        if (policies.SelectMany(p => p.Requirements)
            .OfType<ActiveUserRequirement>().Any())
        {
            descriptions.Add("Пользователь должен быть активен.");
        }

        var text = string.Join(" ", descriptions);

        operation.Description = string.IsNullOrWhiteSpace(operation.Description)
            ? text
            : $"{operation.Description}\n\n{text}";
    }
}
