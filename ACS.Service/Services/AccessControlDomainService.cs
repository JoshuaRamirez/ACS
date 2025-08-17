using Microsoft.Extensions.Logging;
using ACS.Service.Infrastructure;
using ACS.Service.Domain;
using ACS.Service.Data;
using System.Threading.Channels;

namespace ACS.Service.Services;

public class AccessControlDomainService
{
    private readonly InMemoryEntityGraph _entityGraph;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<AccessControlDomainService> _logger;
    private readonly Channel<DomainCommand> _commandChannel;
    private readonly ChannelWriter<DomainCommand> _commandWriter;
    private readonly ChannelReader<DomainCommand> _commandReader;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task _processingTask;

    public AccessControlDomainService(
        InMemoryEntityGraph entityGraph,
        ApplicationDbContext dbContext,
        ILogger<AccessControlDomainService> logger)
    {
        _entityGraph = entityGraph;
        _dbContext = dbContext;
        _logger = logger;
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

        // Start background processing task
        _processingTask = Task.Run(ProcessCommandsAsync, _cancellationTokenSource.Token);
        
        _logger.LogInformation("AccessControlDomainService initialized with single-threaded command processing");
    }

    public async Task<TResult> ExecuteCommandAsync<TResult>(DomainCommand<TResult> command)
    {
        var completionSource = new TaskCompletionSource<TResult>();
        command.CompletionSource = completionSource;

        await _commandWriter.WriteAsync(command, _cancellationTokenSource.Token);
        
        _logger.LogDebug("Queued command {CommandType} for processing", command.GetType().Name);
        
        return await completionSource.Task;
    }

    public async Task ExecuteCommandAsync(DomainCommand command)
    {
        var completionSource = new TaskCompletionSource<bool>();
        command.VoidCompletionSource = completionSource;

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
        _logger.LogDebug("Processing command {CommandType}", command.GetType().Name);
        
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Execute the command using pattern matching
            object? result = command switch
            {
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
                CheckPermissionCommand cmd => ProcessCheckPermission(cmd),
                GetUserCommand cmd => ProcessGetUser(cmd),
                GetGroupCommand cmd => ProcessGetGroup(cmd),
                GetRoleCommand cmd => ProcessGetRole(cmd),
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
            _logger.LogDebug("Completed command {CommandType} in {Duration}ms", 
                command.GetType().Name, duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process command {CommandType}", command.GetType().Name);
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

        // Execute normalizer for database persistence
        // Note: Normalizer will handle database updates
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

        _logger.LogInformation("Assigned user {UserId} to role {RoleId}", command.UserId, command.RoleId);
        
        return true;
    }

    private async Task<bool> ProcessUnAssignUserFromRole(UnAssignUserFromRoleCommand command)
    {
        var user = _entityGraph.GetUser(command.UserId);
        var role = _entityGraph.GetRole(command.RoleId);

        user.Parents.Remove(role);
        role.Children.Remove(user);

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

        _logger.LogInformation("Added role {RoleId} to group {GroupId}", command.RoleId, command.GroupId);
        
        return true;
    }

    private async Task<bool> ProcessRemoveRoleFromGroup(RemoveRoleFromGroupCommand command)
    {
        var group = _entityGraph.GetGroup(command.GroupId);
        var role = _entityGraph.GetRole(command.RoleId);

        group.Children.Remove(role);
        role.Parents.Remove(group);

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

        _logger.LogInformation("Added group {ChildGroupId} to group {ParentGroupId}", command.ChildGroupId, command.ParentGroupId);
        
        return true;
    }

    private async Task<bool> ProcessRemoveGroupFromGroup(RemoveGroupFromGroupCommand command)
    {
        var parentGroup = _entityGraph.GetGroup(command.ParentGroupId);
        var childGroup = _entityGraph.GetGroup(command.ChildGroupId);

        parentGroup.Children.Remove(childGroup);
        childGroup.Parents.Remove(parentGroup);

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

        entity.AddPermission(command.Permission);

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

        entity.RemovePermission(command.Permission);

        _logger.LogInformation("Removed permission {Uri}:{HttpVerb} from entity {EntityId}", 
            command.Permission.Uri, command.Permission.HttpVerb, command.EntityId);
        
        return true;
    }

    #endregion

    #region Query Operations

    private bool ProcessCheckPermission(CheckPermissionCommand command)
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
        
        _logger.LogDebug("Permission check for entity {EntityId} on {Uri}:{HttpVerb} = {HasPermission}", 
            command.EntityId, command.Uri, command.HttpVerb, hasPermission);
        
        return hasPermission;
    }

    private User ProcessGetUser(GetUserCommand command)
    {
        return _entityGraph.GetUser(command.UserId);
    }

    private Group ProcessGetGroup(GetGroupCommand command)
    {
        return _entityGraph.GetGroup(command.GroupId);
    }

    private Role ProcessGetRole(GetRoleCommand command)
    {
        return _entityGraph.GetRole(command.RoleId);
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

    public async Task LoadEntityGraphAsync(CancellationToken cancellationToken = default)
    {
        await _entityGraph.LoadFromDatabaseAsync(cancellationToken);
        _entityGraph.HydrateNormalizerReferences();
        
        _logger.LogInformation("Entity graph loaded and normalizer references hydrated");
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _commandWriter.Complete();
        
        try
        {
            _processingTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException))
        {
            // Expected cancellation
        }
        
        _cancellationTokenSource.Dispose();
        _logger.LogInformation("AccessControlDomainService disposed");
    }
}