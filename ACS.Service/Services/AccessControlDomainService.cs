using Microsoft.Extensions.Logging;
using ACS.Service.Infrastructure;
using ACS.Service.Domain;
using ACS.Service.Data;
using ACS.Service.Caching;
using System.Threading.Channels;
using Polly;
using Polly.Retry;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace ACS.Service.Services;

public class AccessControlDomainService : IDisposable
{
    private static readonly ActivitySource ActivitySource = new("ACS.DomainService");
    
    private readonly InMemoryEntityGraph _entityGraph;
    private readonly ApplicationDbContext _dbContext;
    private readonly TenantDatabasePersistenceService _persistenceService;
    private readonly EventPersistenceService _eventPersistenceService;
    private readonly DeadLetterQueueService _deadLetterQueue;
    private readonly ErrorRecoveryService _errorRecovery;
    private readonly HealthMonitoringService _healthMonitoring;
    private readonly IEntityCache _cache;
    private readonly ILogger<AccessControlDomainService> _logger;
    private readonly Channel<DomainCommand> _commandChannel;
    private readonly ChannelWriter<DomainCommand> _commandWriter;
    private readonly ChannelReader<DomainCommand> _commandReader;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task? _processingTask;
    private readonly bool _startBackgroundProcessing;
    
    // Thread-safe ID generation
    private int _nextUserId = 0;
    private int _nextGroupId = 0;
    private int _nextRoleId = 0;
    
    // Retry policy for database operations
    private readonly AsyncRetryPolicy _retryPolicy;

    public AccessControlDomainService(
        InMemoryEntityGraph entityGraph,
        ApplicationDbContext dbContext,
        TenantDatabasePersistenceService persistenceService,
        EventPersistenceService eventPersistenceService,
        DeadLetterQueueService deadLetterQueue,
        ErrorRecoveryService errorRecovery,
        HealthMonitoringService healthMonitoring,
        IEntityCache cache,
        ILogger<AccessControlDomainService> logger,
        bool startBackgroundProcessing = true)
    {
        _entityGraph = entityGraph;
        _dbContext = dbContext;
        _persistenceService = persistenceService;
        _eventPersistenceService = eventPersistenceService;
        _deadLetterQueue = deadLetterQueue;
        _errorRecovery = errorRecovery;
        _healthMonitoring = healthMonitoring;
        _cache = cache;
        _logger = logger;
        _startBackgroundProcessing = startBackgroundProcessing;
        _cancellationTokenSource = new CancellationTokenSource();

        // Create single-threaded channel for command processing
        var channelOptions = new BoundedChannelOptions(1000)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        };
        
        _commandChannel = Channel.CreateBounded<DomainCommand>(channelOptions);
        _commandWriter = _commandChannel.Writer;
        _commandReader = _commandChannel.Reader;
        
        // Configure retry policy for database operations
        _retryPolicy = Policy
            .Handle<DbUpdateException>()
            .Or<TimeoutException>()
            .Or<InvalidOperationException>(ex => ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception, 
                        "Database operation failed. Retry {RetryCount} after {Delay}ms", 
                        retryCount, timeSpan.TotalMilliseconds);
                });

        // Start background processing task only if enabled
        if (_startBackgroundProcessing)
        {
            _processingTask = Task.Run(ProcessCommandsAsync, _cancellationTokenSource.Token);
            _logger.LogInformation("AccessControlDomainService initialized with single-threaded command processing and retry policy");
        }
        else
        {
            _logger.LogInformation("AccessControlDomainService initialized in synchronous mode for testing");
        }
    }

    public async Task<TResult> ExecuteCommandAsync<TResult>(DomainCommand<TResult> command)
    {
        var completionSource = new TaskCompletionSource<TResult>();
        command.CompletionSource = completionSource;
        command.CompletionSourceObject = completionSource; // Fix: Set this after creating the completion source
        
        if (!_startBackgroundProcessing)
        {
            // Execute synchronously for testing
            await ProcessSingleCommand(command);
            return await completionSource.Task;
        }

        await _commandWriter.WriteAsync(command, _cancellationTokenSource.Token);
        
        _logger.LogDebug("Queued command {CommandType} for processing", command.GetType().Name);
        
        return await completionSource.Task;
    }

    public async Task ExecuteCommandAsync(DomainCommand command)
    {
        var completionSource = new TaskCompletionSource<bool>();
        command.VoidCompletionSource = completionSource;
        
        if (!_startBackgroundProcessing)
        {
            // Execute synchronously for testing
            await ProcessSingleCommand(command);
            await completionSource.Task;
            return;
        }

        await _commandWriter.WriteAsync(command, _cancellationTokenSource.Token);
        
        _logger.LogDebug("Queued void command {CommandType} for processing", command.GetType().Name);
        
        await completionSource.Task;
    }

    private async Task ProcessCommandsAsync()
    {
        _logger.LogInformation("Started domain command processing loop");

        try
        {
            await foreach (var command in _commandReader.ReadAllAsync(_cancellationTokenSource.Token))
            {
                try
                {
                    await ProcessSingleCommand(command);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing command {CommandType}", command.GetType().Name);
                    
                    // Check if this error type should be retried
                    bool shouldRetry = ex switch
                    {
                        ArgumentNullException => false,      // Null arguments (more specific)
                        ArgumentException => false,          // Invalid parameters (more general)
                        NotSupportedException => false,      // Unsupported operations
                        _ => true                           // Retry database/infrastructure errors (including InvalidOperationException)
                    };
                    
                    // Enqueue failed command to dead letter queue for retry only if it should be retried
                    if (shouldRetry)
                    {
                        try
                        {
                            await _deadLetterQueue.EnqueueFailedCommandAsync(command, ex);
                            _logger.LogInformation("Enqueued failed command {CommandType} to dead letter queue for retry", command.GetType().Name);
                        }
                        catch (Exception dlqEx)
                        {
                            _logger.LogError(dlqEx, "Failed to enqueue command {CommandType} to dead letter queue", command.GetType().Name);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Command {CommandType} failed with non-retryable error: {ErrorType}. Will not retry.", 
                            command.GetType().Name, ex.GetType().Name);
                    }
                    
                    // Complete command with error
                    if (command.VoidCompletionSource != null)
                        command.VoidCompletionSource.SetException(ex);
                    else
                        command.CompletionSourceObject?.GetType()
                            .GetMethod("SetException", new[] { typeof(Exception) })
                            ?.Invoke(command.CompletionSourceObject, new object?[] { ex });
                }
            }
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.Token.IsCancellationRequested)
        {
            _logger.LogInformation("Domain command processing cancelled");
        }
    }

    private async Task ProcessSingleCommand(DomainCommand command)
    {
        using var activity = ActivitySource.StartActivity($"domain.command.{command.GetType().Name}");
        activity?.SetTag("command.type", command.GetType().Name);
        activity?.SetTag("tenant.id", Environment.GetEnvironmentVariable("TENANT_ID") ?? "unknown");
        
        _logger.LogDebug("Processing command {CommandType}", command.GetType().Name);
        
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Execute the command using pattern matching (error recovery applied at persistence level)
            object? result = command switch
            {
                // CREATE Commands - Phase 2 requirement
                CreateUserCommand cmd => await ProcessCreateUser(cmd),
                CreateGroupCommand cmd => await ProcessCreateGroup(cmd),
                CreateRoleCommand cmd => await ProcessCreateRole(cmd),
                
                // UPDATE Commands
                UpdateUserCommand cmd => await ProcessUpdateUser(cmd),
                UpdateGroupCommand cmd => await ProcessUpdateGroup(cmd),
                UpdateRoleCommand cmd => await ProcessUpdateRole(cmd),
                
                // DELETE Commands
                DeleteUserCommand cmd => await ProcessDeleteUser(cmd),
                DeleteGroupCommand cmd => await ProcessDeleteGroup(cmd),
                DeleteRoleCommand cmd => await ProcessDeleteRole(cmd),
                
                // Relationship Commands
                AddUserToGroupCommand cmd => await ProcessAddUserToGroup(cmd),
                RemoveUserFromGroupCommand cmd => await ProcessRemoveUserFromGroup(cmd),
                AssignUserToRoleCommand cmd => await ProcessAssignUserToRole(cmd),
                UnAssignUserFromRoleCommand cmd => await ProcessUnAssignUserFromRole(cmd),
                AddRoleToGroupCommand cmd => await ProcessAddRoleToGroup(cmd),
                RemoveRoleFromGroupCommand cmd => await ProcessRemoveRoleFromGroup(cmd),
                AddGroupToGroupCommand cmd => await ProcessAddGroupToGroup(cmd),
                RemoveGroupFromGroupCommand cmd => await ProcessRemoveGroupFromGroup(cmd),
                AddPermissionToEntityCommand cmd => await ProcessAddPermissionToEntity(cmd),
                RemovePermissionFromEntityCommand cmd => await ProcessRemovePermissionFromEntity(cmd),
                
                // Query Commands
                CheckPermissionCommand cmd => await ProcessCheckPermission(cmd),
                GetUserCommand cmd => ProcessGetUser(cmd),
                GetGroupCommand cmd => ProcessGetGroup(cmd),
                GetRoleCommand cmd => ProcessGetRole(cmd),
                GetUsersCommand cmd => ProcessGetUsers(cmd),
                GetGroupsCommand cmd => ProcessGetGroups(cmd),
                GetRolesCommand cmd => ProcessGetRoles(cmd),
                GetEntityPermissionsCommand cmd => ProcessGetEntityPermissions(cmd),
                _ => throw new NotSupportedException($"Command type {command.GetType().Name} is not supported")
            };

            // Complete the command
            if (command.VoidCompletionSource != null)
            {
                command.VoidCompletionSource.SetResult(true);
            }
            else if (command.CompletionSourceObject != null)
            {
                var setResultMethod = command.CompletionSourceObject.GetType()
                    .GetMethod("SetResult");
                setResultMethod?.Invoke(command.CompletionSourceObject, new[] { result });
            }

            var duration = DateTime.UtcNow - startTime;
            
            // Add telemetry metrics
            activity?.SetTag("command.duration_ms", duration.TotalMilliseconds);
            activity?.SetTag("command.successful", true);
            
            if (duration.TotalMilliseconds > 1000) // Flag slow commands
            {
                activity?.SetTag("command.slow", true);
            }
            
            // Record successful operation for health monitoring
            _healthMonitoring.RecordSuccess("domain_command", duration);
            
            _logger.LogDebug("Completed command {CommandType} in {Duration}ms", 
                command.GetType().Name, duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            
            // Add error telemetry
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("command.duration_ms", duration.TotalMilliseconds);
            activity?.SetTag("command.successful", false);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);
            
            // Check if this is a critical error that should not be retried
            bool shouldRetry = ex switch
            {
                ArgumentNullException => false,      // Null arguments (more specific)
                ArgumentException => false,          // Invalid parameters (more general)
                NotSupportedException => false,      // Unsupported operations
                _ => true                           // Retry database/infrastructure errors (including InvalidOperationException)
            };
            
            activity?.SetTag("error.should_retry", shouldRetry);
            
            // Record failed operation for health monitoring
            _healthMonitoring.RecordFailure("domain_command", ex, duration);
            
            _logger.LogError(ex, "Failed to process command {CommandType}. ShouldRetry: {ShouldRetry}", 
                command.GetType().Name, shouldRetry);
            throw;
        }
    }

    #region User-Group Operations

    private async Task<bool> ProcessAddUserToGroup(AddUserToGroupCommand command)
    {
        var user = _entityGraph.GetUser(command.UserId);
        var group = _entityGraph.GetGroup(command.GroupId);

        // Add relationship in domain
        user.Parents.Add(group);
        group.Children.Add(user);

        // Normalizer will be executed by persistence service

        // Persist to database with error recovery
        await _errorRecovery.ExecuteWithRecoveryAsync(
            "database",
            async ct => await _persistenceService.PersistAddUserToGroupAsync(command.UserId, command.GroupId),
            maxRetries: 3,
            cancellationToken: _cancellationTokenSource.Token);

        // Log audit event
        await _eventPersistenceService.LogAddUserToGroupAsync(command.UserId, command.GroupId);
        
        // Invalidate caches for affected entities
        await _cache.InvalidateUserAsync(command.UserId);
        await _cache.InvalidateGroupAsync(command.GroupId);
        await _cache.InvalidateUserGroupsAsync(command.UserId);

        _logger.LogInformation("Added user {UserId} to group {GroupId}", command.UserId, command.GroupId);
        
        return true;
    }

    private async Task<bool> ProcessRemoveUserFromGroup(RemoveUserFromGroupCommand command)
    {
        var user = _entityGraph.GetUser(command.UserId);
        var group = _entityGraph.GetGroup(command.GroupId);

        // Remove relationship in domain
        user.Parents.Remove(group);
        group.Children.Remove(user);

        // Normalizer will be executed by persistence service

        // Persist to database
        await _persistenceService.PersistRemoveUserFromGroupAsync(command.UserId, command.GroupId);

        // Log audit event
        await _eventPersistenceService.LogRemoveUserFromGroupAsync(command.UserId, command.GroupId);
        
        // Invalidate caches for affected entities
        await _cache.InvalidateUserAsync(command.UserId);
        await _cache.InvalidateGroupAsync(command.GroupId);
        await _cache.InvalidateUserGroupsAsync(command.UserId);

        _logger.LogInformation("Removed user {UserId} from group {GroupId}", command.UserId, command.GroupId);
        
        return true;
    }

    #endregion

    #region User-Role Operations

    private async Task<bool> ProcessAssignUserToRole(AssignUserToRoleCommand command)
    {
        var user = _entityGraph.GetUser(command.UserId);
        var role = _entityGraph.GetRole(command.RoleId);

        user.Parents.Add(role);
        role.Children.Add(user);

        // Normalizer will be executed by persistence service

        // Persist to database
        await _persistenceService.PersistAssignUserToRoleAsync(command.UserId, command.RoleId);

        // Log audit event
        await _eventPersistenceService.LogAssignUserToRoleAsync(command.UserId, command.RoleId);
        
        // Invalidate caches for affected entities
        await _cache.InvalidateUserAsync(command.UserId);
        await _cache.InvalidateRoleAsync(command.RoleId);
        await _cache.InvalidateUserRolesAsync(command.UserId);

        _logger.LogInformation("Assigned user {UserId} to role {RoleId}", command.UserId, command.RoleId);
        
        return true;
    }

    private async Task<bool> ProcessUnAssignUserFromRole(UnAssignUserFromRoleCommand command)
    {
        var user = _entityGraph.GetUser(command.UserId);
        var role = _entityGraph.GetRole(command.RoleId);

        user.Parents.Remove(role);
        role.Children.Remove(user);

        // Normalizer will be executed by persistence service

        // Persist to database
        await _persistenceService.PersistUnAssignUserFromRoleAsync(command.UserId, command.RoleId);

        // Log audit event
        await _eventPersistenceService.LogUnAssignUserFromRoleAsync(command.UserId, command.RoleId);
        
        // Invalidate caches for affected entities
        await _cache.InvalidateUserAsync(command.UserId);
        await _cache.InvalidateRoleAsync(command.RoleId);
        await _cache.InvalidateUserRolesAsync(command.UserId);

        _logger.LogInformation("Unassigned user {UserId} from role {RoleId}", command.UserId, command.RoleId);
        
        return true;
    }

    #endregion

    #region Group-Role Operations

    private async Task<bool> ProcessAddRoleToGroup(AddRoleToGroupCommand command)
    {
        var group = _entityGraph.GetGroup(command.GroupId);
        var role = _entityGraph.GetRole(command.RoleId);

        group.Children.Add(role);
        role.Parents.Add(group);

        // Normalizer will be executed by persistence service

        // Persist to database
        await _persistenceService.PersistAddRoleToGroupAsync(command.GroupId, command.RoleId);

        // Log audit event
        await _eventPersistenceService.LogAddRoleToGroupAsync(command.GroupId, command.RoleId);

        _logger.LogInformation("Added role {RoleId} to group {GroupId}", command.RoleId, command.GroupId);
        
        return true;
    }

    private async Task<bool> ProcessRemoveRoleFromGroup(RemoveRoleFromGroupCommand command)
    {
        var group = _entityGraph.GetGroup(command.GroupId);
        var role = _entityGraph.GetRole(command.RoleId);

        group.Children.Remove(role);
        role.Parents.Remove(group);

        // Execute normalizer for database model synchronization
        // TODO: Update RemoveRoleFromGroupNormalizer to use async pattern
        // await ACS.Service.Delegates.Normalizers.RemoveRoleFromGroupNormalizer.ExecuteAsync(_dbContext, command.RoleId, command.GroupId);

        // Persist to database
        await _persistenceService.PersistRemoveRoleFromGroupAsync(command.GroupId, command.RoleId);

        // Log audit event
        await _eventPersistenceService.LogRemoveRoleFromGroupAsync(command.GroupId, command.RoleId);

        _logger.LogInformation("Removed role {RoleId} from group {GroupId}", command.RoleId, command.GroupId);
        
        return true;
    }

    #endregion

    #region Group-Group Operations

    private async Task<bool> ProcessAddGroupToGroup(AddGroupToGroupCommand command)
    {
        var parentGroup = _entityGraph.GetGroup(command.ParentGroupId);
        var childGroup = _entityGraph.GetGroup(command.ChildGroupId);

        // Prevent circular references
        if (WouldCreateCircularReference(childGroup, parentGroup))
        {
            throw new InvalidOperationException($"Adding group {command.ChildGroupId} to group {command.ParentGroupId} would create a circular reference");
        }

        parentGroup.Children.Add(childGroup);
        childGroup.Parents.Add(parentGroup);

        // Normalizer will be executed by persistence service

        // Persist to database
        await _persistenceService.PersistAddGroupToGroupAsync(command.ParentGroupId, command.ChildGroupId);

        // Log audit event
        await _eventPersistenceService.LogAddGroupToGroupAsync(command.ParentGroupId, command.ChildGroupId);

        _logger.LogInformation("Added group {ChildGroupId} to group {ParentGroupId}", command.ChildGroupId, command.ParentGroupId);
        
        return true;
    }

    private async Task<bool> ProcessRemoveGroupFromGroup(RemoveGroupFromGroupCommand command)
    {
        var parentGroup = _entityGraph.GetGroup(command.ParentGroupId);
        var childGroup = _entityGraph.GetGroup(command.ChildGroupId);

        parentGroup.Children.Remove(childGroup);
        childGroup.Parents.Remove(parentGroup);

        // Execute normalizer for database model synchronization
        // TODO: Update RemoveGroupFromGroupNormalizer to use async pattern
        // await ACS.Service.Delegates.Normalizers.RemoveGroupFromGroupNormalizer.ExecuteAsync(_dbContext, command.ParentGroupId, command.ChildGroupId);

        // Persist to database
        await _persistenceService.PersistRemoveGroupFromGroupAsync(command.ParentGroupId, command.ChildGroupId);

        // Log audit event
        await _eventPersistenceService.LogRemoveGroupFromGroupAsync(command.ParentGroupId, command.ChildGroupId);

        _logger.LogInformation("Removed group {ChildGroupId} from group {ParentGroupId}", command.ChildGroupId, command.ParentGroupId);
        
        return true;
    }

    #endregion

    #region Permission Operations

    private async Task<bool> ProcessAddPermissionToEntity(AddPermissionToEntityCommand command)
    {
        Entity? entity = null;

        // Find the entity
        if (_entityGraph.Users.TryGetValue(command.EntityId, out var user))
            entity = user;
        else if (_entityGraph.Groups.TryGetValue(command.EntityId, out var group))
            entity = group;
        else if (_entityGraph.Roles.TryGetValue(command.EntityId, out var role))
            entity = role;

        if (entity == null)
            throw new InvalidOperationException($"Entity {command.EntityId} not found");

        // Add to domain entity
        entity.Permissions.Add(command.Permission);

        // Execute normalizer for database model synchronization
        // TODO: Update AddPermissionToEntity normalizer to use async pattern
        // ACS.Service.Delegates.Normalizers.AddPermissionToEntity.Execute(command.Permission, command.EntityId);

        // Persist to database
        await _persistenceService.PersistAddPermissionToEntityAsync(command.EntityId, command.Permission);

        // Log audit event
        await _eventPersistenceService.LogAddPermissionToEntityAsync(command.EntityId, command.Permission);
        
        // Invalidate permission cache for the entity
        await _cache.InvalidateEntityPermissionsAsync(command.EntityId);

        _logger.LogInformation("Added permission {Uri}:{HttpVerb} to entity {EntityId}", 
            command.Permission.Uri, command.Permission.HttpVerb, command.EntityId);
        
        return true;
    }

    private async Task<bool> ProcessRemovePermissionFromEntity(RemovePermissionFromEntityCommand command)
    {
        Entity? entity = null;

        // Find the entity
        if (_entityGraph.Users.TryGetValue(command.EntityId, out var user))
            entity = user;
        else if (_entityGraph.Groups.TryGetValue(command.EntityId, out var group))
            entity = group;
        else if (_entityGraph.Roles.TryGetValue(command.EntityId, out var role))
            entity = role;

        if (entity == null)
            throw new InvalidOperationException($"Entity {command.EntityId} not found");

        // Remove from domain entity
        entity.Permissions.Remove(command.Permission);

        // Execute normalizer for database model synchronization
        // TODO: Update RemovePermissionFromEntity normalizer to use async pattern
        // ACS.Service.Delegates.Normalizers.RemovePermissionFromEntity.Execute(command.Permission, command.EntityId);

        // Persist to database
        await _persistenceService.PersistRemovePermissionFromEntityAsync(command.EntityId, command.Permission);

        // Log audit event
        await _eventPersistenceService.LogRemovePermissionFromEntityAsync(command.EntityId, command.Permission);
        
        // Invalidate permission cache for the entity
        await _cache.InvalidateEntityPermissionsAsync(command.EntityId);

        _logger.LogInformation("Removed permission {Uri}:{HttpVerb} from entity {EntityId}", 
            command.Permission.Uri, command.Permission.HttpVerb, command.EntityId);
        
        return true;
    }

    #endregion

    #region Query Operations

    private async Task<bool> ProcessCheckPermission(CheckPermissionCommand command)
    {
        Entity? entity = null;

        // Find the entity
        if (_entityGraph.Users.TryGetValue(command.EntityId, out var user))
            entity = user;
        else if (_entityGraph.Groups.TryGetValue(command.EntityId, out var group))
            entity = group;
        else if (_entityGraph.Roles.TryGetValue(command.EntityId, out var role))
            entity = role;

        if (entity == null)
            return false;

        var hasPermission = entity.HasPermission(command.Uri, command.HttpVerb);
        
        // Log audit event for permission checks (important for security auditing)
        await _eventPersistenceService.LogPermissionCheckAsync(command.EntityId, command.Uri, command.HttpVerb, hasPermission);
        
        _logger.LogDebug("Permission check for entity {EntityId} on {Uri}:{HttpVerb} = {HasPermission}", 
            command.EntityId, command.Uri, command.HttpVerb, hasPermission);
        
        return hasPermission;
    }

    private User ProcessGetUser(GetUserCommand command)
    {
        // Try cache first
        var cachedUser = _cache.GetUserAsync(command.UserId).GetAwaiter().GetResult();
        if (cachedUser != null)
        {
            _logger.LogTrace("User {UserId} retrieved from cache", command.UserId);
            return cachedUser;
        }
        
        // Fallback to entity graph
        var user = _entityGraph.GetUser(command.UserId);
        
        // Cache the result
        _cache.SetUserAsync(user).GetAwaiter().GetResult();
        
        return user;
    }

    private Group ProcessGetGroup(GetGroupCommand command)
    {
        // Try cache first
        var cachedGroup = _cache.GetGroupAsync(command.GroupId).GetAwaiter().GetResult();
        if (cachedGroup != null)
        {
            _logger.LogTrace("Group {GroupId} retrieved from cache", command.GroupId);
            return cachedGroup;
        }
        
        // Fallback to entity graph
        var group = _entityGraph.GetGroup(command.GroupId);
        
        // Cache the result
        _cache.SetGroupAsync(group).GetAwaiter().GetResult();
        
        return group;
    }

    private Role ProcessGetRole(GetRoleCommand command)
    {
        // Try cache first
        var cachedRole = _cache.GetRoleAsync(command.RoleId).GetAwaiter().GetResult();
        if (cachedRole != null)
        {
            _logger.LogTrace("Role {RoleId} retrieved from cache", command.RoleId);
            return cachedRole;
        }
        
        // Fallback to entity graph
        var role = _entityGraph.GetRole(command.RoleId);
        
        // Cache the result
        _cache.SetRoleAsync(role).GetAwaiter().GetResult();
        
        return role;
    }

    #endregion

    #region Utility Methods

    private bool WouldCreateCircularReference(Group childGroup, Group parentGroup)
    {
        // Check if parentGroup is already a descendant of childGroup
        var visited = new HashSet<int>();
        var queue = new Queue<Entity>();
        queue.Enqueue(childGroup);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (visited.Contains(current.Id))
                continue;

            visited.Add(current.Id);

            if (current.Id == parentGroup.Id)
                return true;

            foreach (var child in current.Children)
            {
                if (!visited.Contains(child.Id))
                    queue.Enqueue(child);
            }
        }

        return false;
    }

    #endregion

    #region CREATE Operations - Phase 2 requirement

    private async Task<User> ProcessCreateUser(CreateUserCommand command)
    {
        using var activity = ActivitySource.StartActivity("domain.create_user");
        activity?.SetTag("user.name", command.Name);
        activity?.SetTag("user.group_id", command.GroupId);
        
        // Thread-safe ID generation
        var newId = Interlocked.Increment(ref _nextUserId);
        activity?.SetTag("user.id", newId);
        
        // Create new user domain object
        var user = new User
        {
            Id = newId,
            Name = command.Name,
            Parents = new List<Entity>(),
            Children = new List<Entity>(),
            Permissions = new List<Permission>()
        };

        // Add to entity graph
        _entityGraph.Users[newId] = user;

        // Handle optional group assignment
        if (command.GroupId.HasValue)
        {
            var group = _entityGraph.GetGroup(command.GroupId.Value);
            user.Parents.Add(group);
            group.Children.Add(user);
            // Invalidate group cache due to child change
            await _cache.InvalidateGroupAsync(command.GroupId.Value);
        }

        // Handle optional role assignment
        if (command.RoleId.HasValue)
        {
            var role = _entityGraph.GetRole(command.RoleId.Value);
            user.Parents.Add(role);
            role.Children.Add(user);
            // Invalidate role cache due to child change
            await _cache.InvalidateRoleAsync(command.RoleId.Value);
        }

        // Persist to database with retry logic
        await _retryPolicy.ExecuteAsync(async () =>
            await _persistenceService.PersistCreateUserAsync(user.Id, user.Name, command.GroupId, command.RoleId));

        // Log audit event (non-critical, no retry needed)
        try
        {
            await _eventPersistenceService.LogCreateUserAsync(user.Id, user.Name, command.GroupId, command.RoleId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log audit event for user creation {UserId}", user.Id);
            // Don't fail the operation if audit logging fails
        }

        // Refresh normalizer collections to include new user (optimized)
        RefreshNormalizerCollectionsForUsers();
        
        // Cache the new user
        await _cache.SetUserAsync(user);

        _logger.LogInformation("Created user {UserId} with name '{UserName}'", user.Id, user.Name);
        
        return user;
    }

    private async Task<Group> ProcessCreateGroup(CreateGroupCommand command)
    {
        // Thread-safe ID generation
        var newId = Interlocked.Increment(ref _nextGroupId);
        
        // Create new group domain object
        var group = new Group
        {
            Id = newId,
            Name = command.Name,
            Parents = new List<Entity>(),
            Children = new List<Entity>(),
            Permissions = new List<Permission>()
        };

        // Add to entity graph
        _entityGraph.Groups[newId] = group;

        // Handle optional parent group assignment
        if (command.ParentGroupId.HasValue)
        {
            var parentGroup = _entityGraph.GetGroup(command.ParentGroupId.Value);
            
            // Prevent circular references
            if (WouldCreateCircularReference(group, parentGroup))
            {
                throw new InvalidOperationException($"Adding group {newId} to parent group {command.ParentGroupId.Value} would create a circular reference");
            }
            
            group.Parents.Add(parentGroup);
            parentGroup.Children.Add(group);
            // Invalidate parent group cache due to child change
            await _cache.InvalidateGroupAsync(command.ParentGroupId.Value);
        }

        // Persist to database
        await _persistenceService.PersistCreateGroupAsync(group.Id, group.Name, command.ParentGroupId);

        // Log audit event
        await _eventPersistenceService.LogCreateGroupAsync(group.Id, group.Name, command.ParentGroupId);

        // Refresh normalizer collections to include new group (optimized)
        RefreshNormalizerCollectionsForGroups();
        
        // Cache the new group
        await _cache.SetGroupAsync(group);

        _logger.LogInformation("Created group {GroupId} with name '{GroupName}'", group.Id, group.Name);
        
        return group;
    }

    private async Task<Role> ProcessCreateRole(CreateRoleCommand command)
    {
        // Thread-safe ID generation
        var newId = Interlocked.Increment(ref _nextRoleId);
        
        // Create new role domain object
        var role = new Role
        {
            Id = newId,
            Name = command.Name,
            Parents = new List<Entity>(),
            Children = new List<Entity>(),
            Permissions = new List<Permission>()
        };

        // Add to entity graph
        _entityGraph.Roles[newId] = role;

        // Handle optional group assignment
        if (command.GroupId.HasValue)
        {
            var group = _entityGraph.GetGroup(command.GroupId.Value);
            role.Parents.Add(group);
            group.Children.Add(role);
            // Invalidate group cache due to child change
            await _cache.InvalidateGroupAsync(command.GroupId.Value);
        }

        // Persist to database
        await _persistenceService.PersistCreateRoleAsync(role.Id, role.Name, command.GroupId);

        // Log audit event
        await _eventPersistenceService.LogCreateRoleAsync(role.Id, role.Name, command.GroupId);

        // Refresh normalizer collections to include new role (optimized)
        RefreshNormalizerCollectionsForRoles();
        
        // Cache the new role
        await _cache.SetRoleAsync(role);

        _logger.LogInformation("Created role {RoleId} with name '{RoleName}'", role.Id, role.Name);
        
        return role;
    }

    #endregion

    #region UPDATE Operations

    private async Task<User> ProcessUpdateUser(UpdateUserCommand command)
    {
        var user = _entityGraph.GetUser(command.UserId);
        user.Name = command.Name;

        // Update in database
        var dbUser = await _dbContext.Users.FindAsync(command.UserId);
        if (dbUser != null)
        {
            dbUser.Name = command.Name;
            await _dbContext.SaveChangesAsync();
        }

        // Log audit event
        await _eventPersistenceService.LogUpdateUserAsync(command.UserId, command.Name);
        
        // Invalidate and update cache
        await _cache.InvalidateUserAsync(command.UserId);
        await _cache.SetUserAsync(user);

        _logger.LogInformation("Updated user {UserId} with name '{UserName}'", command.UserId, command.Name);
        return user;
    }

    private async Task<Group> ProcessUpdateGroup(UpdateGroupCommand command)
    {
        var group = _entityGraph.GetGroup(command.GroupId);
        group.Name = command.Name;

        // Update in database
        var dbGroup = await _dbContext.Groups.FindAsync(command.GroupId);
        if (dbGroup != null)
        {
            dbGroup.Name = command.Name;
            await _dbContext.SaveChangesAsync();
        }

        // Log audit event
        await _eventPersistenceService.LogUpdateGroupAsync(command.GroupId, command.Name);
        
        // Invalidate and update cache
        await _cache.InvalidateGroupAsync(command.GroupId);
        await _cache.SetGroupAsync(group);

        _logger.LogInformation("Updated group {GroupId} with name '{GroupName}'", command.GroupId, command.Name);
        return group;
    }

    private async Task<Role> ProcessUpdateRole(UpdateRoleCommand command)
    {
        var role = _entityGraph.GetRole(command.RoleId);
        role.Name = command.Name;

        // Update in database
        var dbRole = await _dbContext.Roles.FindAsync(command.RoleId);
        if (dbRole != null)
        {
            dbRole.Name = command.Name;
            await _dbContext.SaveChangesAsync();
        }

        // Log audit event
        await _eventPersistenceService.LogUpdateRoleAsync(command.RoleId, command.Name);
        
        // Invalidate and update cache
        await _cache.InvalidateRoleAsync(command.RoleId);
        await _cache.SetRoleAsync(role);

        _logger.LogInformation("Updated role {RoleId} with name '{RoleName}'", command.RoleId, command.Name);
        return role;
    }

    #endregion

    #region DELETE Operations

    private async Task<bool> ProcessDeleteUser(DeleteUserCommand command)
    {
        var user = _entityGraph.GetUser(command.UserId);
        
        // Remove all relationships
        foreach (var parent in user.Parents.ToList())
        {
            parent.Children.Remove(user);
        }
        user.Parents.Clear();
        
        foreach (var child in user.Children.ToList())
        {
            child.Parents.Remove(user);
        }
        user.Children.Clear();

        // Remove from entity graph
        _entityGraph.Users.Remove(command.UserId);

        // Delete from database
        var dbUser = await _dbContext.Users.FindAsync(command.UserId);
        if (dbUser != null)
        {
            _dbContext.Users.Remove(dbUser);
            await _dbContext.SaveChangesAsync();
        }

        // Log audit event
        await _eventPersistenceService.LogDeleteUserAsync(command.UserId);
        
        // Invalidate cache
        await _cache.InvalidateUserAsync(command.UserId);

        _logger.LogInformation("Deleted user {UserId}", command.UserId);
        return true;
    }

    private async Task<bool> ProcessDeleteGroup(DeleteGroupCommand command)
    {
        var group = _entityGraph.GetGroup(command.GroupId);
        
        // Remove all relationships
        foreach (var parent in group.Parents.ToList())
        {
            parent.Children.Remove(group);
        }
        group.Parents.Clear();
        
        foreach (var child in group.Children.ToList())
        {
            child.Parents.Remove(group);
        }
        group.Children.Clear();

        // Remove from entity graph
        _entityGraph.Groups.Remove(command.GroupId);

        // Delete from database
        var dbGroup = await _dbContext.Groups.FindAsync(command.GroupId);
        if (dbGroup != null)
        {
            _dbContext.Groups.Remove(dbGroup);
            await _dbContext.SaveChangesAsync();
        }

        // Log audit event
        await _eventPersistenceService.LogDeleteGroupAsync(command.GroupId);
        
        // Invalidate cache
        await _cache.InvalidateGroupAsync(command.GroupId);

        _logger.LogInformation("Deleted group {GroupId}", command.GroupId);
        return true;
    }

    private async Task<bool> ProcessDeleteRole(DeleteRoleCommand command)
    {
        var role = _entityGraph.GetRole(command.RoleId);
        
        // Remove all relationships
        foreach (var parent in role.Parents.ToList())
        {
            parent.Children.Remove(role);
        }
        role.Parents.Clear();
        
        foreach (var child in role.Children.ToList())
        {
            child.Parents.Remove(role);
        }
        role.Children.Clear();

        // Remove from entity graph
        _entityGraph.Roles.Remove(command.RoleId);

        // Delete from database
        var dbRole = await _dbContext.Roles.FindAsync(command.RoleId);
        if (dbRole != null)
        {
            _dbContext.Roles.Remove(dbRole);
            await _dbContext.SaveChangesAsync();
        }

        // Log audit event
        await _eventPersistenceService.LogDeleteRoleAsync(command.RoleId);
        
        // Invalidate cache
        await _cache.InvalidateRoleAsync(command.RoleId);

        _logger.LogInformation("Deleted role {RoleId}", command.RoleId);
        return true;
    }

    #endregion

    #region QUERY Operations for Lists

    private List<User> ProcessGetUsers(GetUsersCommand command)
    {
        var users = _entityGraph.Users.Values
            .Skip((command.Page - 1) * command.PageSize)
            .Take(command.PageSize)
            .ToList();
        
        _logger.LogDebug("Retrieved {Count} users for page {Page}", users.Count, command.Page);
        return users;
    }

    private List<Group> ProcessGetGroups(GetGroupsCommand command)
    {
        var groups = _entityGraph.Groups.Values
            .Skip((command.Page - 1) * command.PageSize)
            .Take(command.PageSize)
            .ToList();
        
        _logger.LogDebug("Retrieved {Count} groups for page {Page}", groups.Count, command.Page);
        return groups;
    }

    private List<Role> ProcessGetRoles(GetRolesCommand command)
    {
        var roles = _entityGraph.Roles.Values
            .Skip((command.Page - 1) * command.PageSize)
            .Take(command.PageSize)
            .ToList();
        
        _logger.LogDebug("Retrieved {Count} roles for page {Page}", roles.Count, command.Page);
        return roles;
    }

    private List<Permission> ProcessGetEntityPermissions(GetEntityPermissionsCommand command)
    {
        Entity? entity = null;
        
        // Try to find the entity in users, groups, or roles
        if (_entityGraph.Users.ContainsKey(command.EntityId))
        {
            entity = _entityGraph.Users[command.EntityId];
        }
        else if (_entityGraph.Groups.ContainsKey(command.EntityId))
        {
            entity = _entityGraph.Groups[command.EntityId];
        }
        else if (_entityGraph.Roles.ContainsKey(command.EntityId))
        {
            entity = _entityGraph.Roles[command.EntityId];
        }
        
        if (entity == null)
        {
            _logger.LogWarning("Entity {EntityId} not found for permissions query", command.EntityId);
            return new List<Permission>();
        }
        
        var permissions = entity.Permissions
            .Skip((command.Page - 1) * command.PageSize)
            .Take(command.PageSize)
            .ToList();
        
        _logger.LogDebug("Retrieved {Count} permissions for entity {EntityId} on page {Page}", 
            permissions.Count, command.EntityId, command.Page);
        return permissions;
    }

    #endregion

    public async Task LoadEntityGraphAsync(CancellationToken cancellationToken = default)
    {
        await _entityGraph.LoadFromDatabaseAsync(cancellationToken);
        RefreshNormalizerCollections();
        
        // Initialize ID counters from existing data
        _nextUserId = _entityGraph.Users.Keys.Any() ? _entityGraph.Users.Keys.Max() : 0;
        _nextGroupId = _entityGraph.Groups.Keys.Any() ? _entityGraph.Groups.Keys.Max() : 0;
        _nextRoleId = _entityGraph.Roles.Keys.Any() ? _entityGraph.Roles.Keys.Max() : 0;
        
        // Warm up the cache with frequently accessed entities
        await _cache.WarmupAsync();
        
        _logger.LogInformation("Entity graph loaded and normalizer references hydrated. Next IDs: User={UserId}, Group={GroupId}, Role={RoleId}", 
            _nextUserId + 1, _nextGroupId + 1, _nextRoleId + 1);
    }

    private void RefreshNormalizerCollections()
    {
        // Full refresh - used during initialization
        RefreshNormalizerCollectionsInternal(true, true, true);
    }
    
    private void RefreshNormalizerCollectionsForUsers()
    {
        // Optimized refresh for user changes only
        RefreshNormalizerCollectionsInternal(true, false, false);
    }
    
    private void RefreshNormalizerCollectionsForGroups()
    {
        // Optimized refresh for group changes only
        RefreshNormalizerCollectionsInternal(false, true, false);
    }
    
    private void RefreshNormalizerCollectionsForRoles()
    {
        // Optimized refresh for role changes only
        RefreshNormalizerCollectionsInternal(false, false, true);
    }
    
    private void RefreshNormalizerCollectionsInternal(bool refreshUsers, bool refreshGroups, bool refreshRoles)
    {
        // Only refresh what's needed to minimize allocations
        List<User>? usersList = null;
        List<Group>? groupsList = null;
        List<Role>? rolesList = null;
        
        if (refreshUsers)
        {
            usersList = _entityGraph.Users.Values.ToList();
        }
        
        if (refreshGroups)
        {
            groupsList = _entityGraph.Groups.Values.ToList();
        }
        
        if (refreshRoles)
        {
            rolesList = _entityGraph.Roles.Values.ToList();
        }

        // Note: Normalizers now use database persistence directly via DbContext
        // The static collection pattern has been replaced with async database operations
        _logger.LogDebug("Entity collections refreshed - Users: {RefreshUsers}, Groups: {RefreshGroups}, Roles: {RefreshRoles}", 
            refreshUsers, refreshGroups, refreshRoles);
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _commandWriter.Complete();
        
        if (_processingTask != null)
        {
            try
            {
                _processingTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException))
            {
                // Expected cancellation
            }
        }
        
        _cancellationTokenSource.Dispose();
        _logger.LogInformation("AccessControlDomainService disposed");
    }
}