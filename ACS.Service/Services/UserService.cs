using ACS.Service.Data;
using ACS.Service.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACS.Service.Services;

public class UserService : IUserService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICommandProcessingService _commandProcessingService;
    private readonly ILogger<UserService> _logger;

    public UserService(
        ApplicationDbContext dbContext, 
        ICommandProcessingService commandProcessingService,
        ILogger<UserService> logger)
    {
        _dbContext = dbContext;
        _commandProcessingService = commandProcessingService;
        _logger = logger;
    }

    public async Task<IEnumerable<Domain.User>> GetAllAsync()
    {
        // Convert data model users to domain model users
        var dataUsers = await _dbContext.Users
            .Include(u => u.Entity)
            .Include(u => u.UserGroups)
            .ThenInclude(ug => ug.Group)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ToListAsync();

        return dataUsers.Select(ConvertToDomainUser);
    }

    public async Task<Domain.User?> GetByIdAsync(int id)
    {
        var dataUser = await _dbContext.Users
            .Include(u => u.Entity)
            .Include(u => u.UserGroups)
            .ThenInclude(ug => ug.Group)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id);

        return dataUser != null ? ConvertToDomainUser(dataUser) : null;
    }

    public async Task<Domain.User> AddAsync(Domain.User user, string createdBy)
    {
        // Create entity first
        var entity = new Data.Models.Entity
        {
            EntityType = "User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Entities.Add(entity);
        await _dbContext.SaveChangesAsync(); // Save to get the ID

        // Create data model user
        var dataUser = new Data.Models.User
        {
            Name = user.Name,
            EntityId = entity.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(dataUser);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Created user {UserId} with name {UserName} by {CreatedBy}", dataUser.Id, user.Name, createdBy);

        // Return domain user
        user.Id = dataUser.Id;
        return user;
    }

    public async Task<Domain.User> UpdateAsync(Domain.User user)
    {
        try
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user), "User cannot be null");
            }
            
            if (string.IsNullOrWhiteSpace(user.Name))
            {
                throw new ArgumentException("User name cannot be null or empty", nameof(user));
            }

            var dataUser = await _dbContext.Users.FindAsync(user.Id);
            if (dataUser == null)
            {
                _logger.LogWarning("Attempted to update non-existent user {UserId}", user.Id);
                throw new InvalidOperationException($"User {user.Id} not found");
            }

            dataUser.Name = user.Name;
            dataUser.UpdatedAt = DateTime.UtcNow;
            _dbContext.Users.Update(dataUser);
            
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Successfully updated user {UserId} with name {UserName}", user.Id, user.Name);
            return user;
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument provided for user update: {UserId}", user?.Id);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during user update: {UserId}", user?.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating user {UserId}", user?.Id);
            throw new InvalidOperationException($"Failed to update user {user?.Id}: {ex.Message}", ex);
        }
    }

    public async Task DeleteAsync(int id)
    {
        try
        {
            if (id <= 0)
            {
                throw new ArgumentException("User ID must be a positive integer", nameof(id));
            }

            var dataUser = await _dbContext.Users
                .Include(u => u.Entity)
                .FirstOrDefaultAsync(u => u.Id == id);
            
            if (dataUser == null)
            {
                _logger.LogWarning("Attempted to delete non-existent user {UserId}", id);
                throw new InvalidOperationException($"User {id} not found");
            }

            // Check for dependencies before deletion
            var hasActiveRelationships = await HasActiveDependenciesAsync(id);
            if (hasActiveRelationships)
            {
                _logger.LogWarning("Cannot delete user {UserId} due to active relationships", id);
                throw new InvalidOperationException($"Cannot delete user {id} with active relationships. Remove user from groups and roles first.");
            }

            // Cascading delete will handle relationships due to foreign key constraints
            _dbContext.Users.Remove(dataUser);
            if (dataUser.Entity != null)
            {
                _dbContext.Entities.Remove(dataUser.Entity);
            }
            
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Successfully deleted user {UserId}", id);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument provided for user deletion: {UserId}", id);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during user deletion: {UserId}", id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting user {UserId}", id);
            throw new InvalidOperationException($"Failed to delete user {id}: {ex.Message}", ex);
        }
    }
    
    private async Task<bool> HasActiveDependenciesAsync(int userId)
    {
        try
        {
            // Check for user-group relationships
            var hasGroups = await _dbContext.UserGroups.AnyAsync(ug => ug.UserId == userId);
            
            // Check for user-role relationships  
            var hasRoles = await _dbContext.UserRoles.AnyAsync(ur => ur.UserId == userId);
            
            return hasGroups || hasRoles;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking dependencies for user {UserId}", userId);
            // Return true to be safe and prevent deletion if we can't verify
            return true;
        }
    }

    // Legacy methods for backward compatibility - these should be deprecated
    public IEnumerable<Domain.User> GetAll()
    {
        return GetAllAsync().Result;
    }

    public Domain.User? GetById(int id)
    {
        return GetByIdAsync(id).Result;
    }

    public Domain.User Add(Domain.User user)
    {
        return AddAsync(user, "system").Result;
    }

    private Domain.User ConvertToDomainUser(Data.Models.User dataUser)
    {
        var domainUser = new Domain.User
        {
            Id = dataUser.Id,
            Name = dataUser.Name
        };

        // Convert relationships - this is simplified
        // In a complete implementation, you'd need to properly map all relationships
        foreach (var userGroup in dataUser.UserGroups)
        {
            var domainGroup = new Domain.Group
            {
                Id = userGroup.Group.Id,
                Name = userGroup.Group.Name
            };
            domainUser.Parents.Add(domainGroup);
        }

        foreach (var userRole in dataUser.UserRoles)
        {
            var domainRole = new Domain.Role
            {
                Id = userRole.Role.Id,
                Name = userRole.Role.Name
            };
            domainUser.Parents.Add(domainRole);
        }

        return domainUser;
    }
}
