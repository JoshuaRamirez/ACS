using ACS.WebApi.DTOs;
using ACS.Service.Requests;
using ACS.Service.Data.Models;

namespace ACS.WebApi.Tests.Integration.Infrastructure;

/// <summary>
/// Builder class for creating test data objects with fluent interface
/// </summary>
public class TestDataBuilder
{
    /// <summary>
    /// Creates a UserBuilder for building User test objects
    /// </summary>
    public static UserBuilder User() => new();

    /// <summary>
    /// Creates a GroupBuilder for building Group test objects
    /// </summary>
    public static GroupBuilder Group() => new();

    /// <summary>
    /// Creates a RoleBuilder for building Role test objects
    /// </summary>
    public static RoleBuilder Role() => new();

    /// <summary>
    /// Creates an EntityBuilder for building Entity test objects
    /// </summary>
    public static EntityBuilder Entity() => new();

    /// <summary>
    /// Creates a CreateUserRequestBuilder for building CreateUserRequest test objects
    /// </summary>
    public static CreateUserRequestBuilder CreateUserRequest() => new();

    /// <summary>
    /// Creates an UpdateUserRequestBuilder for building UpdateUserRequest test objects
    /// </summary>
    public static UpdateUserRequestBuilder UpdateUserRequest() => new();

    /// <summary>
    /// Creates a CreateGroupRequestBuilder for building CreateGroupRequest test objects
    /// </summary>
    public static CreateGroupRequestBuilder CreateGroupRequest() => new();

    /// <summary>
    /// Creates a CheckPermissionRequestBuilder for building CheckPermissionRequest test objects
    /// </summary>
    public static CheckPermissionRequestBuilder CheckPermissionRequest() => new();
}

/// <summary>
/// Builder for User objects
/// </summary>
public class UserBuilder
{
    private readonly User _user = new()
    {
        Id = 1,
        Name = "Test User",
        Email = "test@example.com",
        PasswordHash = "hashedpassword",
        IsActive = true,
        EntityId = 1,
        CreatedAt = DateTime.UtcNow.AddDays(-1),
        UpdatedAt = DateTime.UtcNow
    };

    public UserBuilder WithId(int id)
    {
        _user.Id = id;
        return this;
    }

    public UserBuilder WithName(string name)
    {
        _user.Name = name;
        return this;
    }

    public UserBuilder WithEmail(string email)
    {
        _user.Email = email;
        return this;
    }

    public UserBuilder WithEntityId(int entityId)
    {
        _user.EntityId = entityId;
        return this;
    }

    public UserBuilder Inactive()
    {
        _user.IsActive = false;
        return this;
    }

    public UserBuilder CreatedDaysAgo(int days)
    {
        _user.CreatedAt = DateTime.UtcNow.AddDays(-days);
        return this;
    }

    public User Build() => _user;
}

/// <summary>
/// Builder for Group objects
/// </summary>
public class GroupBuilder
{
    private readonly Group _group = new()
    {
        Id = 1,
        Name = "Test Group",
        EntityId = 1,
        CreatedAt = DateTime.UtcNow.AddDays(-1),
        UpdatedAt = DateTime.UtcNow
    };

    public GroupBuilder WithId(int id)
    {
        _group.Id = id;
        return this;
    }

    public GroupBuilder WithName(string name)
    {
        _group.Name = name;
        return this;
    }

    public GroupBuilder WithEntityId(int entityId)
    {
        _group.EntityId = entityId;
        return this;
    }

    public GroupBuilder CreatedDaysAgo(int days)
    {
        _group.CreatedAt = DateTime.UtcNow.AddDays(-days);
        return this;
    }

    public Group Build() => _group;
}

/// <summary>
/// Builder for Role objects
/// </summary>
public class RoleBuilder
{
    private readonly Role _role = new()
    {
        Id = 1,
        Name = "Test Role",
        EntityId = 1,
        CreatedAt = DateTime.UtcNow.AddDays(-1),
        UpdatedAt = DateTime.UtcNow
    };

    public RoleBuilder WithId(int id)
    {
        _role.Id = id;
        return this;
    }

    public RoleBuilder WithName(string name)
    {
        _role.Name = name;
        return this;
    }

    public RoleBuilder WithEntityId(int entityId)
    {
        _role.EntityId = entityId;
        return this;
    }

    public RoleBuilder CreatedDaysAgo(int days)
    {
        _role.CreatedAt = DateTime.UtcNow.AddDays(-days);
        return this;
    }

    public Role Build() => _role;
}

/// <summary>
/// Builder for Entity objects
/// </summary>
public class EntityBuilder
{
    private readonly Entity _entity = new()
    {
        Id = 1,
        EntityType = "User",
        CreatedAt = DateTime.UtcNow.AddDays(-1),
        UpdatedAt = DateTime.UtcNow
    };

    public EntityBuilder WithId(int id)
    {
        _entity.Id = id;
        return this;
    }

    public EntityBuilder WithType(string entityType)
    {
        _entity.EntityType = entityType;
        return this;
    }

    public EntityBuilder AsUser() => WithType("User");
    public EntityBuilder AsGroup() => WithType("Group");
    public EntityBuilder AsRole() => WithType("Role");

    public EntityBuilder CreatedDaysAgo(int days)
    {
        _entity.CreatedAt = DateTime.UtcNow.AddDays(-days);
        return this;
    }

    public Entity Build() => _entity;
}

/// <summary>
/// Builder for CreateUserRequest DTOs
/// </summary>
public class CreateUserRequestBuilder
{
    private string _name = "Test User";
    private int? _initialGroupId;
    private int? _initialRoleId;
    private string _createdBy = "test-user";

    public CreateUserRequestBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public CreateUserRequestBuilder WithInitialGroupId(int groupId)
    {
        _initialGroupId = groupId;
        return this;
    }

    public CreateUserRequestBuilder WithInitialRoleId(int roleId)
    {
        _initialRoleId = roleId;
        return this;
    }

    public CreateUserRequestBuilder WithCreatedBy(string createdBy)
    {
        _createdBy = createdBy;
        return this;
    }

    public ACS.Service.Requests.CreateUserRequest Build() => new()
    {
        Name = _name,
        InitialGroupId = _initialGroupId,
        InitialRoleId = _initialRoleId,
        CreatedBy = _createdBy
    };
}

/// <summary>
/// Builder for UpdateUserRequest DTOs
/// </summary>
public class UpdateUserRequestBuilder
{
    private int _userId = 1;
    private string _name = "Updated User";
    private string _updatedBy = "test-user";

    public UpdateUserRequestBuilder WithUserId(int userId)
    {
        _userId = userId;
        return this;
    }

    public UpdateUserRequestBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public UpdateUserRequestBuilder WithUpdatedBy(string updatedBy)
    {
        _updatedBy = updatedBy;
        return this;
    }

    public ACS.Service.Requests.UpdateUserRequest Build() => new()
    {
        UserId = _userId,
        Name = _name,
        UpdatedBy = _updatedBy
    };
}

/// <summary>
/// Builder for CreateGroupRequest DTOs
/// </summary>
public class CreateGroupRequestBuilder
{
    private string _name = "Test Group";
    private string? _description;
    private int? _parentGroupId;
    private string _createdBy = "test-user";

    public CreateGroupRequestBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public CreateGroupRequestBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public CreateGroupRequestBuilder WithParentGroupId(int parentGroupId)
    {
        _parentGroupId = parentGroupId;
        return this;
    }

    public CreateGroupRequestBuilder WithCreatedBy(string createdBy)
    {
        _createdBy = createdBy;
        return this;
    }

    public ACS.Service.Requests.CreateGroupRequest Build() => new()
    {
        Name = _name,
        Description = _description,
        ParentGroupId = _parentGroupId,
        CreatedBy = _createdBy
    };
}

/// <summary>
/// Builder for CheckPermissionRequest DTOs
/// </summary>
public class CheckPermissionRequestBuilder
{
    private int _entityId = 1;
    private int _permissionId = 1;
    private int? _resourceId = null;

    public CheckPermissionRequestBuilder ForEntity(int entityId)
    {
        _entityId = entityId;
        return this;
    }

    public CheckPermissionRequestBuilder WithPermission(int permissionId)
    {
        _permissionId = permissionId;
        return this;
    }

    public CheckPermissionRequestBuilder ForResource(int? resourceId)
    {
        _resourceId = resourceId;
        return this;
    }

    public ACS.Service.Requests.CheckPermissionRequest Build() => new()
    {
        EntityId = _entityId,
        EntityType = "User", // Default for tests
        PermissionId = _permissionId,
        ResourceId = _resourceId,
        RequestedBy = "test-user"
    };
}