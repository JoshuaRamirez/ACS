using System;
using System.Collections.Generic;
using ACS.WebResources.Groups;
using ACS.WebResources.Roles;

namespace ACS.WebResources.Users;

public class UserResource
{
    public int Id { get; set; }
    public string Name { get; set; }
    public IReadOnlyCollection<GroupResource> GroupMemberships { get; init; } = Array.Empty<GroupResource>();
    public IReadOnlyCollection<RoleResource> RoleMemberships { get; init; } = Array.Empty<RoleResource>();
}
