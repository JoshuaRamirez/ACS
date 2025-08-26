using ACS.Service.Domain;
using ACS.WebApi.Resources;

namespace ACS.WebApi.Mapping;

/// <summary>
/// Extension methods for mapping REST resources to domain objects
/// Clean, composable mappings without AutoMapper dependencies
/// </summary>
public static class ResourceToDomainExtensions
{
    /// <summary>
    /// Maps CreateUserResource to domain User
    /// </summary>
    public static User ToDomain(this CreateUserResource resource)
    {
        return new User
        {
            Name = resource.Name
            // ID will be assigned by the domain service
        };
    }

    /// <summary>
    /// Maps UpdateUserResource to domain User (for updates)
    /// </summary>
    public static User ToDomain(this UpdateUserResource resource, int id)
    {
        return new User
        {
            Id = id,
            Name = resource.Name
        };
    }

    /// <summary>
    /// Maps CreateGroupResource to domain Group
    /// </summary>
    public static Group ToDomain(this CreateGroupResource resource)
    {
        return new Group
        {
            Name = resource.Name
            // ID will be assigned by the domain service
        };
    }

    /// <summary>
    /// Maps UpdateGroupResource to domain Group (for updates)
    /// </summary>
    public static Group ToDomain(this UpdateGroupResource resource, int id)
    {
        return new Group
        {
            Id = id,
            Name = resource.Name
        };
    }

    /// <summary>
    /// Maps CreateRoleResource to domain Role
    /// </summary>
    public static Role ToDomain(this CreateRoleResource resource)
    {
        return new Role
        {
            Name = resource.Name
            // ID will be assigned by the domain service
        };
    }

    /// <summary>
    /// Maps UpdateRoleResource to domain Role (for updates)
    /// </summary>
    public static Role ToDomain(this UpdateRoleResource resource, int id)
    {
        return new Role
        {
            Id = id,
            Name = resource.Name
        };
    }

    /// <summary>
    /// Applies patch operations to existing domain User
    /// Only updates non-null fields
    /// </summary>
    public static User ApplyPatch(this User user, PatchUserResource patch)
    {
        if (!string.IsNullOrEmpty(patch.Name))
        {
            user.ChangeName(patch.Name, patch.UpdatedBy);
        }
        return user;
    }

    /// <summary>
    /// Applies patch operations to existing domain Group
    /// Only updates non-null fields
    /// </summary>
    public static Group ApplyPatch(this Group group, PatchGroupResource patch)
    {
        if (!string.IsNullOrEmpty(patch.Name))
        {
            // Use domain method when available, for now direct assignment
            group.Name = patch.Name;
        }
        return group;
    }

    /// <summary>
    /// Applies patch operations to existing domain Role
    /// Only updates non-null fields
    /// </summary>
    public static Role ApplyPatch(this Role role, PatchRoleResource patch)
    {
        if (!string.IsNullOrEmpty(patch.Name))
        {
            role.ChangeName(patch.Name, patch.UpdatedBy);
        }
        return role;
    }

    /// <summary>
    /// Maps collection of CreateUserResources to domain Users
    /// LINQ-style composable collection mapper
    /// </summary>
    public static IEnumerable<User> ToDomainUsers(this IEnumerable<CreateUserResource> resources)
    {
        return resources.Select(r => r.ToDomain());
    }

    /// <summary>
    /// Maps collection of CreateGroupResources to domain Groups
    /// LINQ-style composable collection mapper
    /// </summary>
    public static IEnumerable<Group> ToDomainGroups(this IEnumerable<CreateGroupResource> resources)
    {
        return resources.Select(r => r.ToDomain());
    }

    /// <summary>
    /// Maps collection of CreateRoleResources to domain Roles
    /// LINQ-style composable collection mapper
    /// </summary>
    public static IEnumerable<Role> ToDomainRoles(this IEnumerable<CreateRoleResource> resources)
    {
        return resources.Select(r => r.ToDomain());
    }

    /// <summary>
    /// Generic bulk operation mapper for domain objects
    /// Composable with LINQ operations
    /// </summary>
    public static IEnumerable<T> ToBulkDomain<TResource, T>(
        this BulkOperationResource<TResource> bulkResource,
        Func<TResource, T> mapper)
    {
        return bulkResource.Items.Select(mapper);
    }
}