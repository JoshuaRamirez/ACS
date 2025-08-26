using ACS.Service.Data;
using ACS.Service.Data.Models;
using ACS.Service.Delegates.Normalizers;
using ACS.Service.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACS.Service.Services;

public class CommandProcessingService : ICommandProcessingService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CommandProcessingService> _logger;

    public CommandProcessingService(
        ApplicationDbContext dbContext, 
        IUnitOfWork unitOfWork, 
        ILogger<CommandProcessingService> logger)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<TResult> ExecuteCommandAsync<TResult>(Infrastructure.WebRequestCommand command)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var commandId = command.RequestId;
        var commandType = command.GetType().Name;
        var userId = command.UserId;
        
        _logger.LogInformation("Command execution started: {CommandType}({CommandId}) by User:{UserId}", 
            commandType, commandId, userId);
            
        using var transaction = await _unitOfWork.BeginTransactionAsync();
        try
        {
            var result = await ProcessCommandWithResult<TResult>(command);
            await _unitOfWork.SaveChangesAsync();
            await transaction.CommitAsync();
            
            _logger.LogInformation("Command {CommandType}({CommandId}) executed successfully by User:{UserId} in {ElapsedMs}ms", 
                commandType, commandId, userId, stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Command {CommandType}({CommandId}) failed for User:{UserId} after {ElapsedMs}ms", 
                commandType, commandId, userId, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task ExecuteCommandAsync(Infrastructure.WebRequestCommand command)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var commandId = command.RequestId;
        var commandType = command.GetType().Name;
        var userId = command.UserId;
        
        _logger.LogInformation("Command execution started: {CommandType}({CommandId}) by User:{UserId}", 
            commandType, commandId, userId);
            
        using var transaction = await _unitOfWork.BeginTransactionAsync();
        try
        {
            await ProcessCommand(command);
            await _unitOfWork.SaveChangesAsync();
            await transaction.CommitAsync();
            
            _logger.LogInformation("Command {CommandType}({CommandId}) executed successfully by User:{UserId} in {ElapsedMs}ms", 
                commandType, commandId, userId, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Command {CommandType}({CommandId}) failed for User:{UserId} after {ElapsedMs}ms", 
                commandType, commandId, userId, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    private async Task<TResult> ProcessCommandWithResult<TResult>(Infrastructure.WebRequestCommand command)
    {
        switch (command)
        {
            case Infrastructure.GetUsersCommand getUsersCmd:
                return (TResult)(object)await HandleGetUsersCommand(getUsersCmd);
            case Infrastructure.GetUserCommand getUserCmd:
                return (TResult)(object)await HandleGetUserCommand(getUserCmd);
            case Infrastructure.CreateUserCommand createUserCmd:
                return (TResult)(object)await HandleCreateUserCommand(createUserCmd);
            case Infrastructure.UpdateUserCommand updateUserCmd:
                return (TResult)(object)await HandleUpdateUserCommand(updateUserCmd);
            case Infrastructure.GetGroupsCommand getGroupsCmd:
                return (TResult)(object)await HandleGetGroupsCommand(getGroupsCmd);
            case Infrastructure.GetGroupCommand getGroupCmd:
                return (TResult)(object)await HandleGetGroupCommand(getGroupCmd);
            case Infrastructure.CreateGroupCommand createGroupCmd:
                return (TResult)(object)await HandleCreateGroupCommand(createGroupCmd);
            case Infrastructure.GetRolesCommand getRolesCmd:
                return (TResult)(object)await HandleGetRolesCommand(getRolesCmd);
            case Infrastructure.GetRoleCommand getRoleCmd:
                return (TResult)(object)await HandleGetRoleCommand(getRoleCmd);
            case Infrastructure.CreateRoleCommand createRoleCmd:
                return (TResult)(object)await HandleCreateRoleCommand(createRoleCmd);
            default:
                throw new NotSupportedException($"Command type {command.GetType().Name} is not supported for result operations");
        }
    }

    private async Task ProcessCommand(Infrastructure.WebRequestCommand command)
    {
        switch (command)
        {
            case Infrastructure.AddUserToGroupCommand addUserToGroupCmd:
                await HandleAddUserToGroupCommand(addUserToGroupCmd);
                break;
            case Infrastructure.RemoveUserFromGroupCommand removeUserFromGroupCmd:
                await HandleRemoveUserFromGroupCommand(removeUserFromGroupCmd);
                break;
            case Infrastructure.AssignUserToRoleCommand assignUserToRoleCmd:
                await HandleAssignUserToRoleCommand(assignUserToRoleCmd);
                break;
            case Infrastructure.UnAssignUserFromRoleCommand unAssignUserFromRoleCmd:
                await HandleUnAssignUserFromRoleCommand(unAssignUserFromRoleCmd);
                break;
            case Infrastructure.DeleteUserCommand deleteUserCmd:
                await HandleDeleteUserCommand(deleteUserCmd);
                break;
            case Infrastructure.DeleteGroupCommand deleteGroupCmd:
                await HandleDeleteGroupCommand(deleteGroupCmd);
                break;
            case Infrastructure.DeleteRoleCommand deleteRoleCmd:
                await HandleDeleteRoleCommand(deleteRoleCmd);
                break;
            default:
                throw new NotSupportedException($"Command type {command.GetType().Name} is not supported for void operations");
        }
    }

    // Query handlers that return results
    private async Task<object> HandleGetUsersCommand(Infrastructure.GetUsersCommand command)
    {
        var users = await _dbContext.Users
            .Include(u => u.Entity)
            .OrderBy(u => u.Id)
            .Skip((command.Page - 1) * command.PageSize)
            .Take(command.PageSize)
            .ToListAsync();

        return new { Users = users, TotalCount = await _dbContext.Users.CountAsync() };
    }

    private async Task<object> HandleGetUserCommand(Infrastructure.GetUserCommand command)
    {
        var user = await _dbContext.Users
            .Include(u => u.Entity)
            .Include(u => u.UserGroups)
            .ThenInclude(ug => ug.Group)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == command.TargetUserId);

        if (user == null)
        {
            throw new InvalidOperationException($"User {command.TargetUserId} not found");
        }

        return user;
    }

    private async Task<object> HandleCreateUserCommand(Infrastructure.CreateUserCommand command)
    {
        // Create entity first
        var entity = new Entity
        {
            EntityType = "User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Entities.Add(entity);
        await _dbContext.SaveChangesAsync(); // Save to get the ID

        // Create user
        var user = new User
        {
            Name = command.Name,
            EntityId = entity.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(); // Save to get the ID

        return user;
    }

    private async Task<object> HandleUpdateUserCommand(Infrastructure.UpdateUserCommand command)
    {
        var user = await _dbContext.Users.FindAsync(command.TargetUserId);
        if (user == null)
        {
            throw new InvalidOperationException($"User {command.TargetUserId} not found");
        }

        user.Name = command.Name;
        user.UpdatedAt = DateTime.UtcNow;
        _dbContext.Users.Update(user);

        return user;
    }

    private async Task<object> HandleGetGroupsCommand(Infrastructure.GetGroupsCommand command)
    {
        var groups = await _dbContext.Groups
            .Include(g => g.Entity)
            .OrderBy(g => g.Id)
            .Skip((command.Page - 1) * command.PageSize)
            .Take(command.PageSize)
            .ToListAsync();

        return new { Groups = groups, TotalCount = await _dbContext.Groups.CountAsync() };
    }

    private async Task<object> HandleGetGroupCommand(Infrastructure.GetGroupCommand command)
    {
        var group = await _dbContext.Groups
            .Include(g => g.Entity)
            .Include(g => g.UserGroups)
            .ThenInclude(ug => ug.User)
            .Include(g => g.GroupRoles)
            .ThenInclude(gr => gr.Role)
            .FirstOrDefaultAsync(g => g.Id == command.GroupId);

        if (group == null)
        {
            throw new InvalidOperationException($"Group {command.GroupId} not found");
        }

        return group;
    }

    private async Task<object> HandleCreateGroupCommand(Infrastructure.CreateGroupCommand command)
    {
        // Create entity first
        var entity = new Entity
        {
            EntityType = "Group",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Entities.Add(entity);
        await _dbContext.SaveChangesAsync(); // Save to get the ID

        // Create group
        var group = new Group
        {
            Name = command.Name,
            EntityId = entity.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Groups.Add(group);
        await _dbContext.SaveChangesAsync(); // Save to get the ID

        // Handle parent group relationship if specified
        if (command.ParentGroupId.HasValue)
        {
            // Load domain objects for normalizer
            var parentGroup = await _dbContext.Groups.Include(g => g.Entity)
                .FirstOrDefaultAsync(g => g.Id == command.ParentGroupId.Value);
            if (parentGroup != null)
            {
                var childGroupDomain = new Domain.Group { Id = group.Id, Name = group.Name };
                var parentGroupDomain = new Domain.Group { Id = parentGroup.Id, Name = parentGroup.Name };
                
                AddGroupToGroupNormalizer.Execute(childGroupDomain, parentGroupDomain);
                
                // Create junction table entry
                var groupHierarchy = new Data.Models.GroupHierarchy 
                { 
                    ChildGroupId = group.Id, 
                    ParentGroupId = parentGroup.Id 
                };
                _dbContext.GroupHierarchies.Add(groupHierarchy);
                await _dbContext.SaveChangesAsync();
            }
        }

        return group;
    }

    private async Task<object> HandleGetRolesCommand(Infrastructure.GetRolesCommand command)
    {
        var roles = await _dbContext.Roles
            .Include(r => r.Entity)
            .OrderBy(r => r.Id)
            .Skip((command.Page - 1) * command.PageSize)
            .Take(command.PageSize)
            .ToListAsync();

        return new { Roles = roles, TotalCount = await _dbContext.Roles.CountAsync() };
    }

    private async Task<object> HandleGetRoleCommand(Infrastructure.GetRoleCommand command)
    {
        var role = await _dbContext.Roles
            .Include(r => r.Entity)
            .Include(r => r.UserRoles)
            .ThenInclude(ur => ur.User)
            .Include(r => r.GroupRoles)
            .ThenInclude(gr => gr.Group)
            .FirstOrDefaultAsync(r => r.Id == command.RoleId);

        if (role == null)
        {
            throw new InvalidOperationException($"Role {command.RoleId} not found");
        }

        return role;
    }

    private async Task<object> HandleCreateRoleCommand(Infrastructure.CreateRoleCommand command)
    {
        // Create entity first
        var entity = new Entity
        {
            EntityType = "Role",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Entities.Add(entity);
        await _dbContext.SaveChangesAsync(); // Save to get the ID

        // Create role
        var role = new Role
        {
            Name = command.Name,
            EntityId = entity.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync(); // Save to get the ID

        // Handle group relationship if specified
        if (command.GroupId.HasValue)
        {
            // Load domain objects for normalizer
            var group = await _dbContext.Groups.Include(g => g.Entity)
                .FirstOrDefaultAsync(g => g.Id == command.GroupId.Value);
            if (group != null)
            {
                var roleDomain = new Domain.Role { Id = role.Id, Name = role.Name };
                var groupDomain = new Domain.Group { Id = group.Id, Name = group.Name };
                
                AddRoleToGroupNormalizer.Execute(roleDomain, groupDomain);
                
                // Create junction table entry
                var groupRole = new Data.Models.GroupRole { GroupId = group.Id, RoleId = role.Id };
                _dbContext.GroupRoles.Add(groupRole);
                await _dbContext.SaveChangesAsync();
            }
        }

        return role;
    }

    // Command handlers that don't return results
    private async Task HandleAddUserToGroupCommand(Infrastructure.AddUserToGroupCommand command)
    {
        // Load domain objects
        var user = await _dbContext.Users.Include(u => u.Entity)
            .FirstOrDefaultAsync(u => u.Id == command.TargetUserId);
        var group = await _dbContext.Groups.Include(g => g.Entity)
            .FirstOrDefaultAsync(g => g.Id == command.GroupId);

        if (user != null && group != null)
        {
            var userDomain = new Domain.User { Id = user.Id, Name = user.Name };
            var groupDomain = new Domain.Group { Id = group.Id, Name = group.Name };
            
            AddUserToGroupNormalizer.Execute(userDomain, groupDomain);
            
            // Create junction table entry
            var userGroup = new Data.Models.UserGroup { UserId = user.Id, GroupId = group.Id };
            _dbContext.UserGroups.Add(userGroup);
            await _dbContext.SaveChangesAsync();
        }
    }

    private async Task HandleRemoveUserFromGroupCommand(Infrastructure.RemoveUserFromGroupCommand command)
    {
        // Load domain objects
        var user = await _dbContext.Users.Include(u => u.Entity)
            .FirstOrDefaultAsync(u => u.Id == command.TargetUserId);
        var group = await _dbContext.Groups.Include(g => g.Entity)
            .FirstOrDefaultAsync(g => g.Id == command.GroupId);

        if (user != null && group != null)
        {
            var userDomain = new Domain.User { Id = user.Id, Name = user.Name };
            var groupDomain = new Domain.Group { Id = group.Id, Name = group.Name };
            
            RemoveUserFromGroupNormalizer.Execute(userDomain, groupDomain);
            
            // Remove junction table entry
            var userGroup = await _dbContext.UserGroups
                .FirstOrDefaultAsync(ug => ug.UserId == user.Id && ug.GroupId == group.Id);
            if (userGroup != null)
            {
                _dbContext.UserGroups.Remove(userGroup);
                await _dbContext.SaveChangesAsync();
            }
        }
    }

    private async Task HandleAssignUserToRoleCommand(Infrastructure.AssignUserToRoleCommand command)
    {
        // Load domain objects
        var user = await _dbContext.Users.Include(u => u.Entity)
            .FirstOrDefaultAsync(u => u.Id == command.TargetUserId);
        var role = await _dbContext.Roles.Include(r => r.Entity)
            .FirstOrDefaultAsync(r => r.Id == command.RoleId);

        if (user != null && role != null)
        {
            var userDomain = new Domain.User { Id = user.Id, Name = user.Name };
            var roleDomain = new Domain.Role { Id = role.Id, Name = role.Name };
            
            AssignUserToRoleNormalizer.Execute(userDomain, roleDomain);
            
            // Create junction table entry
            var userRole = new Data.Models.UserRole { UserId = user.Id, RoleId = role.Id };
            _dbContext.UserRoles.Add(userRole);
            await _dbContext.SaveChangesAsync();
        }
    }

    private async Task HandleUnAssignUserFromRoleCommand(Infrastructure.UnAssignUserFromRoleCommand command)
    {
        // Load domain objects
        var user = await _dbContext.Users.Include(u => u.Entity)
            .FirstOrDefaultAsync(u => u.Id == command.TargetUserId);
        var role = await _dbContext.Roles.Include(r => r.Entity)
            .FirstOrDefaultAsync(r => r.Id == command.RoleId);

        if (user != null && role != null)
        {
            var userDomain = new Domain.User { Id = user.Id, Name = user.Name };
            var roleDomain = new Domain.Role { Id = role.Id, Name = role.Name };
            
            UnAssignUserFromRoleNormalizer.Execute(userDomain, roleDomain);
            
            // Remove junction table entry
            var userRole = await _dbContext.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id);
            if (userRole != null)
            {
                _dbContext.UserRoles.Remove(userRole);
                await _dbContext.SaveChangesAsync();
            }
        }
    }

    private async Task HandleDeleteUserCommand(Infrastructure.DeleteUserCommand command)
    {
        var user = await _dbContext.Users.Include(u => u.Entity).FirstOrDefaultAsync(u => u.Id == command.TargetUserId);
        if (user == null)
        {
            throw new InvalidOperationException($"User {command.TargetUserId} not found");
        }

        // Cascading delete will handle relationships due to foreign key constraints
        _dbContext.Users.Remove(user);
        if (user.Entity != null)
        {
            _dbContext.Entities.Remove(user.Entity);
        }
    }

    private async Task HandleDeleteGroupCommand(Infrastructure.DeleteGroupCommand command)
    {
        var group = await _dbContext.Groups.Include(g => g.Entity).FirstOrDefaultAsync(g => g.Id == command.GroupId);
        if (group == null)
        {
            throw new InvalidOperationException($"Group {command.GroupId} not found");
        }

        // Cascading delete will handle relationships due to foreign key constraints
        _dbContext.Groups.Remove(group);
        if (group.Entity != null)
        {
            _dbContext.Entities.Remove(group.Entity);
        }
    }

    private async Task HandleDeleteRoleCommand(Infrastructure.DeleteRoleCommand command)
    {
        var role = await _dbContext.Roles.Include(r => r.Entity).FirstOrDefaultAsync(r => r.Id == command.RoleId);
        if (role == null)
        {
            throw new InvalidOperationException($"Role {command.RoleId} not found");
        }

        // Cascading delete will handle relationships due to foreign key constraints
        _dbContext.Roles.Remove(role);
        if (role.Entity != null)
        {
            _dbContext.Entities.Remove(role.Entity);
        }
    }
}