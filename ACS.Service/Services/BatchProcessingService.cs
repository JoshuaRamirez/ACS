using Microsoft.Extensions.Logging;
using ACS.Service.Infrastructure;
using ACS.Service.Infrastructure.BatchCommands;
using ACS.Service.Domain;
using System.Diagnostics;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using ACS.Service.Data;

namespace ACS.Service.Services;

/// <summary>
/// Service for processing batch operations efficiently
/// Coordinates bulk operations while maintaining separation of concerns
/// </summary>
public class BatchProcessingService
{
    private static readonly ActivitySource ActivitySource = new("ACS.BatchProcessing");
    
    private readonly AccessControlDomainService _domainService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<BatchProcessingService> _logger;
    private readonly HealthMonitoringService _healthMonitoring;
    
    // Configurable batch processing parameters
    private readonly int _maxConcurrency = Environment.ProcessorCount * 2;
    private readonly int _defaultBatchSize = 100;
    
    public BatchProcessingService(
        AccessControlDomainService domainService,
        ApplicationDbContext dbContext,
        HealthMonitoringService healthMonitoring,
        ILogger<BatchProcessingService> logger)
    {
        _domainService = domainService;
        _dbContext = dbContext;
        _healthMonitoring = healthMonitoring;
        _logger = logger;
    }
    
    /// <summary>
    /// Process bulk user creation
    /// </summary>
    public async Task<BatchOperationResult<User>> ProcessBulkCreateUsersAsync(BulkCreateUsersCommand command)
    {
        using var activity = ActivitySource.StartActivity("BulkCreateUsers");
        activity?.SetTag("batch.total_items", command.TotalItems);
        activity?.SetTag("batch.size", command.BatchSize);
        
        var startTime = DateTime.UtcNow;
        command.StartTime = startTime;
        
        _logger.LogInformation("Starting bulk user creation for {Count} users", command.TotalItems);
        
        var result = new BatchOperationResult<User>();
        var semaphore = new SemaphoreSlim(_maxConcurrency);
        
        try
        {
            // Process in batches for better performance and memory management
            var batches = command.Users.Chunk(command.BatchSize);
            var batchNumber = 0;
            
            foreach (var batch in batches)
            {
                batchNumber++;
                _logger.LogDebug("Processing batch {BatchNumber} with {Count} users", batchNumber, batch.Length);
                
                // Use transaction for each batch
                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                
                try
                {
                    var batchTasks = batch.Select(async userData =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            return await ProcessSingleUserCreation(userData);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });
                    
                    var batchResults = await Task.WhenAll(batchTasks);
                    
                    // Check for failures
                    var failures = batchResults.Where(r => !r.Success).ToList();
                    
                    if (failures.Any() && command.StopOnFirstError)
                    {
                        _logger.LogWarning("Batch {BatchNumber} failed with {Count} errors, rolling back", 
                            batchNumber, failures.Count);
                        await transaction.RollbackAsync();
                        
                        result.Results.AddRange(batchResults);
                        break;
                    }
                    
                    // Commit successful batch
                    await transaction.CommitAsync();
                    
                    // Add results
                    result.Results.AddRange(batchResults);
                    
                    // Extract successful users
                    var successfulUsers = batchResults
                        .Where(r => r.Success && r.ResultData is User)
                        .Select(r => (User)r.ResultData!)
                        .ToList();
                    
                    result.SuccessfulItems.AddRange(successfulUsers);
                    
                    _logger.LogInformation("Batch {BatchNumber} completed: {Success}/{Total} successful", 
                        batchNumber, successfulUsers.Count, batch.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing batch {BatchNumber}", batchNumber);
                    await transaction.RollbackAsync();
                    
                    if (command.StopOnFirstError)
                    {
                        throw;
                    }
                }
            }
        }
        finally
        {
            command.EndTime = DateTime.UtcNow;
            var duration = command.EndTime.Value - startTime;
            
            result.TotalProcessed = result.Results.Count;
            result.SuccessCount = result.Results.Count(r => r.Success);
            result.FailureCount = result.Results.Count(r => !r.Success);
            result.TotalDuration = duration;
            
            // Record metrics
            activity?.SetTag("batch.success_count", result.SuccessCount);
            activity?.SetTag("batch.failure_count", result.FailureCount);
            activity?.SetTag("batch.duration_ms", duration.TotalMilliseconds);
            
            _healthMonitoring.RecordBatchOperation("bulk_create_users", result.SuccessCount, result.FailureCount, duration);
            
            _logger.LogInformation("Bulk user creation completed: {Success}/{Total} successful in {Duration}ms", 
                result.SuccessCount, result.TotalProcessed, duration.TotalMilliseconds);
            
            semaphore.Dispose();
        }
        
        return result;
    }
    
    /// <summary>
    /// Process a single user creation within a batch
    /// </summary>
    private async Task<BatchResult> ProcessSingleUserCreation(UserCreationData userData)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new BatchResult
        {
            ItemId = userData.ExternalId ?? userData.Name
        };
        
        try
        {
            var createCommand = new CreateUserCommand
            {
                Name = userData.Name,
                GroupId = userData.GroupId,
                RoleId = userData.RoleId
            };
            
            var user = await _domainService.ExecuteCommandAsync(createCommand);
            
            result.Success = true;
            result.ResultData = user;
            result.ProcessingTime = stopwatch.Elapsed;
            
            _logger.LogTrace("Created user {UserName} with ID {UserId}", user.Name, user.Id);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Exception = ex;
            result.ProcessingTime = stopwatch.Elapsed;
            
            _logger.LogWarning(ex, "Failed to create user {UserName}", userData.Name);
        }
        
        return result;
    }
    
    /// <summary>
    /// Process bulk user updates
    /// </summary>
    public async Task<BatchOperationResult<bool>> ProcessBulkUpdateUsersAsync(BulkUpdateUsersCommand command)
    {
        using var activity = ActivitySource.StartActivity("BulkUpdateUsers");
        activity?.SetTag("batch.total_items", command.TotalItems);
        
        var startTime = DateTime.UtcNow;
        command.StartTime = startTime;
        
        _logger.LogInformation("Starting bulk user update for {Count} users", command.TotalItems);
        
        var result = new BatchOperationResult<bool>();
        var semaphore = new SemaphoreSlim(_maxConcurrency);
        
        try
        {
            var batches = command.Updates.Chunk(command.BatchSize);
            var batchNumber = 0;
            
            foreach (var batch in batches)
            {
                batchNumber++;
                
                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                
                try
                {
                    var batchTasks = batch.Select(async updateData =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            return await ProcessSingleUserUpdate(updateData);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });
                    
                    var batchResults = await Task.WhenAll(batchTasks);
                    
                    // Check for failures
                    if (batchResults.Any(r => !r.Success) && command.StopOnFirstError)
                    {
                        await transaction.RollbackAsync();
                        result.Results.AddRange(batchResults);
                        break;
                    }
                    
                    await transaction.CommitAsync();
                    result.Results.AddRange(batchResults);
                    
                    var successCount = batchResults.Count(r => r.Success);
                    result.SuccessfulItems.AddRange(Enumerable.Repeat(true, successCount));
                    
                    _logger.LogInformation("Batch {BatchNumber} completed: {Success}/{Total} successful", 
                        batchNumber, successCount, batch.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing update batch {BatchNumber}", batchNumber);
                    await transaction.RollbackAsync();
                    
                    if (command.StopOnFirstError)
                    {
                        throw;
                    }
                }
            }
        }
        finally
        {
            command.EndTime = DateTime.UtcNow;
            var duration = command.EndTime.Value - startTime;
            
            result.TotalProcessed = result.Results.Count;
            result.SuccessCount = result.Results.Count(r => r.Success);
            result.FailureCount = result.Results.Count(r => !r.Success);
            result.TotalDuration = duration;
            
            _healthMonitoring.RecordBatchOperation("bulk_update_users", result.SuccessCount, result.FailureCount, duration);
            
            _logger.LogInformation("Bulk user update completed: {Success}/{Total} successful in {Duration}ms", 
                result.SuccessCount, result.TotalProcessed, duration.TotalMilliseconds);
            
            semaphore.Dispose();
        }
        
        return result;
    }
    
    /// <summary>
    /// Process a single user update within a batch
    /// </summary>
    private async Task<BatchResult> ProcessSingleUserUpdate(UserUpdateData updateData)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new BatchResult
        {
            ItemId = updateData.UserId.ToString()
        };
        
        try
        {
            // Update basic user info
            var updateCommand = new UpdateUserCommand
            {
                UserId = updateData.UserId,
                Name = updateData.Name
            };
            
            await _domainService.ExecuteCommandAsync(updateCommand);
            
            // Update group assignment if specified
            if (updateData.NewGroupId.HasValue)
            {
                var addToGroupCommand = new AddUserToGroupCommand
                {
                    UserId = updateData.UserId,
                    GroupId = updateData.NewGroupId.Value
                };
                
                await _domainService.ExecuteCommandAsync(addToGroupCommand);
            }
            
            // Update role assignment if specified
            if (updateData.NewRoleId.HasValue)
            {
                var assignRoleCommand = new AssignUserToRoleCommand
                {
                    UserId = updateData.UserId,
                    RoleId = updateData.NewRoleId.Value
                };
                
                await _domainService.ExecuteCommandAsync(assignRoleCommand);
            }
            
            result.Success = true;
            result.ResultData = true;
            result.ProcessingTime = stopwatch.Elapsed;
            
            _logger.LogTrace("Updated user {UserId}", updateData.UserId);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Exception = ex;
            result.ProcessingTime = stopwatch.Elapsed;
            
            _logger.LogWarning(ex, "Failed to update user {UserId}", updateData.UserId);
        }
        
        return result;
    }
    
    /// <summary>
    /// Process bulk user deletion
    /// </summary>
    public async Task<BatchOperationResult<bool>> ProcessBulkDeleteUsersAsync(BulkDeleteUsersCommand command)
    {
        using var activity = ActivitySource.StartActivity("BulkDeleteUsers");
        activity?.SetTag("batch.total_items", command.TotalItems);
        
        var startTime = DateTime.UtcNow;
        command.StartTime = startTime;
        
        _logger.LogInformation("Starting bulk user deletion for {Count} users", command.TotalItems);
        
        var result = new BatchOperationResult<bool>();
        
        try
        {
            // Process deletions in batches with transactions
            var batches = command.UserIds.Chunk(command.BatchSize);
            var batchNumber = 0;
            
            foreach (var batch in batches)
            {
                batchNumber++;
                
                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                
                try
                {
                    var batchResults = new List<BatchResult>();
                    
                    foreach (var userId in batch)
                    {
                        var deleteResult = await ProcessSingleUserDeletion(userId);
                        batchResults.Add(deleteResult);
                        
                        if (!deleteResult.Success && command.StopOnFirstError)
                        {
                            await transaction.RollbackAsync();
                            result.Results.AddRange(batchResults);
                            return result;
                        }
                    }
                    
                    await transaction.CommitAsync();
                    result.Results.AddRange(batchResults);
                    
                    var successCount = batchResults.Count(r => r.Success);
                    result.SuccessfulItems.AddRange(Enumerable.Repeat(true, successCount));
                    
                    _logger.LogInformation("Deletion batch {BatchNumber} completed: {Success}/{Total} successful", 
                        batchNumber, successCount, batch.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing deletion batch {BatchNumber}", batchNumber);
                    await transaction.RollbackAsync();
                    
                    if (command.StopOnFirstError)
                    {
                        throw;
                    }
                }
            }
        }
        finally
        {
            command.EndTime = DateTime.UtcNow;
            var duration = command.EndTime.Value - startTime;
            
            result.TotalProcessed = result.Results.Count;
            result.SuccessCount = result.Results.Count(r => r.Success);
            result.FailureCount = result.Results.Count(r => !r.Success);
            result.TotalDuration = duration;
            
            _healthMonitoring.RecordBatchOperation("bulk_delete_users", result.SuccessCount, result.FailureCount, duration);
            
            _logger.LogInformation("Bulk user deletion completed: {Success}/{Total} successful in {Duration}ms", 
                result.SuccessCount, result.TotalProcessed, duration.TotalMilliseconds);
        }
        
        return result;
    }
    
    /// <summary>
    /// Process a single user deletion within a batch
    /// </summary>
    private async Task<BatchResult> ProcessSingleUserDeletion(int userId)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new BatchResult
        {
            ItemId = userId.ToString()
        };
        
        try
        {
            var deleteCommand = new DeleteUserCommand
            {
                UserId = userId
            };
            
            await _domainService.ExecuteCommandAsync(deleteCommand);
            
            result.Success = true;
            result.ResultData = true;
            result.ProcessingTime = stopwatch.Elapsed;
            
            _logger.LogTrace("Deleted user {UserId}", userId);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Exception = ex;
            result.ProcessingTime = stopwatch.Elapsed;
            
            _logger.LogWarning(ex, "Failed to delete user {UserId}", userId);
        }
        
        return result;
    }
    
    /// <summary>
    /// Process bulk permission assignments
    /// </summary>
    public async Task<BatchOperationResult<bool>> ProcessBulkAssignPermissionsAsync(BulkAssignPermissionsCommand command)
    {
        using var activity = ActivitySource.StartActivity("BulkAssignPermissions");
        activity?.SetTag("batch.total_items", command.TotalItems);
        
        var startTime = DateTime.UtcNow;
        command.StartTime = startTime;
        
        _logger.LogInformation("Starting bulk permission assignment for {Count} operations", command.TotalItems);
        
        var result = new BatchOperationResult<bool>();
        var semaphore = new SemaphoreSlim(_maxConcurrency);
        
        try
        {
            // Group by entity for more efficient processing
            var groupedAssignments = command.Assignments.GroupBy(a => a.EntityId);
            
            var tasks = groupedAssignments.Select(async group =>
            {
                await semaphore.WaitAsync();
                try
                {
                    return await ProcessEntityPermissions(group.Key, group.ToList());
                }
                finally
                {
                    semaphore.Release();
                }
            });
            
            var allResults = await Task.WhenAll(tasks);
            
            foreach (var entityResults in allResults)
            {
                result.Results.AddRange(entityResults);
            }
            
            result.SuccessfulItems.AddRange(
                result.Results.Where(r => r.Success).Select(_ => true));
        }
        finally
        {
            command.EndTime = DateTime.UtcNow;
            var duration = command.EndTime.Value - startTime;
            
            result.TotalProcessed = result.Results.Count;
            result.SuccessCount = result.Results.Count(r => r.Success);
            result.FailureCount = result.Results.Count(r => !r.Success);
            result.TotalDuration = duration;
            
            _healthMonitoring.RecordBatchOperation("bulk_assign_permissions", result.SuccessCount, result.FailureCount, duration);
            
            _logger.LogInformation("Bulk permission assignment completed: {Success}/{Total} successful in {Duration}ms", 
                result.SuccessCount, result.TotalProcessed, duration.TotalMilliseconds);
            
            semaphore.Dispose();
        }
        
        return result;
    }
    
    /// <summary>
    /// Process permissions for a single entity
    /// </summary>
    private async Task<List<BatchResult>> ProcessEntityPermissions(int entityId, List<PermissionAssignment> assignments)
    {
        var results = new List<BatchResult>();
        
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        
        try
        {
            foreach (var assignment in assignments)
            {
                var stopwatch = Stopwatch.StartNew();
                var result = new BatchResult
                {
                    ItemId = $"{entityId}:{assignment.Permission.Uri}:{assignment.Permission.HttpVerb}"
                };
                
                try
                {
                    if (assignment.IsRevoke)
                    {
                        var removeCommand = new RemovePermissionFromEntityCommand
                        {
                            EntityId = entityId,
                            Permission = assignment.Permission
                        };
                        
                        await _domainService.ExecuteCommandAsync(removeCommand);
                    }
                    else
                    {
                        var addCommand = new AddPermissionToEntityCommand
                        {
                            EntityId = entityId,
                            Permission = assignment.Permission
                        };
                        
                        await _domainService.ExecuteCommandAsync(addCommand);
                    }
                    
                    result.Success = true;
                    result.ProcessingTime = stopwatch.Elapsed;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                    result.Exception = ex;
                    result.ProcessingTime = stopwatch.Elapsed;
                    
                    _logger.LogWarning(ex, "Failed to {Operation} permission for entity {EntityId}", 
                        assignment.IsRevoke ? "revoke" : "assign", entityId);
                }
                
                results.Add(result);
            }
            
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process permissions for entity {EntityId}", entityId);
            await transaction.RollbackAsync();
            throw;
        }
        
        return results;
    }
}