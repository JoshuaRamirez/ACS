using System;
using System.Collections.Generic;
using ACS.WebResources.Groups;
using ACS.WebResources.Users;

namespace ACS.WebResources.Roles;

public class RoleResource
{
    public int Id { get; set; }
    public string Name { get; set; }
    public IReadOnlyCollection<GroupResource> GroupMemberships { get; init; } = Array.Empty<GroupResource>();
    public IReadOnlyCollection<UserResource> Users { get; init; } = Array.Empty<UserResource>();
}
