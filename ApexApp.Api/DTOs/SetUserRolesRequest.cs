namespace ApexApp.Api.DTOs;

public sealed class SetUserRolesRequest
{
    public List<string> RoleCodes { get; set; } = [];
}
