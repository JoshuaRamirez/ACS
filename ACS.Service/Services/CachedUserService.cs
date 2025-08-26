using ACS.Service.Domain;
using ACS.Service.Services;
using ACS.Service.Requests;
using ACS.Service.Responses;
using Microsoft.Extensions.Logging;

namespace ACS.Service.Services;

/// <summary>
/// Cached decorator for IUserService using cache-aside pattern
/// TEMPORARILY DISABLED due to missing infrastructure references
/// </summary>
public class CachedUserService : IUserService
{
    private readonly IUserService _userService;
    private readonly ILogger<CachedUserService> _logger;

    public CachedUserService(
        IUserService userService,
        ILogger<CachedUserService> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    // Query operations
    public async Task<UserResponse> GetByIdAsync(GetUserRequest request)
    {
        return await _userService.GetByIdAsync(request);
    }

    public async Task<UsersResponse> GetAllAsync(GetUsersRequest request)
    {
        return await _userService.GetAllAsync(request);
    }
    
    // Create operations
    public async Task<CreateUserResponse> CreateAsync(CreateUserRequest request)
    {
        return await _userService.CreateAsync(request);
    }
    
    // Update operations
    public async Task<UpdateUserResponse> UpdateAsync(UpdateUserRequest request)
    {
        return await _userService.UpdateAsync(request);
    }

    public async Task<UpdateUserResponse> PatchAsync(PatchUserRequest request)
    {
        return await _userService.PatchAsync(request);
    }
    
    // Delete operations
    public async Task<DeleteUserResponse> DeleteAsync(DeleteUserRequest request)
    {
        return await _userService.DeleteAsync(request);
    }
    
    // Relationship operations
    public async Task<UserGroupResponse> AddToGroupAsync(AddUserToGroupRequest request)
    {
        return await _userService.AddToGroupAsync(request);
    }

    public async Task<UserGroupResponse> RemoveFromGroupAsync(RemoveUserFromGroupRequest request)
    {
        return await _userService.RemoveFromGroupAsync(request);
    }

    public async Task<UserRoleResponse> AssignToRoleAsync(AssignUserToRoleRequest request)
    {
        return await _userService.AssignToRoleAsync(request);
    }

    public async Task<UserRoleResponse> UnassignFromRoleAsync(UnassignUserFromRoleRequest request)
    {
        return await _userService.UnassignFromRoleAsync(request);
    }
    
    // Bulk operations
    public async Task<BulkUserResponse<CreateUserResponse>> CreateBulkAsync(BulkUserRequest<CreateUserRequest> request)
    {
        return await _userService.CreateBulkAsync(request);
    }

    public async Task<BulkUserResponse<UpdateUserResponse>> UpdateBulkAsync(BulkUserRequest<UpdateUserRequest> request)
    {
        return await _userService.UpdateBulkAsync(request);
    }

    public async Task<BulkUserResponse<DeleteUserResponse>> DeleteBulkAsync(BulkUserRequest<DeleteUserRequest> request)
    {
        return await _userService.DeleteBulkAsync(request);
    }
}
