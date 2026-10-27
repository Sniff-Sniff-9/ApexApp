namespace ApexApp.Infrastructure.Auth;

public static class RoleCodes
{
    public const string Employee = "employee";
    public const string Manager = "manager";
    public const string Admin = "admin";
    public const string Support = "support";
    public const string SuperAdmin = "superadmin";
    public const string  HumanResources = "humanresources";
    public const string Merchandiser = "merchandiser";

    public static readonly string[] AdminPanelRoles =
    [
        Manager,
        Merchandiser,
        HumanResources,
        Admin,
        SuperAdmin
    ];

    public static bool CanAccessAdminPanel(IEnumerable<string> roles)
    {
        return roles.Any(role => AdminPanelRoles.Contains(role, StringComparer.OrdinalIgnoreCase));
    }
}
