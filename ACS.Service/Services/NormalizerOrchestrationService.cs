using ACS.Service.Data;
using ACS.Service.Delegates.Normalizers;
using ACS.Service.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ACS.Service.Services;

/// <summary>
/// Service for orchestrating normalizer operations with proper async patterns
/// </summary>
public interface INormalizerOrchestrationService
{
    // User-Group operations
    Task AddUserToGroupAsync(int userId, int groupId, string? createdBy = null, CancellationToken cancellationToken = default);
    Task RemoveUserFromGroupAsync(int userId, int groupId, CancellationToken cancellationToken = default);
    
    // User-Role operations
    Task AssignUserToRoleAsync(int userId, int roleId, string? createdBy = null, CancellationToken cancellationToken = default);
    Task UnassignUserFromRoleAsync(int userId, int roleId, CancellationToken cancellationToken = default);
    
    // Group-Group operations
    Task AddGroupToGroupAsync(int childGroupId, int parentGroupId, string? createdBy = null, CancellationToken cancellationToken = default);
    Task RemoveGroupFromGroupAsync(int childGroupId, int parentGroupId, CancellationToken cancellationToken = default);
    
    // Role-Group operations
    Task AddRoleToGroupAsync(int roleId, int groupId, string? createdBy = null, CancellationToken cancellationToken = default);
    Task RemoveRoleFromGroupAsync(int roleId, int groupId, CancellationToken cancellationToken = default);
    
    // Permission operations
    Task CreatePermissionSchemeAsync(PermissionSchemeData data, CancellationToken cancellationToken = default);
    Task CreateResourceAsync(ResourceData data, CancellationToken cancellationToken = default);
    Task CreateUriAccessAsync(UriAccessData data, CancellationToken cancellationToken = default);
    
    // Batch operations
    Task<BatchOperationResult> ExecuteBatchAsync(IEnumerable<NormalizerOperation> operations, CancellationToken cancellationToken = default);
    
    // Transaction support
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of normalizer orchestration service
/// </summary>
public class NormalizerOrchestrationService : INormalizerOrchestrationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<NormalizerOrchestrationService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ConcurrentDictionary<string, DateTime> _operationCache = new();

    public NormalizerOrchestrationService(
        ApplicationDbContext dbContext,
        ILogger<NormalizerOrchestrationService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task AddUserToGroupAsync(int userId, int groupId, string? createdBy = null, CancellationToken cancellationToken = default)
    {
        var operationKey = $"AddUserToGroup_{userId}_{groupId}";
        if (IsOperationCached(operationKey))
        {
            _logger.LogDebug("Operation {Operation} already executed recently, skipping", operationKey);
            return;
        }

        try
        {
            await _semaphore.WaitAsync(cancellationToken);
            
            _logger.LogInformation("Adding user {UserId} to group {GroupId}", userId, groupId);
            
            // Get the actual entities from the database
            var userData = await _dbContext.Users.FindAsync(userId);
            var groupData = await _dbContext.Groups.FindAsync(groupId);
            
            if (userData != null && groupData != null)
            {
                // Convert data models to domain objects for normalizer
                var user = new Domain.User { Id = userData.Id, Name = userData.Name };
                var group = new Domain.Group { Id = groupData.Id, Name = groupData.Name };
                
                AddUserToGroupNormalizer.Execute(user, group);
            }
            
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            CacheOperation(operationKey);
            
            _logger.LogInformation("Successfully added user {UserId} to group {GroupId}", userId, groupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add user {UserId} to group {GroupId}", userId, groupId);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RemoveUserFromGroupAsync(int userId, int groupId, CancellationToken cancellationToken = default)
    {
        var operationKey = $"RemoveUserFromGroup_{userId}_{groupId}";
        if (IsOperationCached(operationKey))
        {
            _logger.LogDebug("Operation {Operation} already executed recently, skipping", operationKey);
            return;
        }

        try
        {
            await _semaphore.WaitAsync(cancellationToken);
            
            _logger.LogInformation("Removing user {UserId} from group {GroupId}", userId, groupId);
            
            // Get the actual entities from the database
            var userData = await _dbContext.Users.FindAsync(userId);
            var groupData = await _dbContext.Groups.FindAsync(groupId);
            
            if (userData != null && groupData != null)
            {
                // Convert data models to domain objects for normalizer
                var user = new Domain.User { Id = userData.Id, Name = userData.Name };
                var group = new Domain.Group { Id = groupData.Id, Name = groupData.Name };
                
                RemoveUserFromGroupNormalizer.Execute(user, group);
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            CacheOperation(operationKey);
            
            _logger.LogInformation("Successfully removed user {UserId} from group {GroupId}", userId, groupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove user {UserId} from group {GroupId}", userId, groupId);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task AssignUserToRoleAsync(int userId, int roleId, string? createdBy = null, CancellationToken cancellationToken = default)
    {
        var operationKey = $"AssignUserToRole_{userId}_{roleId}";
        if (IsOperationCached(operationKey))
        {
            _logger.LogDebug("Operation {Operation} already executed recently, skipping", operationKey);
            return;
        }

        try
        {
            await _semaphore.WaitAsync(cancellationToken);
            
            _logger.LogInformation("Assigning user {UserId} to role {RoleId}", userId, roleId);
            
            // Get the actual entities from the database
            var userData = await _dbContext.Users.FindAsync(userId);
            var roleData = await _dbContext.Roles.FindAsync(roleId);
            
            if (userData != null && roleData != null)
            {
                // Convert data models to domain objects for normalizer
                var user = new Domain.User { Id = userData.Id, Name = userData.Name };
                var role = new Domain.Role { Id = roleData.Id, Name = roleData.Name };
                
                AssignUserToRoleNormalizer.Execute(user, role);
            }
            
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            CacheOperation(operationKey);
            
            _logger.LogInformation("Successfully assigned user {UserId} to role {RoleId}", userId, roleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign user {UserId} to role {RoleId}", userId, roleId);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UnassignUserFromRoleAsync(int userId, int roleId, CancellationToken cancellationToken = default)
    {
        var operationKey = $"UnassignUserFromRole_{userId}_{roleId}";
        if (IsOperationCached(operationKey))
        {
            _logger.LogDebug("Operation {Operation} already executed recently, skipping", operationKey);
            return;
        }

        try
        {
            await _semaphore.WaitAsync(cancellationToken);
            
            _logger.LogInformation("Unassigning user {UserId} from role {RoleId}", userId, roleId);
            
            // Get the actual entities from the database
            var userData = await _dbContext.Users.FindAsync(userId);
            var roleData = await _dbContext.Roles.FindAsync(roleId);
            
            if (userData != null && roleData != null)
            {
                // Convert data models to domain objects for normalizer
                var user = new Domain.User { Id = userData.Id, Name = userData.Name };
                var role = new Domain.Role { Id = roleData.Id, Name = roleData.Name };
                
                UnAssignUserFromRoleNormalizer.Execute(user, role);
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            CacheOperation(operationKey);
            
            _logger.LogInformation("Successfully unassigned user {UserId} from role {RoleId}", userId, roleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unassign user {UserId} from role {RoleId}", userId, roleId);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task AddGroupToGroupAsync(int childGroupId, int parentGroupId, string? createdBy = null, CancellationToken cancellationToken = default)
    {
        var operationKey = $"AddGroupToGroup_{childGroupId}_{parentGroupId}";
        if (IsOperationCached(operationKey))
        {
            _logger.LogDebug("Operation {Operation} already executed recently, skipping", operationKey);
            return;
        }

        try
        {
            await _semaphore.WaitAsync(cancellationToken);
            
            _logger.LogInformation("Adding group {ChildGroupId} to group {ParentGroupId}", childGroupId, parentGroupId);
            
            // Check for circular reference
            if (await WouldCreateCircularReferenceAsync(childGroupId, parentGroupId, cancellationToken))
            {
                throw new InvalidOperationException($"Adding group {childGroupId} to group {parentGroupId} would create a circular reference");
            }
            
            // Get the actual entities from the database
            var childGroupData = await _dbContext.Groups.FindAsync(childGroupId);
            var parentGroupData = await _dbContext.Groups.FindAsync(parentGroupId);
            
            if (childGroupData != null && parentGroupData != null)
            {
                // Convert data models to domain objects for normalizer
                var childGroup = new Domain.Group { Id = childGroupData.Id, Name = childGroupData.Name };
                var parentGroup = new Domain.Group { Id = parentGroupData.Id, Name = parentGroupData.Name };
                
                AddGroupToGroupNormalizer.Execute(childGroup, parentGroup);
            }
            
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            CacheOperation(operationKey);
            
            _logger.LogInformation("Successfully added group {ChildGroupId} to group {ParentGroupId}", childGroupId, parentGroupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add group {ChildGroupId} to group {ParentGroupId}", childGroupId, parentGroupId);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RemoveGroupFromGroupAsync(int childGroupId, int parentGroupId, CancellationToken cancellationToken = default)
    {
        var operationKey = $"RemoveGroupFromGroup_{childGroupId}_{parentGroupId}";
        if (IsOperationCached(operationKey))
        {
            _logger.LogDebug("Operation {Operation} already executed recently, skipping", operationKey);
            return;
        }

        try
        {
            await _semaphore.WaitAsync(cancellationToken);
            
            _logger.LogInformation("Removing group {ChildGroupId} from group {ParentGroupId}", childGroupId, parentGroupId);
            
            // Get the actual group entities
            var childGroupData = await _dbContext.Groups.FindAsync(childGroupId);
            var parentGroupData = await _dbContext.Groups.FindAsync(parentGroupId);
            
            if (childGroupData != null && parentGroupData != null)
            {
                // Convert data models to domain objects for normalizer
                var childGroup = new Domain.Group { Id = childGroupData.Id, Name = childGroupData.Name };
                var parentGroup = new Domain.Group { Id = parentGroupData.Id, Name = parentGroupData.Name };
                
                RemoveGroupFromGroupNormalizer.Execute(childGroup, parentGroup);
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            CacheOperation(operationKey);
            
            _logger.LogInformation("Successfully removed group {ChildGroupId} from group {ParentGroupId}", childGroupId, parentGroupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove group {ChildGroupId} from group {ParentGroupId}", childGroupId, parentGroupId);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task AddRoleToGroupAsync(int roleId, int groupId, string? createdBy = null, CancellationToken cancellationToken = default)
    {
        var operationKey = $"AddRoleToGroup_{roleId}_{groupId}";
        if (IsOperationCached(operationKey))
        {
            _logger.LogDebug("Operation {Operation} already executed recently, skipping", operationKey);
            return;
        }

        try
        {
            await _semaphore.WaitAsync(cancellationToken);
            
            _logger.LogInformation("Adding role {RoleId} to group {GroupId}", roleId, groupId);
            
            // Get the actual entities
            var roleData = await _dbContext.Roles.FindAsync(roleId);
            var groupData = await _dbContext.Groups.FindAsync(groupId);
            
            if (roleData != null && groupData != null)
            {
                // Convert data models to domain objects for normalizer
                var role = new Domain.Role { Id = roleData.Id, Name = roleData.Name };
                var group = new Domain.Group { Id = groupData.Id, Name = groupData.Name };
                
                AddRoleToGroupNormalizer.Execute(role, group);
            }
            
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            CacheOperation(operationKey);
            
            _logger.LogInformation("Successfully added role {RoleId} to group {GroupId}", roleId, groupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add role {RoleId} to group {GroupId}", roleId, groupId);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task RemoveRoleFromGroupAsync(int roleId, int groupId, CancellationToken cancellationToken = default)
    {
        var operationKey = $"RemoveRoleFromGroup_{roleId}_{groupId}";
        if (IsOperationCached(operationKey))
        {
            _logger.LogDebug("Operation {Operation} already executed recently, skipping", operationKey);
            return;
        }

        try
        {
            await _semaphore.WaitAsync(cancellationToken);
            
            _logger.LogInformation("Removing role {RoleId} from group {GroupId}", roleId, groupId);
            
            // Get the actual entities
            var roleData = await _dbContext.Roles.FindAsync(roleId);
            var groupData = await _dbContext.Groups.FindAsync(groupId);
            
            if (roleData != null && groupData != null)
            {
                // Convert data models to domain objects for normalizer
                var role = new Domain.Role { Id = roleData.Id, Name = roleData.Name };
                var group = new Domain.Group { Id = groupData.Id, Name = groupData.Name };
                
                RemoveRoleFromGroupNormalizer.Execute(role, group);
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            CacheOperation(operationKey);
            
            _logger.LogInformation("Successfully removed role {RoleId} from group {GroupId}", roleId, groupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove role {RoleId} from group {GroupId}", roleId, groupId);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task CreatePermissionSchemeAsync(PermissionSchemeData data, CancellationToken cancellationToken = default)
    {
        try
        {
            await _semaphore.WaitAsync(cancellationToken);
            
            _logger.LogInformation("Creating permission scheme for entity {EntityId}", data.EntityId);
            
            // Create permission object for normalizer
            var permission = new Domain.Permission
            {
                EntityId = data.EntityId,
                Uri = data.ResourceName,
                Grant = data.Allow,
                Deny = data.Deny
            };
            
            CreatePermissionSchemeNormalizer.Execute(permission);
            
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("Successfully created permission scheme for entity {EntityId}", data.EntityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create permission scheme for entity {EntityId}", data.EntityId);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task CreateResourceAsync(ResourceData data, CancellationToken cancellationToken = default)
    {
        try
        {
            await _semaphore.WaitAsync(cancellationToken);
            
            _logger.LogInformation("Creating resource {ResourceName}", data.Name);
            
            // Create permission object for normalizer
            var permission = new Domain.Permission
            {
                Uri = data.Name
            };
            
            CreateResourceNormalizer.Execute(permission);
            
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("Successfully created resource {ResourceName}", data.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create resource {ResourceName}", data.Name);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task CreateUriAccessAsync(UriAccessData data, CancellationToken cancellationToken = default)
    {
        try
        {
            await _semaphore.WaitAsync(cancellationToken);
            
            _logger.LogInformation("Creating URI access for resource {ResourceId}", data.ResourceId);
            
            // Create permission object for normalization
            var permission = new Permission
            {
                EntityId = data.ResourceId,
                Resource = data.UriPattern,
                Action = data.VerbId.ToString(),
                Scope = "URI",
                Grant = true,
                Deny = false
            };
            
            CreateUriAccessNormalizer.Execute(permission);
            
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("Successfully created URI access for resource {ResourceId}", data.ResourceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create URI access for resource {ResourceId}", data.ResourceId);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<BatchOperationResult> ExecuteBatchAsync(IEnumerable<NormalizerOperation> operations, CancellationToken cancellationToken = default)
    {
        var result = new BatchOperationResult();
        var operationList = operations.ToList();
        
        _logger.LogInformation("Executing batch of {Count} normalizer operations", operationList.Count);

        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        
        try
        {
            foreach (var operation in operationList)
            {
                try
                {
                    await ExecuteOperationAsync(operation, cancellationToken);
                    result.SuccessfulOperations.Add(operation);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to execute operation {OperationType}", operation.Type);
                    result.FailedOperations.Add(new FailedOperation
                    {
                        Operation = operation,
                        Error = ex.Message
                    });
                    
                    if (!operation.ContinueOnError)
                    {
                        throw;
                    }
                }
            }

            await transaction.CommitAsync(cancellationToken);
            
            _logger.LogInformation("Batch operation completed. Success: {Success}, Failed: {Failed}",
                result.SuccessfulOperations.Count, result.FailedOperations.Count);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Batch operation failed, rolling back transaction");
            throw;
        }

        return result;
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        
        try
        {
            var result = await operation();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task ExecuteOperationAsync(NormalizerOperation operation, CancellationToken cancellationToken)
    {
        switch (operation.Type)
        {
            case OperationType.AddUserToGroup:
                await AddUserToGroupAsync(operation.SourceId, operation.TargetId, operation.CreatedBy, cancellationToken);
                break;
            case OperationType.RemoveUserFromGroup:
                await RemoveUserFromGroupAsync(operation.SourceId, operation.TargetId, cancellationToken);
                break;
            case OperationType.AssignUserToRole:
                await AssignUserToRoleAsync(operation.SourceId, operation.TargetId, operation.CreatedBy, cancellationToken);
                break;
            case OperationType.UnassignUserFromRole:
                await UnassignUserFromRoleAsync(operation.SourceId, operation.TargetId, cancellationToken);
                break;
            case OperationType.AddGroupToGroup:
                await AddGroupToGroupAsync(operation.SourceId, operation.TargetId, operation.CreatedBy, cancellationToken);
                break;
            case OperationType.RemoveGroupFromGroup:
                await RemoveGroupFromGroupAsync(operation.SourceId, operation.TargetId, cancellationToken);
                break;
            case OperationType.AddRoleToGroup:
                await AddRoleToGroupAsync(operation.SourceId, operation.TargetId, operation.CreatedBy, cancellationToken);
                break;
            case OperationType.RemoveRoleFromGroup:
                await RemoveRoleFromGroupAsync(operation.SourceId, operation.TargetId, cancellationToken);
                break;
            default:
                throw new NotSupportedException($"Operation type {operation.Type} is not supported");
        }
    }

    private async Task<bool> WouldCreateCircularReferenceAsync(int childGroupId, int parentGroupId, CancellationToken cancellationToken)
    {
        // Check if adding childGroup to parentGroup would create a circular reference
        // This happens if parentGroup is already a descendant of childGroup
        
        var visited = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(childGroupId);

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            
            if (currentId == parentGroupId)
            {
                return true; // Found circular reference
            }

            if (!visited.Add(currentId))
            {
                continue; // Already visited
            }

            // Get all child groups of current group
            var childGroups = await _dbContext.Set<Data.Models.GroupGroup>()
                .Where(gg => gg.ParentGroupId == currentId)
                .Select(gg => gg.ChildGroupId)
                .ToListAsync(cancellationToken);

            foreach (var childId in childGroups)
            {
                queue.Enqueue(childId);
            }
        }

        return false;
    }

    private bool IsOperationCached(string operationKey)
    {
        if (_operationCache.TryGetValue(operationKey, out var lastExecution))
        {
            // Consider operation cached if executed within last 5 seconds
            return (DateTime.UtcNow - lastExecution).TotalSeconds < 5;
        }
        return false;
    }

    private void CacheOperation(string operationKey)
    {
        _operationCache[operationKey] = DateTime.UtcNow;
        
        // Clean up old entries
        var cutoff = DateTime.UtcNow.AddMinutes(-1);
        var keysToRemove = _operationCache
            .Where(kvp => kvp.Value < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var key in keysToRemove)
        {
            _operationCache.TryRemove(key, out _);
        }
    }
}

#region Supporting Classes

public class PermissionSchemeData
{
    public int EntityId { get; set; }
    public int SchemeType { get; set; }
    public int VerbId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public bool Allow { get; set; }
    public bool Deny { get; set; }
    public string? CreatedBy { get; set; }
}

public class ResourceData
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CreatedBy { get; set; }
}

public class UriAccessData
{
    public int ResourceId { get; set; }
    public string UriPattern { get; set; } = string.Empty;
    public int VerbId { get; set; }
    public string? CreatedBy { get; set; }
}

public class NormalizerOperation
{
    public OperationType Type { get; set; }
    public int SourceId { get; set; }
    public int TargetId { get; set; }
    public string? CreatedBy { get; set; }
    public bool ContinueOnError { get; set; } = false;
}

public enum OperationType
{
    AddUserToGroup,
    RemoveUserFromGroup,
    AssignUserToRole,
    UnassignUserFromRole,
    AddGroupToGroup,
    RemoveGroupFromGroup,
    AddRoleToGroup,
    RemoveRoleFromGroup,
    CreatePermissionScheme,
    CreateResource,
    CreateUriAccess
}

public class BatchOperationResult
{
    public List<NormalizerOperation> SuccessfulOperations { get; set; } = new();
    public List<FailedOperation> FailedOperations { get; set; } = new();
    public bool IsSuccess => !FailedOperations.Any();
}

public class FailedOperation
{
    public NormalizerOperation Operation { get; set; } = null!;
    public string Error { get; set; } = string.Empty;
}

#endregion