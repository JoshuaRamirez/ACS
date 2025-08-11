using ACS.Service.Domain;
using ACS.WebApi.Models.Groups;
using ACS.WebResources.Groups;
using ACS.WebResources.Users;
using ACS.WebResources.Roles;

namespace ACS.WebApi.Mapping;

internal static class GroupMappings
{
    public static Group ToDomain(this CreateGroupRequest request)
    {
        var group = new Group
        {
            Name = request.Group.Name,
        };

        // Memberships are omitted until corresponding services are implemented.
        return group;
    }

    public static GroupResource ToResource(this Group group)
    {
        return new GroupResource
        {
            Id = group.Id,
            Name = group.Name,
            Groups = group.Groups
                .Select(g => new GroupResource { Id = g.Id, Name = g.Name })
                .ToList(),
            ParentGroups = group.ParentGroups
                .Select(g => new GroupResource { Id = g.Id, Name = g.Name })
                .ToList(),
            Users = group.Users
                .Select(u => new UserResource { Id = u.Id, Name = u.Name })
                .ToList(),
            Roles = group.Roles
                .Select(r => new RoleResource { Id = r.Id, Name = r.Name })
                .ToList()
        };
    }
}
