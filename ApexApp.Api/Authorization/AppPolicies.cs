namespace ApexApp.Api.Authorization;

public static class AppPolicies
{
    public const string ActiveUser = "ActiveUser";
    public const string ManageUsers = "ManageUsers";
    public const string ManageOrg = "ManageOrg";
    public const string ViewTeam = "ViewTeam";
    public const string ManageTeam = "ManageTeam";
    public const string ManageRoles = "ManageRoles";
    public const string IssueBonus = "IssueBonus";
    public const string AdjustBalances = "AdjustBalances";
    public const string ViewAllTransactions = "ViewAllTransactions";
    public const string ManageActivities = "ManageActivities";
    public const string ModerateActivities = "ModerateActivities";
    public const string ManageSupport = "ManageSupport";
    public const string ManageOrders = "ManageOrders";
    public const string ManageCatalog = "ManageCatalog";

    /// <summary>Доступ к агрегированной аналитике платформы и отделов.</summary>
    public const string ViewAnalytics = "ViewAnalytics";

    /// <summary>Доступ к аналитике вознаграждений и каталога.</summary>
    public const string ViewRewardAnalytics = "ViewRewardAnalytics";

    /// <summary>Доступ к персональной аналитике сотрудников.</summary>
    public const string ViewEmployeeAnalytics = "ViewEmployeeAnalytics";

    /// <summary>Доступ к аналитике межотдельского взаимодействия.</summary>
    public const string ViewCollaborationAnalytics = "ViewCollaborationAnalytics";
}
