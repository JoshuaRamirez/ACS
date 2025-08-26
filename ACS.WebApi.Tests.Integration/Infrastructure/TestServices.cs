using ACS.Infrastructure.Services;
using ACS.WebApi.Services;
using ACS.Service.Infrastructure;
using ACS.WebApi.DTOs;
using Grpc.Net.Client;

namespace ACS.WebApi.Tests.Integration.Infrastructure;

/// <summary>
/// Test implementation of ITenantContextService for integration tests
/// </summary>
public class TestTenantContextService : ACS.Infrastructure.Services.ITenantContextService
{
    public string? GetTenantId() => "test-tenant";
    public string GetRequiredTenantId() => GetTenantId() ?? throw new InvalidOperationException("Tenant ID not available");
    public TenantProcessInfo? GetTenantProcessInfo() => new TenantProcessInfo
    {
        TenantId = "test-tenant",
        ProcessId = 12345,
        GrpcEndpoint = "https://localhost:5001",
        IsHealthy = true,
        StartTime = DateTime.UtcNow.AddMinutes(-10),
        LastHealthCheck = DateTime.UtcNow
    };
    public GrpcChannel? GetGrpcChannel() => null;
    public void SetTenantContext(string tenantId, TenantProcessInfo? processInfo = null, GrpcChannel? grpcChannel = null) { }
    public void ClearTenantContext() { }
    public Task<bool> ValidateTenantAccessAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
}

/// <summary>
/// Test implementation of IUserContextService for integration tests
/// </summary>
public class TestUserContextService : ACS.Infrastructure.Services.IUserContextService
{
    public string GetCurrentUserId() => "test-user-123";
    public string GetCurrentUserName() => "Test User";
    public string GetTenantId() => "test-tenant";
    public bool IsAuthenticated() => true;
    public IEnumerable<string> GetUserRoles() => new[] { "Admin", "User" };
    public bool HasRole(string role) => GetUserRoles().Contains(role);
    public string? GetUserEmail() => "testuser@example.com";
    public string? GetClaim(string claimType) => claimType == "email" ? GetUserEmail() : null;
    public IEnumerable<(string Type, string Value)> GetAllClaims() => new[] 
    { 
        ("sub", GetCurrentUserId()), 
        ("name", GetCurrentUserName()), 
        ("email", GetUserEmail() ?? ""),
        ("tenant_id", GetTenantId())
    };
}

/// <summary>
/// Test implementation of TenantGrpcClientService that returns mock data instead of making gRPC calls
/// </summary>
public class TestTenantGrpcClientService : TenantGrpcClientService
{
    public TestTenantGrpcClientService() : base(null!, null!, null!, null!, null!, null!)
    {
    }

    // Override methods to return test data instead of making gRPC calls
    
    public new async Task<ApiResponse<UserListResponse>> GetUsersAsync(GetUsersCommand command)
    {
        await Task.Delay(1); // Simulate async operation
        
        var users = new List<UserResponse>
        {
            new UserResponse(1, "John Doe", 1, "Administrators", 1, "Admin", 
                new List<PermissionResponse>(), DateTime.UtcNow.AddDays(-30), DateTime.UtcNow),
            new UserResponse(2, "Jane Smith", 2, "Users", 2, "User", 
                new List<PermissionResponse>(), DateTime.UtcNow.AddDays(-15), DateTime.UtcNow)
        };

        var response = new UserListResponse(users, users.Count, command.Page, command.PageSize);
        return new ApiResponse<UserListResponse>(true, response, "Success");
    }

    public new async Task<ApiResponse<UserResponse>> GetUserAsync(GetUserCommand command)
    {
        await Task.Delay(1);
        
        if (command.UserId == "999") // Test case for not found
        {
            return new ApiResponse<UserResponse>(false, null, "User not found");
        }

        var user = new UserResponse(int.TryParse(command.UserId, out int userId) ? userId : 1, "Test User", 1, "Administrators", 1, "Admin",
            new List<PermissionResponse>(), DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        
        return new ApiResponse<UserResponse>(true, user, "Success");
    }

    public new async Task<ApiResponse<UserResponse>> CreateUserAsync(CreateUserCommand command)
    {
        await Task.Delay(1);
        
        var user = new UserResponse(99, command.Name, null, null, null, null,
            new List<PermissionResponse>(), DateTime.UtcNow, null);
        
        return new ApiResponse<UserResponse>(true, user, "User created successfully");
    }

    public new async Task<ApiResponse<UserResponse>> UpdateUserAsync(UpdateUserCommand command)
    {
        await Task.Delay(1);
        
        if (command.UserId == "999")
        {
            return new ApiResponse<UserResponse>(false, null, "User not found");
        }

        var user = new UserResponse(int.TryParse(command.UserId, out int updateUserId) ? updateUserId : 1, command.Name, null, null, null, null,
            new List<PermissionResponse>(), DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        
        return new ApiResponse<UserResponse>(true, user, "User updated successfully");
    }

    public new async Task<ApiResponse<bool>> DeleteUserAsync(DeleteUserCommand command)
    {
        await Task.Delay(1);
        
        if (command.UserId == "999")
        {
            return new ApiResponse<bool>(false, false, "User not found");
        }

        return new ApiResponse<bool>(true, true, "User deleted successfully");
    }

    public new async Task<ApiResponse<bool>> AddUserToGroupAsync(AddUserToGroupRequest request)
    {
        await Task.Delay(1);
        
        if (request.UserId == 999 || request.GroupId == 999)
        {
            return new ApiResponse<bool>(false, false, "User or Group not found");
        }

        return new ApiResponse<bool>(true, true, "User added to group successfully");
    }

    public new async Task<ApiResponse<bool>> AssignUserToRoleAsync(AssignUserToRoleRequest request)
    {
        await Task.Delay(1);
        
        if (request.UserId == 999 || request.RoleId == 999)
        {
            return new ApiResponse<bool>(false, false, "User or Role not found");
        }

        return new ApiResponse<bool>(true, true, "User assigned to role successfully");
    }

    public new async Task<ApiResponse<GroupListResponse>> GetGroupsAsync(GetGroupsCommand command)
    {
        await Task.Delay(1);
        
        var groups = new List<GroupResponse>
        {
            new GroupResponse(1, "Administrators", null, null, new List<GroupResponse>(),
                new List<UserResponse>(), new List<RoleResponse>(), new List<PermissionResponse>(),
                DateTime.UtcNow.AddDays(-60), DateTime.UtcNow),
            new GroupResponse(2, "Users", 1, "Administrators", new List<GroupResponse>(),
                new List<UserResponse>(), new List<RoleResponse>(), new List<PermissionResponse>(),
                DateTime.UtcNow.AddDays(-45), DateTime.UtcNow)
        };

        var response = new GroupListResponse(groups, groups.Count, command.Page, command.PageSize);
        return new ApiResponse<GroupListResponse>(true, response, "Success");
    }

    public new async Task<ApiResponse<GroupResponse>> GetGroupAsync(GetGroupCommand command)
    {
        await Task.Delay(1);
        
        if (command.GroupId == 999)
        {
            return new ApiResponse<GroupResponse>(false, null, "Group not found");
        }

        var group = new GroupResponse(command.GroupId, "Test Group", null, null,
            new List<GroupResponse>(), new List<UserResponse>(), new List<RoleResponse>(), 
            new List<PermissionResponse>(), DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        
        return new ApiResponse<GroupResponse>(true, group, "Success");
    }

    public new async Task<ApiResponse<CheckPermissionResponse>> CheckPermissionAsync(CheckPermissionRequest request)
    {
        await Task.Delay(1);
        
        // Simple test logic: grant access to admin entities or specific test URIs
        bool hasAccess = request.EntityId == 1 || request.Uri.Contains("test");
        
        var response = new CheckPermissionResponse(
            hasAccess, 
            request.Uri, 
            request.HttpVerb, 
            request.EntityId, 
            "User", // Assume entity type is User for tests
            hasAccess ? "Access granted" : "Access denied");
        return new ApiResponse<CheckPermissionResponse>(true, response, "Permission check completed");
    }

    public new async Task<ApiResponse<GroupResponse>> CreateGroupAsync(CreateGroupCommand command)
    {
        await Task.Delay(1);
        
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return new ApiResponse<GroupResponse>(false, null, "Group name is required");
        }

        if (command.ParentGroupId.HasValue && command.ParentGroupId == 999)
        {
            return new ApiResponse<GroupResponse>(false, null, "Parent group not found");
        }

        var group = new GroupResponse(99, command.Name, command.ParentGroupId, null,
            new List<GroupResponse>(), new List<UserResponse>(), new List<RoleResponse>(), 
            new List<PermissionResponse>(), DateTime.UtcNow, null);
        
        return new ApiResponse<GroupResponse>(true, group, "Group created successfully");
    }

    public new async Task<ApiResponse<GroupResponse>> UpdateGroupAsync(UpdateGroupCommand command)
    {
        await Task.Delay(1);
        
        if (command.GroupId == 999)
        {
            return new ApiResponse<GroupResponse>(false, null, "Group not found");
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return new ApiResponse<GroupResponse>(false, null, "Group name is required");
        }

        var group = new GroupResponse(command.GroupId, command.Name, null, null,
            new List<GroupResponse>(), new List<UserResponse>(), new List<RoleResponse>(), 
            new List<PermissionResponse>(), DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        
        return new ApiResponse<GroupResponse>(true, group, "Group updated successfully");
    }

    public new async Task<ApiResponse<bool>> DeleteGroupAsync(DeleteGroupCommand command)
    {
        await Task.Delay(1);
        
        if (command.GroupId == 999)
        {
            return new ApiResponse<bool>(false, false, "Group not found");
        }

        return new ApiResponse<bool>(true, true, "Group deleted successfully");
    }

    public new async Task<ApiResponse<bool>> AddRoleToGroupAsync(AddRoleToGroupRequest request)
    {
        await Task.Delay(1);
        
        if (request.GroupId == 999 || request.RoleId == 999)
        {
            return new ApiResponse<bool>(false, false, "Group or Role not found");
        }

        return new ApiResponse<bool>(true, true, "Role added to group successfully");
    }

    public new async Task<ApiResponse<bool>> GrantPermissionAsync(GrantPermissionRequest request)
    {
        await Task.Delay(1);
        
        if (request.EntityId <= 0)
        {
            return new ApiResponse<bool>(false, false, "Entity ID must be greater than 0");
        }

        if (string.IsNullOrWhiteSpace(request.Uri))
        {
            return new ApiResponse<bool>(false, false, "URI is required");
        }

        if (string.IsNullOrWhiteSpace(request.HttpVerb))
        {
            return new ApiResponse<bool>(false, false, "HTTP verb is required");
        }

        if (request.EntityId == 999)
        {
            return new ApiResponse<bool>(false, false, "Entity not found");
        }

        return new ApiResponse<bool>(true, true, "Permission granted successfully");
    }

    public new async Task<ApiResponse<bool>> DenyPermissionAsync(DenyPermissionRequest request)
    {
        await Task.Delay(1);
        
        if (request.EntityId <= 0)
        {
            return new ApiResponse<bool>(false, false, "Entity ID must be greater than 0");
        }

        if (string.IsNullOrWhiteSpace(request.Uri))
        {
            return new ApiResponse<bool>(false, false, "URI is required");
        }

        if (string.IsNullOrWhiteSpace(request.HttpVerb))
        {
            return new ApiResponse<bool>(false, false, "HTTP verb is required");
        }

        if (request.EntityId == 999)
        {
            return new ApiResponse<bool>(false, false, "Entity not found");
        }

        return new ApiResponse<bool>(true, true, "Permission denied successfully");
    }

    public async Task<ApiResponse<PermissionListResponse>> GetEntityPermissionsAsync(int entityId)
    {
        await Task.Delay(1);
        
        if (entityId == 999)
        {
            return new ApiResponse<PermissionListResponse>(false, null, "Entity not found");
        }

        var permissions = new List<PermissionResponse>
        {
            new PermissionResponse(1, "/api/users", "GET", true, false, "ApiUriAuthorization"),
            new PermissionResponse(2, "/api/users", "POST", true, false, "ApiUriAuthorization"),
            new PermissionResponse(3, "/api/admin", "DELETE", false, true, "ApiUriAuthorization")
        };

        var response = new PermissionListResponse(permissions, permissions.Count, 1, 20);
        return new ApiResponse<PermissionListResponse>(true, response, "Success");
    }
}