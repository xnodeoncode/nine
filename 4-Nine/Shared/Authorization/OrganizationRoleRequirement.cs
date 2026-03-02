using Microsoft.AspNetCore.Authorization;

namespace Nine.Shared.Authorization;

/// <summary>
/// Authorization requirement for organization role checking.
/// </summary>
public class OrganizationRoleRequirement : IAuthorizationRequirement
{
    public string[] AllowedRoles { get; }

    public OrganizationRoleRequirement(params string[] allowedRoles)
    {
        AllowedRoles = allowedRoles;
    }
}
