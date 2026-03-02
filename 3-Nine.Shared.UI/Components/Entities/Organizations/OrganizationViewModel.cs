using Nine.Core.Entities;
using Nine.Shared.UI.Components.Entities.OrganizationUsers;

namespace Nine.Shared.UI.Components.Entities.Organizations;

public class OrganizationViewModel
{
    public Organization? Organization { get; set; }
    public OrganizationUserViewModel? OrganizationOwner { get; set; }
    public List<OrganizationUserViewModel> OrganizationUsers { get; set; } = new();
}