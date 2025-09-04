using ACS.VerticalHost.Services;
using ACS.VerticalHost.Commands;
using ACS.Service.Domain;
using ACS.Service.Services;
using static ACS.VerticalHost.Services.HandlerErrorHandling;
using static ACS.VerticalHost.Services.HandlerExtensions;
// Use fully qualified names to avoid conflicts

namespace ACS.VerticalHost.Handlers;

// Command Handlers
public class CreateUserCommandHandler : ICommandHandler<ACS.VerticalHost.Commands.CreateUserCommand, ACS.Service.Domain.User>
{
    private readonly IUserService _userService;
    private readonly ILogger<CreateUserCommandHandler> _logger;

    public CreateUserCommandHandler(IUserService userService, ILogger<CreateUserCommandHandler> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public async Task<ACS.Service.Domain.User> HandleAsync(ACS.VerticalHost.Commands.CreateUserCommand command, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(CreateUserCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { Name = command.Name, Email = command.Email }, correlationId);
        
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
            
            LogCommandSuccess(_logger, context, new { UserId = user.Id, Name = user.Name }, correlationId);
            return user;
        }
        catch (Exception ex)
        {
            return HandleCommandError<ACS.Service.Domain.User>(_logger, ex, context, correlationId);
        }
    }
}

public class UpdateUserCommandHandler : ICommandHandler<ACS.VerticalHost.Commands.UpdateUserCommand, ACS.Service.Domain.User>
{
    private readonly IUserService _userService;
    private readonly ILogger<UpdateUserCommandHandler> _logger;

    public UpdateUserCommandHandler(IUserService userService, ILogger<UpdateUserCommandHandler> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public async Task<ACS.Service.Domain.User> HandleAsync(ACS.VerticalHost.Commands.UpdateUserCommand command, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(UpdateUserCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { UserId = command.UserId, Name = command.Name }, correlationId);
        
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
            
            if (updatedUser == null) 
                throw new InvalidOperationException($"Updated user {command.UserId} not found");
            
            LogCommandSuccess(_logger, context, new { UserId = command.UserId, Name = updatedUser.Name }, correlationId);
            return updatedUser;
        }
        catch (Exception ex)
        {
            return HandleCommandError<ACS.Service.Domain.User>(_logger, ex, context, correlationId);
        }
    }
}

public class DeleteUserCommandHandler : ICommandHandler<ACS.VerticalHost.Commands.DeleteUserCommand, DeleteUserResult>
{
    private readonly IUserService _userService;
    private readonly ILogger<DeleteUserCommandHandler> _logger;

    public DeleteUserCommandHandler(IUserService userService, ILogger<DeleteUserCommandHandler> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public async Task<DeleteUserResult> HandleAsync(ACS.VerticalHost.Commands.DeleteUserCommand command, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(DeleteUserCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { UserId = command.UserId, ForceDelete = command.ForceDelete }, correlationId);
        
        try
        {
            // Call real user service
            var request = new ACS.Service.Requests.DeleteUserRequest
            {
                UserId = command.UserId,
                DeletedBy = command.DeletedBy ?? "system"
            };
            
            await _userService.DeleteAsync(request);
            
            var result = new DeleteUserResult
            {
                Success = true,
                UserId = command.UserId,
                DeletedAt = DateTime.UtcNow,
                Message = "User deleted successfully"
            };
            
            LogCommandSuccess(_logger, context, new { UserId = command.UserId }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleCommandError<DeleteUserResult>(_logger, ex, context, correlationId);
        }
    }
}

public class AddUserToGroupCommandHandler : ICommandHandler<ACS.VerticalHost.Commands.AddUserToGroupCommand, UserGroupOperationResult>
{
    private readonly IGroupService _groupService;
    private readonly ILogger<AddUserToGroupCommandHandler> _logger;

    public AddUserToGroupCommandHandler(IGroupService groupService, ILogger<AddUserToGroupCommandHandler> logger)
    {
        _groupService = groupService;
        _logger = logger;
    }

    public async Task<UserGroupOperationResult> HandleAsync(ACS.VerticalHost.Commands.AddUserToGroupCommand command, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(AddUserToGroupCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { UserId = command.UserId, GroupId = command.GroupId }, correlationId);
        
        try
        {
            // Call real group service
            await _groupService.AddUserToGroupAsync(command.UserId, command.GroupId, command.AddedBy ?? "system");
            
            var result = new UserGroupOperationResult
            {
                Success = true,
                UserId = command.UserId,
                GroupId = command.GroupId,
                OperationAt = DateTime.UtcNow,
                Operation = "Added",
                Message = "User added to group successfully"
            };
            
            LogCommandSuccess(_logger, context, new { UserId = command.UserId, GroupId = command.GroupId }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleCommandError<UserGroupOperationResult>(_logger, ex, context, correlationId);
        }
    }
}

public class RemoveUserFromGroupCommandHandler : ICommandHandler<ACS.VerticalHost.Commands.RemoveUserFromGroupCommand, UserGroupOperationResult>
{
    private readonly IGroupService _groupService;
    private readonly ILogger<RemoveUserFromGroupCommandHandler> _logger;

    public RemoveUserFromGroupCommandHandler(IGroupService groupService, ILogger<RemoveUserFromGroupCommandHandler> logger)
    {
        _groupService = groupService;
        _logger = logger;
    }

    public async Task<UserGroupOperationResult> HandleAsync(ACS.VerticalHost.Commands.RemoveUserFromGroupCommand command, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(RemoveUserFromGroupCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { UserId = command.UserId, GroupId = command.GroupId }, correlationId);
        
        try
        {
            // Call real group service
            await _groupService.RemoveUserFromGroupAsync(command.UserId, command.GroupId, command.RemovedBy ?? "system");
            
            var result = new UserGroupOperationResult
            {
                Success = true,
                UserId = command.UserId,
                GroupId = command.GroupId,
                OperationAt = DateTime.UtcNow,
                Operation = "Removed",
                Message = "User removed from group successfully"
            };
            
            LogCommandSuccess(_logger, context, new { UserId = command.UserId, GroupId = command.GroupId }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleCommandError<UserGroupOperationResult>(_logger, ex, context, correlationId);
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
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetUserQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { UserId = query.UserId }, correlationId);
        
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
                _logger.LogWarning("User {UserId} not found. CorrelationId: {CorrelationId}", query.UserId, correlationId);
                throw new InvalidOperationException($"User with ID {query.UserId} not found");
            }
            
            LogQuerySuccess(_logger, context, new { UserId = query.UserId, Name = user.Name }, correlationId);
            return user;
        }
        catch (Exception ex)
        {
            return HandleQueryError<User>(_logger, ex, context, correlationId);
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
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetUsersQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, 
            new { Page = query.Page, PageSize = query.PageSize, Search = query.Search }, correlationId);
        
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
            
            var result = users.ToList();
            LogQuerySuccess(_logger, context, 
                new { Page = query.Page, Count = result.Count }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<List<User>>(_logger, ex, context, correlationId);
        }
    }
}

public class GetUserGroupsQueryHandler : IQueryHandler<GetUserGroupsQuery, List<Group>>
{
    private readonly IGroupService _groupService;
    private readonly ILogger<GetUserGroupsQueryHandler> _logger;

    public GetUserGroupsQueryHandler(IGroupService groupService, ILogger<GetUserGroupsQueryHandler> logger)
    {
        _groupService = groupService;
        _logger = logger;
    }

    public async Task<List<Group>> HandleAsync(GetUserGroupsQuery query, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetUserGroupsQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { UserId = query.UserId }, correlationId);
        
        try
        {
            // Use proper service layer abstraction - single abstraction level
            var groups = await _groupService.GetGroupsByUserAsync(query.UserId);
            
            var result = groups.ToList();
            LogQuerySuccess(_logger, context, 
                new { UserId = query.UserId, Count = result.Count }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<List<Group>>(_logger, ex, context, correlationId);
        }
    }
}