using ACS.Service.Infrastructure;
using ACS.Service.Services;

namespace ACS.VerticalHost.Tests;

// Shared test command classes for all integration tests
public class TestCreateUserCommand : DomainCommand<TestUser>
{
    public string Name { get; set; } = null!;
    public int? GroupId { get; set; }
    public int? RoleId { get; set; }
    public string? Metadata { get; set; } // For large payload testing
}

public class TestCreateGroupCommand : DomainCommand<TestGroup>
{
    public string Name { get; set; } = null!;
    public int? ParentGroupId { get; set; }
}

public class TestCreateRoleCommand : DomainCommand<TestRole>
{
    public string Name { get; set; } = null!;
    public int? GroupId { get; set; }
}

public class TestDeleteUserCommand : DomainCommand<bool>
{
    public int UserId { get; set; }
}

public class TestGetUsersCommand : DomainCommand<List<TestUser>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

// Test result classes
public class TestUser
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}

public class TestGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}

public class TestRole
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}

// DTOs for end-to-end testing
public class CreateUserDto
{
    public string Name { get; set; } = null!;
    public int? GroupId { get; set; }
}

public class CreateGroupDto
{
    public string Name { get; set; } = null!;
    public int? ParentGroupId { get; set; }
}

public class CreateRoleDto
{
    public string Name { get; set; } = null!;
    public int? GroupId { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}

public class GroupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}

public class RoleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}

public class AddUserToGroupDto
{
    public int UserId { get; set; }
    public int GroupId { get; set; }
}