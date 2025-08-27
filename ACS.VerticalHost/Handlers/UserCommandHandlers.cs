using ACS.VerticalHost.Services;
using ACS.VerticalHost.Commands;
using ACS.Service.Domain;
using ACS.Service.Services;
// Use fully qualified names to avoid conflicts

namespace ACS.VerticalHost.Handlers;

// Command Handlers
public class CreateUserCommandHandler : ICommandHandler<ACS.VerticalHost.Commands.CreateUserCommand>
{
    private readonly IUserService _userService;
    private readonly ILogger<CreateUserCommandHandler> _logger;

    public CreateUserCommandHandler(IUserService userService, ILogger<CreateUserCommandHandler> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public async Task<object?> HandleAsync(ACS.VerticalHost.Commands.CreateUserCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating user: {Name}, {Email}", command.Name, command.Email);
        
        try
        {
            // Call real user service
            var request = new ACS.Service.Requests.CreateUserRequest
            {
                Name = command.Name ?? throw new ArgumentException("Name is required"),
                CreatedBy = command.CreatedBy ?? "system"
            };
            
            var response = await _userService.CreateAsync(request);
            var user = response.User;

            if (user == null) throw new InvalidOperationException("User creation failed - null user returned");
            _logger.LogInformation("User created successfully: {UserId}", user.Id);
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create user {Name}, {Email}", command.Name, command.Email);
            throw;
        }
    }
}

public class UpdateUserCommandHandler : ICommandHandler<ACS.VerticalHost.Commands.UpdateUserCommand>
{
    private readonly IUserService _userService;
    private readonly ILogger<UpdateUserCommandHandler> _logger;

    public UpdateUserCommandHandler(IUserService userService, ILogger<UpdateUserCommandHandler> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public async Task<object?> HandleAsync(ACS.VerticalHost.Commands.UpdateUserCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating user: {UserId}", command.UserId);
        
        try
        {
            // Call real user service
            var updateRequest = new ACS.Service.Requests.UpdateUserRequest
            {
                UserId = command.UserId,
                Name = command.Name ?? throw new ArgumentException("Name is required"),
                UpdatedBy = command.UpdatedBy ?? "system"
            };
            
            var updateResponse = await _userService.UpdateAsync(updateRequest);
            
            var getRequest = new ACS.Service.Requests.GetUserRequest
            {
                UserId = command.UserId
            };
            
            var getUserResponse = await _userService.GetByIdAsync(getRequest);
            var updatedUser = getUserResponse.User;
            _logger.LogInformation("User {UserId} updated successfully", command.UserId);
            return updatedUser;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update user {UserId}", command.UserId);
            throw;
        }
    }
}

public class DeleteUserCommandHandler : ICommandHandler<ACS.VerticalHost.Commands.DeleteUserCommand>
{
    private readonly IUserService _userService;
    private readonly ILogger<DeleteUserCommandHandler> _logger;

    public DeleteUserCommandHandler(IUserService userService, ILogger<DeleteUserCommandHandler> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public async Task<object?> HandleAsync(ACS.VerticalHost.Commands.DeleteUserCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting user: {UserId}, ForceDelete: {ForceDelete}", command.UserId, command.ForceDelete);
        
        try
        {
            // Call real user service
            var request = new ACS.Service.Requests.DeleteUserRequest
            {
                UserId = command.UserId,
                DeletedBy = command.DeletedBy ?? "system"
            };
            
            await _userService.DeleteAsync(request);
            
            _logger.LogInformation("User {UserId} deleted successfully", command.UserId);
            return new { Success = true, UserId = command.UserId, DeletedAt = DateTime.UtcNow };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete user {UserId}", command.UserId);
            throw;
        }
    }
}

public class AddUserToGroupCommandHandler : ICommandHandler<ACS.VerticalHost.Commands.AddUserToGroupCommand>
{
    private readonly IGroupService _groupService;
    private readonly ILogger<AddUserToGroupCommandHandler> _logger;

    public AddUserToGroupCommandHandler(IGroupService groupService, ILogger<AddUserToGroupCommandHandler> logger)
    {
        _groupService = groupService;
        _logger = logger;
    }

    public async Task<object?> HandleAsync(ACS.VerticalHost.Commands.AddUserToGroupCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Adding user {UserId} to group {GroupId}", command.UserId, command.GroupId);
        
        try
        {
            // Call real group service
            await _groupService.AddUserToGroupAsync(command.UserId, command.GroupId, command.AddedBy ?? "system");
            
            _logger.LogInformation("User {UserId} added to group {GroupId} successfully", command.UserId, command.GroupId);
            return new { Success = true, UserId = command.UserId, GroupId = command.GroupId, AddedAt = DateTime.UtcNow };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add user {UserId} to group {GroupId}", command.UserId, command.GroupId);
            throw;
        }
    }
}

public class RemoveUserFromGroupCommandHandler : ICommandHandler<ACS.VerticalHost.Commands.RemoveUserFromGroupCommand>
{
    private readonly IGroupService _groupService;
    private readonly ILogger<RemoveUserFromGroupCommandHandler> _logger;

    public RemoveUserFromGroupCommandHandler(IGroupService groupService, ILogger<RemoveUserFromGroupCommandHandler> logger)
    {
        _groupService = groupService;
        _logger = logger;
    }

    public async Task<object?> HandleAsync(ACS.VerticalHost.Commands.RemoveUserFromGroupCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Removing user {UserId} from group {GroupId}", command.UserId, command.GroupId);
        
        try
        {
            // Call real group service
            await _groupService.RemoveUserFromGroupAsync(command.UserId, command.GroupId, command.RemovedBy ?? "system");
            
            _logger.LogInformation("User {UserId} removed from group {GroupId} successfully", command.UserId, command.GroupId);
            return new { Success = true, UserId = command.UserId, GroupId = command.GroupId, RemovedAt = DateTime.UtcNow };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove user {UserId} from group {GroupId}", command.UserId, command.GroupId);
            throw;
        }
    }
}

// Query Handlers
public class GetUserQueryHandler : IQueryHandler<GetUserQuery, User>
{
    private readonly IUserService _userService;
    private readonly ILogger<GetUserQueryHandler> _logger;

    public GetUserQueryHandler(IUserService userService, ILogger<GetUserQueryHandler> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public async Task<User> HandleAsync(GetUserQuery query, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting user: {UserId}", query.UserId);
        
        try
        {
            var request = new ACS.Service.Requests.GetUserRequest
            {
                UserId = query.UserId
            };
            
            var response = await _userService.GetByIdAsync(request);
            var user = response.User;
            
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found", query.UserId);
                throw new InvalidOperationException($"User with ID {query.UserId} not found");
            }
            
            _logger.LogDebug("User {UserId} retrieved successfully", query.UserId);
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve user {UserId}", query.UserId);
            throw;
        }
    }
}

public class GetUsersQueryHandler : IQueryHandler<GetUsersQuery, List<User>>
{
    private readonly IUserService _userService;
    private readonly ILogger<GetUsersQueryHandler> _logger;

    public GetUsersQueryHandler(IUserService userService, ILogger<GetUsersQueryHandler> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public async Task<List<User>> HandleAsync(GetUsersQuery query, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting users: Page={Page}, PageSize={PageSize}, Search={Search}", 
            query.Page, query.PageSize, query.Search);
        
        try
        {
            var request = new ACS.Service.Requests.GetUsersRequest
            {
                Page = query.Page,
                PageSize = query.PageSize,
                Search = query.Search
            };
            
            var response = await _userService.GetAllAsync(request);
            var users = response.Users;
            
            _logger.LogDebug("Retrieved {Count} users for page {Page}", users.Count(), query.Page);
            return users.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve users");
            throw;
        }
    }
}

public class GetUserGroupsQueryHandler : IQueryHandler<GetUserGroupsQuery, List<Group>>
{
    private readonly IGroupService _groupService;
    private readonly ACS.Service.Infrastructure.InMemoryEntityGraph _entityGraph;
    private readonly ILogger<GetUserGroupsQueryHandler> _logger;

    public GetUserGroupsQueryHandler(IGroupService groupService, ACS.Service.Infrastructure.InMemoryEntityGraph entityGraph, ILogger<GetUserGroupsQueryHandler> logger)
    {
        _groupService = groupService;
        _entityGraph = entityGraph;
        _logger = logger;
    }

    public async Task<List<Group>> HandleAsync(GetUserGroupsQuery query, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting groups for user: {UserId}", query.UserId);
        
        try
        {
            // Get group IDs for the user from the in-memory entity graph
            var groupIds = _entityGraph.GetUserGroups(query.UserId);
            
            // Retrieve the full group objects
            var groups = new List<ACS.Service.Domain.Group>();
            foreach (var groupId in groupIds)
            {
                var group = await _groupService.GetGroupByIdAsync(groupId);
                if (group != null)
                {
                    groups.Add(group);
                }
            }
            
            _logger.LogDebug("Retrieved {Count} groups for user {UserId}", groups.Count, query.UserId);
            return groups;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve groups for user {UserId}", query.UserId);
            throw;
        }
    }
}