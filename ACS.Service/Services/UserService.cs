using ACS.Service.Domain;
using ACS.Service.Infrastructure;
using ACS.Service.Data;
using ACS.Service.Requests;
using ACS.Service.Responses;
using ACS.Service.Delegates.Queries;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ACS.Service.Services;

/// <summary>
/// Flat service for User operations in LMAX architecture
/// Works directly with in-memory entity graph and fire-and-forget persistence
/// Uses request/response pattern for clean API contracts
/// No service-to-service dependencies
/// </summary>
public class UserService : IUserService
{
    private readonly InMemoryEntityGraph _entityGraph;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<UserService> _logger;

    public UserService(
        InMemoryEntityGraph entityGraph,
        ApplicationDbContext dbContext,
        ILogger<UserService> logger)
    {
        _entityGraph = entityGraph;
        _dbContext = dbContext;
        _logger = logger;
    }

    public Task<UserResponse> GetByIdAsync(GetUserRequest request)
    {
        // Telemetry removed for build compatibility
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Use Query object for data access
            var getUserQuery = new GetUserByIdQuery
            {
                UserId = request.UserId,
                EntityGraph = _entityGraph
            };

            var user = getUserQuery.Execute();

            // activity?.SetTag("operation.success", true);
            // activity?.SetTag("user.found", user != null);
            // Database operation recorded

            _logger.LogDebug("Retrieved user {UserId} in {Duration}ms, found: {Found}", 
                request.UserId, stopwatch.ElapsedMilliseconds, user != null);

            return Task.FromResult(new UserResponse
            {
                User = user,
                Success = true,
                Message = user != null ? "User found" : "User not found"
            });
        }
        catch (Exception ex)
        {
            // activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            // OpenTelemetryConfiguration.RecordDatabaseOperation("GetUserById", stopwatch.Elapsed.TotalSeconds, false, "User");

            _logger.LogError(ex, "Error retrieving user {UserId} in {Duration}ms", 
                request.UserId, stopwatch.ElapsedMilliseconds);

            return Task.FromResult(new UserResponse
            {
                Success = false,
                Message = "Error retrieving user",
                Errors = new[] { ex.Message }
            });
        }
    }

    public Task<UsersResponse> GetAllAsync(GetUsersRequest request)
    {
        // using var activity = OpenTelemetryConfiguration.ServiceActivitySource.StartActivity("UserService.GetAll");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Use composite Query object for data access with pagination and count
            var getUsersWithCountQuery = new GetUsersWithCountQuery
            {
                Page = request.Page,
                PageSize = request.PageSize,
                Search = request.Search,
                SortBy = request.SortBy,
                SortDescending = request.SortDescending,
                EntityGraph = _entityGraph
            };

            var (users, totalCount) = getUsersWithCountQuery.Execute();

            // activity?.SetTag("operation.success", true);
            // activity?.SetTag("users.count", users.Count);
            // activity?.SetTag("users.total", totalCount);
            // OpenTelemetryConfiguration.RecordDatabaseOperation("GetAllUsers", stopwatch.Elapsed.TotalSeconds, true, "User");

            _logger.LogDebug("Retrieved {UserCount} of {TotalCount} users in {Duration}ms", 
                users.Count, totalCount, stopwatch.ElapsedMilliseconds);

            return Task.FromResult(new UsersResponse
            {
                Users = users,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize,
                Success = true,
                Message = $"Retrieved {users.Count} users"
            });
        }
        catch (Exception ex)
        {
            // activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            // OpenTelemetryConfiguration.RecordDatabaseOperation("GetAllUsers", stopwatch.Elapsed.TotalSeconds, false, "User");

            _logger.LogError(ex, "Error retrieving users in {Duration}ms", stopwatch.ElapsedMilliseconds);

            return Task.FromResult(new UsersResponse
            {
                Success = false,
                Message = "Error retrieving users",
                Errors = new[] { ex.Message }
            });
        }
    }

    public async Task<CreateUserResponse> CreateAsync(CreateUserRequest request)
    {
        // using var activity = OpenTelemetryConfiguration.ServiceActivitySource.StartActivity("UserService.Create");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return new CreateUserResponse
                {
                    Success = false,
                    Message = "User name is required",
                    Errors = new[] { "User name cannot be empty" }
                };
            }

            // Generate new ID and create user
            var newId = _entityGraph.GetNextUserId();
            var user = new User
            {
                Id = newId,
                Name = request.Name
            };

            _entityGraph.Users[newId] = user;

            // EF Core will handle persistence automatically via change tracking
            await _dbContext.SaveChangesAsync();

            // activity?.SetTag("operation.success", true);
            // activity?.SetTag("user.id", newId);
            // OpenTelemetryConfiguration.RecordDatabaseOperation("CreateUser", stopwatch.Elapsed.TotalSeconds, true, "User");

            _logger.LogInformation("Created user {UserId} with name {UserName} by {CreatedBy} in {Duration}ms", 
                newId, user.Name, request.CreatedBy, stopwatch.ElapsedMilliseconds);

            return new CreateUserResponse
            {
                User = user,
                Success = true,
                Message = "User created successfully"
            };
        }
        catch (Exception ex)
        {
            // activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            // OpenTelemetryConfiguration.RecordDatabaseOperation("CreateUser", stopwatch.Elapsed.TotalSeconds, false, "User");

            _logger.LogError(ex, "Error creating user in {Duration}ms", stopwatch.ElapsedMilliseconds);

            return new CreateUserResponse
            {
                Success = false,
                Message = "Error creating user",
                Errors = new[] { ex.Message }
            };
        }
    }

    public async Task<UpdateUserResponse> UpdateAsync(UpdateUserRequest request)
    {
        // using var activity = OpenTelemetryConfiguration.ServiceActivitySource.StartActivity("UserService.Update");
        // activity?.SetTag("user.id", request.UserId);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return new UpdateUserResponse
                {
                    Success = false,
                    Message = "User name is required",
                    Errors = new[] { "User name cannot be empty" }
                };
            }

            // Use Query object to check if user exists
            var getUserQuery = new GetUserByIdQuery
            {
                UserId = request.UserId,
                EntityGraph = _entityGraph
            };

            var existingUser = getUserQuery.Execute();
            if (existingUser == null)
            {
                return new UpdateUserResponse
                {
                    Success = false,
                    Message = "User not found",
                    Errors = new[] { $"User {request.UserId} not found" }
                };
            }

            // Use domain method for name change (includes business rules)
            existingUser.ChangeName(request.Name, request.UpdatedBy);

            // EF Core change tracking will handle persistence automatically
            await _dbContext.SaveChangesAsync();

            // activity?.SetTag("operation.success", true);
            // OpenTelemetryConfiguration.RecordDatabaseOperation("UpdateUser", stopwatch.Elapsed.TotalSeconds, true, "User");

            _logger.LogInformation("Updated user {UserId} with name {UserName} by {UpdatedBy} in {Duration}ms", 
                request.UserId, request.Name, request.UpdatedBy, stopwatch.ElapsedMilliseconds);

            return new UpdateUserResponse
            {
                User = existingUser,
                Success = true,
                Message = "User updated successfully"
            };
        }
        catch (Exception ex)
        {
            // activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            // OpenTelemetryConfiguration.RecordDatabaseOperation("UpdateUser", stopwatch.Elapsed.TotalSeconds, false, "User");

            _logger.LogError(ex, "Error updating user {UserId} in {Duration}ms", request.UserId, stopwatch.ElapsedMilliseconds);

            return new UpdateUserResponse
            {
                Success = false,
                Message = "Error updating user",
                Errors = new[] { ex.Message }
            };
        }
    }

    public async Task<UpdateUserResponse> PatchAsync(PatchUserRequest request)
    {
        // using var activity = OpenTelemetryConfiguration.ServiceActivitySource.StartActivity("UserService.Patch");
        // activity?.SetTag("user.id", request.UserId);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Use Query object to get user
            var getUserQuery = new GetUserByIdQuery
            {
                UserId = request.UserId,
                EntityGraph = _entityGraph
            };

            var existingUser = getUserQuery.Execute();
            if (existingUser == null)
            {
                return new UpdateUserResponse
                {
                    Success = false,
                    Message = "User not found",
                    Errors = new[] { $"User {request.UserId} not found" }
                };
            }

            // Only update non-null fields
            if (!string.IsNullOrEmpty(request.Name))
            {
                existingUser.ChangeName(request.Name, request.UpdatedBy);
            }

            // EF Core change tracking will handle persistence automatically
            await _dbContext.SaveChangesAsync();

            // activity?.SetTag("operation.success", true);
            // OpenTelemetryConfiguration.RecordDatabaseOperation("PatchUser", stopwatch.Elapsed.TotalSeconds, true, "User");

            _logger.LogInformation("Patched user {UserId} by {UpdatedBy} in {Duration}ms", 
                request.UserId, request.UpdatedBy, stopwatch.ElapsedMilliseconds);

            return new UpdateUserResponse
            {
                User = existingUser,
                Success = true,
                Message = "User patched successfully"
            };
        }
        catch (Exception ex)
        {
            // activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            // OpenTelemetryConfiguration.RecordDatabaseOperation("PatchUser", stopwatch.Elapsed.TotalSeconds, false, "User");

            _logger.LogError(ex, "Error patching user {UserId} in {Duration}ms", request.UserId, stopwatch.ElapsedMilliseconds);

            return new UpdateUserResponse
            {
                Success = false,
                Message = "Error patching user",
                Errors = new[] { ex.Message }
            };
        }
    }

    public async Task<DeleteUserResponse> DeleteAsync(DeleteUserRequest request)
    {
        // using var activity = OpenTelemetryConfiguration.ServiceActivitySource.StartActivity("UserService.Delete");
        // activity?.SetTag("user.id", request.UserId);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Use Query object to check if user exists before deletion
            var getUserQuery = new GetUserByIdQuery
            {
                UserId = request.UserId,
                EntityGraph = _entityGraph
            };

            var existingUser = getUserQuery.Execute();
            if (existingUser == null)
            {
                return new DeleteUserResponse
                {
                    Success = false,
                    Message = "User not found",
                    Errors = new[] { $"User {request.UserId} not found" }
                };
            }

            // Remove from in-memory graph
            _entityGraph.Users.Remove(request.UserId);

            // EF Core change tracking will handle persistence automatically
            await _dbContext.SaveChangesAsync();

            // activity?.SetTag("operation.success", true);
            // OpenTelemetryConfiguration.RecordDatabaseOperation("DeleteUser", stopwatch.Elapsed.TotalSeconds, true, "User");

            _logger.LogInformation("Deleted user {UserId} by {DeletedBy} in {Duration}ms", 
                request.UserId, request.DeletedBy, stopwatch.ElapsedMilliseconds);

            return new DeleteUserResponse
            {
                Success = true,
                Message = "User deleted successfully"
            };
        }
        catch (Exception ex)
        {
            // activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            // OpenTelemetryConfiguration.RecordDatabaseOperation("DeleteUser", stopwatch.Elapsed.TotalSeconds, false, "User");

            _logger.LogError(ex, "Error deleting user {UserId} in {Duration}ms", request.UserId, stopwatch.ElapsedMilliseconds);

            return new DeleteUserResponse
            {
                Success = false,
                Message = "Error deleting user",
                Errors = new[] { ex.Message }
            };
        }
    }

    public Task<UserGroupResponse> AddToGroupAsync(AddUserToGroupRequest request)
    {
        // Implementation for adding user to group
        // This would use the domain logic for relationship management
        throw new NotImplementedException("AddToGroupAsync implementation pending");
    }

    public Task<UserGroupResponse> RemoveFromGroupAsync(RemoveUserFromGroupRequest request)
    {
        // Implementation for removing user from group
        throw new NotImplementedException("RemoveFromGroupAsync implementation pending");
    }

    public Task<UserRoleResponse> AssignToRoleAsync(AssignUserToRoleRequest request)
    {
        // Implementation for assigning user to role
        throw new NotImplementedException("AssignToRoleAsync implementation pending");
    }

    public Task<UserRoleResponse> UnassignFromRoleAsync(UnassignUserFromRoleRequest request)
    {
        // Implementation for unassigning user from role
        throw new NotImplementedException("UnassignFromRoleAsync implementation pending");
    }

    public Task<BulkUserResponse<CreateUserResponse>> CreateBulkAsync(BulkUserRequest<CreateUserRequest> request)
    {
        // Implementation for bulk user creation
        throw new NotImplementedException("CreateBulkAsync implementation pending");
    }

    public Task<BulkUserResponse<UpdateUserResponse>> UpdateBulkAsync(BulkUserRequest<UpdateUserRequest> request)
    {
        // Implementation for bulk user updates
        throw new NotImplementedException("UpdateBulkAsync implementation pending");
    }

    public Task<BulkUserResponse<DeleteUserResponse>> DeleteBulkAsync(BulkUserRequest<DeleteUserRequest> request)
    {
        // Implementation for bulk user deletions
        throw new NotImplementedException("DeleteBulkAsync implementation pending");
    }
}