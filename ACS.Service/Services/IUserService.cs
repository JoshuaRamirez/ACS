using ACS.Service.Requests;
using ACS.Service.Responses;

namespace ACS.Service.Services;

/// <summary>
/// Service interface for User operations using request/response pattern
/// Supports full REST operations with proper separation of concerns
/// </summary>
public interface IUserService
{
    // Query operations
    Task<UserResponse> GetByIdAsync(GetUserRequest request);
    Task<UsersResponse> GetAllAsync(GetUsersRequest request);
    
    // Create operations
    Task<CreateUserResponse> CreateAsync(CreateUserRequest request);
    
    // Update operations
    Task<UpdateUserResponse> UpdateAsync(UpdateUserRequest request);
    Task<UpdateUserResponse> PatchAsync(PatchUserRequest request);
    
    // Delete operations
    Task<DeleteUserResponse> DeleteAsync(DeleteUserRequest request);
    
    // Relationship operations
    Task<UserGroupResponse> AddToGroupAsync(AddUserToGroupRequest request);
    Task<UserGroupResponse> RemoveFromGroupAsync(RemoveUserFromGroupRequest request);
    Task<UserRoleResponse> AssignToRoleAsync(AssignUserToRoleRequest request);
    Task<UserRoleResponse> UnassignFromRoleAsync(UnassignUserFromRoleRequest request);
    
    // Bulk operations
    Task<BulkUserResponse<CreateUserResponse>> CreateBulkAsync(BulkUserRequest<CreateUserRequest> request);
    Task<BulkUserResponse<UpdateUserResponse>> UpdateBulkAsync(BulkUserRequest<UpdateUserRequest> request);
    Task<BulkUserResponse<DeleteUserResponse>> DeleteBulkAsync(BulkUserRequest<DeleteUserRequest> request);
}
