using ACS.Service.Domain;
using ACS.WebApi.Models.Roles;
using ACS.WebResources.Groups;
using ACS.WebResources.Roles;
using ACS.WebResources.Users;

namespace ACS.WebApi.Mapping;

internal static class RoleMappings
{
    public static Role ToDomain(this CreateRoleRequest request)
    {
        var role = new Role
        {
            Name = request.Role.Name,
        };

        // Memberships are omitted until corresponding services are implemented.
        return role;
    }

    public static RoleResource ToResource(this Role role)
    {
        return new RoleResource
        {
            Id = role.Id,
            Name = role.Name,
            GroupMemberships = role.GroupMemberships
                .Select(g => new GroupResource { Id = g.Id, Name = g.Name })
                .ToList(),
            Users = role.Users
                .Select(u => new UserResource { Id = u.Id, Name = u.Name })
                .ToList()
        };
    }
}
