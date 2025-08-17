using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using ACS.Service.Data;
using ACS.Service.Domain;

namespace ACS.Service.Infrastructure;

public class InMemoryEntityGraph
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<InMemoryEntityGraph> _logger;

    // Master collections of all domain objects
    public Dictionary<int, User> Users { get; private set; } = new();
    public Dictionary<int, Group> Groups { get; private set; } = new();
    public Dictionary<int, Role> Roles { get; private set; } = new();
    public Dictionary<int, Permission> Permissions { get; private set; } = new();

    // Performance metrics
    public DateTime LastLoadTime { get; private set; }
    public TimeSpan LoadDuration { get; private set; }
    public int TotalEntityCount => Users.Count + Groups.Count + Roles.Count;

    public InMemoryEntityGraph(ApplicationDbContext dbContext, ILogger<InMemoryEntityGraph> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task LoadFromDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting entity graph load from database");

        try
        {
            // Load all entities with their relationships
            await LoadUsersAsync(cancellationToken);
            await LoadGroupsAsync(cancellationToken);
            await LoadRolesAsync(cancellationToken);
            await LoadPermissionsAsync(cancellationToken);
            
            // Build relationships between loaded entities
            BuildEntityRelationships();

            LastLoadTime = DateTime.UtcNow;
            LoadDuration = LastLoadTime - startTime;
            
            _logger.LogInformation("Entity graph loaded successfully. " +
                "Users: {UserCount}, Groups: {GroupCount}, Roles: {RoleCount}, " +
                "Load time: {LoadTime}ms", 
                Users.Count, Groups.Count, Roles.Count, LoadDuration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load entity graph from database");
            throw;
        }
    }

    private async Task LoadUsersAsync(CancellationToken cancellationToken)
    {
        var users = await _dbContext.Users
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        Users.Clear();
        foreach (var user in users)
        {
            var domainUser = new User
            {
                Id = user.Id,
                Name = user.Name
            };
            Users[user.Id] = domainUser;
        }
    }

    private async Task LoadGroupsAsync(CancellationToken cancellationToken)
    {
        var groups = await _dbContext.Groups
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        Groups.Clear();
        foreach (var group in groups)
        {
            var domainGroup = new Group
            {
                Id = group.Id,
                Name = group.Name
            };
            Groups[group.Id] = domainGroup;
        }
    }

    private async Task LoadRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await _dbContext.Roles
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        Roles.Clear();
        foreach (var role in roles)
        {
            var domainRole = new Role
            {
                Id = role.Id,
                Name = role.Name
            };
            Roles[role.Id] = domainRole;
        }
    }

    private async Task LoadPermissionsAsync(CancellationToken cancellationToken)
    {
        // For now, create a simple permission structure
        // This will be enhanced in Phase 2 with proper URI access loading
        Permissions.Clear();
        
        _logger.LogDebug("Permission loading implementation pending for Phase 2");
    }

    private void BuildEntityRelationships()
    {
        // This method will be implemented in Phase 2
        // It builds the Children/Parents relationships in domain objects
        _logger.LogDebug("Building entity relationships - implementation pending for Phase 2");
    }

    public void HydrateNormalizerReferences()
    {
        _logger.LogInformation("Hydrating normalizer references to domain objects");

        // Point all normalizers to the same domain object collections
        // This will be implemented in Phase 2 when we integrate with normalizers
        
        _logger.LogInformation("Normalizer references hydration pending for Phase 2");
    }

    public User GetUser(int userId)
    {
        return Users.TryGetValue(userId, out var user) 
            ? user 
            : throw new InvalidOperationException($"User {userId} not found");
    }

    public Group GetGroup(int groupId)
    {
        return Groups.TryGetValue(groupId, out var group) 
            ? group 
            : throw new InvalidOperationException($"Group {groupId} not found");
    }

    public Role GetRole(int roleId)
    {
        return Roles.TryGetValue(roleId, out var role) 
            ? role 
            : throw new InvalidOperationException($"Role {roleId} not found");
    }
}