using ACS.Service.Domain;
using ACS.WebApi.Models.Users;
using ACS.WebResources.Groups;
using ACS.WebResources.Roles;
using ACS.WebResources.Users;

namespace ACS.WebApi.Mapping;

internal static class UserMappings
{
    public static User ToDomain(this CreateUserRequest request)
    {
        var user = new User
        {
            Name = request.User.Name,
        };

        // Group and role memberships are omitted until corresponding
        // services are implemented.
        return user;
    }

    public static UserResource ToResource(this User user)
    {
        return new UserResource
        {
            Id = user.Id,
            Name = user.Name,
            GroupMemberships = user.GroupMemberships
                .Select(g => new GroupResource { Id = g.Id, Name = g.Name })
                .ToList(),
            RoleMemberships = user.RoleMemberships
                .Select(r => new RoleResource { Id = r.Id, Name = r.Name })
                .ToList()
        };
    }
}

