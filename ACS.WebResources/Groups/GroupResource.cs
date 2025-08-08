using System;
using System.Collections.Generic;
using ACS.WebResources.Users;
using ACS.WebResources.Roles;

namespace ACS.WebResources.Groups;

public class GroupResource
{
    public int Id { get; set; }
    public string Name { get; set; }
    public IReadOnlyCollection<GroupResource> Groups { get; init; } = Array.Empty<GroupResource>();
    public IReadOnlyCollection<GroupResource> ParentGroups { get; init; } = Array.Empty<GroupResource>();
    public IReadOnlyCollection<UserResource> Users { get; init; } = Array.Empty<UserResource>();
    public IReadOnlyCollection<RoleResource> Roles { get; init; } = Array.Empty<RoleResource>();
}
