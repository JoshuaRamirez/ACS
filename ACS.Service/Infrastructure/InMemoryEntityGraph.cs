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
            await BuildEntityRelationships();

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
        // Load permissions from UriAccess table with proper joins
        var uriAccesses = await _dbContext.UriAccesses
            .Include(ua => ua.Resource)
            .Include(ua => ua.VerbType)
            .Include(ua => ua.PermissionScheme)
                .ThenInclude(ps => ps.SchemeType)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        Permissions.Clear();
        foreach (var uriAccess in uriAccesses)
        {
            var permission = new Permission
            {
                Id = uriAccess.Id,
                Uri = uriAccess.Resource.Uri,
                HttpVerb = Enum.Parse<HttpVerb>(uriAccess.VerbType.VerbName),
                Grant = uriAccess.Grant,
                Deny = uriAccess.Deny,
                Scheme = Enum.Parse<Scheme>(uriAccess.PermissionScheme.SchemeType.SchemeName)
            };
            Permissions[permission.Id] = permission;
        }
        
        _logger.LogDebug("Loaded {PermissionCount} permissions from database", Permissions.Count);
    }

    private async Task BuildEntityRelationships()
    {
        _logger.LogInformation("Building entity relationships from database");

        // Load user-group relationships
        await BuildUserGroupRelationships();
        
        // Load user-role relationships  
        await BuildUserRoleRelationships();
        
        // Load group-role relationships
        await BuildGroupRoleRelationships();
        
        // Load group-group hierarchical relationships
        await BuildGroupHierarchyRelationships();
        
        // Load entity permissions
        await BuildEntityPermissionRelationships();
        
        _logger.LogInformation("Entity relationships built successfully");
    }

    private async Task BuildUserGroupRelationships()
    {
        // Load from User table where GroupId is not null
        var userGroupMappings = await _dbContext.Users
            .Where(u => u.GroupId > 0)
            .Select(u => new { UserId = u.Id, GroupId = u.GroupId })
            .AsNoTracking()
            .ToListAsync();

        foreach (var mapping in userGroupMappings)
        {
            if (Users.TryGetValue(mapping.UserId, out var user) && 
                Groups.TryGetValue(mapping.GroupId, out var group))
            {
                // Build bidirectional relationship using domain object collections
                user.Parents.Add(group);
                group.Children.Add(user);
            }
        }
        
        _logger.LogDebug("Built {Count} user-group relationships", userGroupMappings.Count);
    }

    private async Task BuildUserRoleRelationships()
    {
        // Load from User table where RoleId is not null
        var userRoleMappings = await _dbContext.Users
            .Where(u => u.RoleId > 0)
            .Select(u => new { UserId = u.Id, RoleId = u.RoleId })
            .AsNoTracking()
            .ToListAsync();

        foreach (var mapping in userRoleMappings)
        {
            if (Users.TryGetValue(mapping.UserId, out var user) && 
                Roles.TryGetValue(mapping.RoleId, out var role))
            {
                user.Parents.Add(role);
                role.Children.Add(user);
            }
        }
        
        _logger.LogDebug("Built {Count} user-role relationships", userRoleMappings.Count);
    }

    private async Task BuildGroupRoleRelationships()
    {
        // Load from Role table where GroupId is not null
        var groupRoleMappings = await _dbContext.Roles
            .Where(r => r.GroupId > 0)
            .Select(r => new { RoleId = r.Id, GroupId = r.GroupId })
            .AsNoTracking()
            .ToListAsync();

        foreach (var mapping in groupRoleMappings)
        {
            if (Groups.TryGetValue(mapping.GroupId, out var group) && 
                Roles.TryGetValue(mapping.RoleId, out var role))
            {
                group.Children.Add(role);
                role.Parents.Add(group);
            }
        }
        
        _logger.LogDebug("Built {Count} group-role relationships", groupRoleMappings.Count);
    }

    private async Task BuildGroupHierarchyRelationships()
    {
        // Load group hierarchy from ParentGroup relationships
        var groupHierarchyMappings = await _dbContext.Groups
            .Where(g => g.ParentGroupId > 0)
            .Select(g => new { ChildGroupId = g.Id, ParentGroupId = g.ParentGroupId })
            .AsNoTracking()
            .ToListAsync();

        foreach (var mapping in groupHierarchyMappings)
        {
            if (Groups.TryGetValue(mapping.ChildGroupId, out var childGroup) && 
                Groups.TryGetValue(mapping.ParentGroupId, out var parentGroup))
            {
                childGroup.Parents.Add(parentGroup);
                parentGroup.Children.Add(childGroup);
            }
        }
        
        _logger.LogDebug("Built {Count} group hierarchy relationships", groupHierarchyMappings.Count);
    }

    private async Task BuildEntityPermissionRelationships()
    {
        // Load URI access with permission schemes to build entity permissions
        var uriAccesses = await _dbContext.UriAccesses
            .Include(ua => ua.Resource)
            .Include(ua => ua.VerbType)
            .Include(ua => ua.PermissionScheme)
                .ThenInclude(ps => ps.SchemeType)
            .Include(ua => ua.PermissionScheme)
                .ThenInclude(ps => ps.Entity)
            .AsNoTracking()
            .ToListAsync();

        foreach (var uriAccess in uriAccesses)
        {
            var permissionScheme = uriAccess.PermissionScheme;
            if (!permissionScheme.EntityId.HasValue) continue;

            // Find the domain entity (could be User, Group, or Role)
            Entity? domainEntity = null;
            if (Users.TryGetValue(permissionScheme.EntityId.Value, out var user))
                domainEntity = user;
            else if (Groups.TryGetValue(permissionScheme.EntityId.Value, out var group))
                domainEntity = group;
            else if (Roles.TryGetValue(permissionScheme.EntityId.Value, out var role))
                domainEntity = role;

            if (domainEntity == null) continue;

            // Add permission to the entity
            var permission = new Permission
            {
                Id = uriAccess.Id,
                Uri = uriAccess.Resource.Uri,
                HttpVerb = Enum.Parse<HttpVerb>(uriAccess.VerbType.VerbName),
                Grant = uriAccess.Grant,
                Deny = uriAccess.Deny,
                Scheme = Enum.Parse<Scheme>(permissionScheme.SchemeType.SchemeName)
            };

            domainEntity.Permissions.Add(permission);
        }
        
        _logger.LogDebug("Built entity permission relationships from {Count} URI accesses", uriAccesses.Count);
    }

    public void HydrateNormalizerReferences()
    {
        _logger.LogInformation("Hydrating normalizer references to domain objects");

        // Convert dictionaries to lists for normalizer compatibility
        var usersList = Users.Values.ToList();
        var groupsList = Groups.Values.ToList();
        var rolesList = Roles.Values.ToList();

        // Hydrate all normalizers with references to domain objects
        
        // User-Group normalizers
        HydrateUserGroupNormalizers(usersList, groupsList);
        
        // User-Role normalizers
        HydrateUserRoleNormalizers(usersList, rolesList);

        // Group-Role normalizers
        HydrateGroupRoleNormalizers(groupsList, rolesList);

        // Group-Group normalizers
        HydrateGroupGroupNormalizers(groupsList);

        // Permission normalizers - these need additional collections
        HydratePermissionNormalizers();

        // Validate normalizer hydration
        var hydrationResults = ValidateNormalizerHydration();
        
        _logger.LogInformation("Normalizer references hydrated successfully. " +
            "Users: {UserCount}, Groups: {GroupCount}, Roles: {RoleCount}. " +
            "Validation: {SuccessCount} successful, {FailureCount} failed", 
            usersList.Count, groupsList.Count, rolesList.Count,
            hydrationResults.SuccessCount, hydrationResults.FailureCount);
    }

    private void HydrateUserGroupNormalizers(List<User> users, List<Group> groups)
    {
        // Use reflection to set static properties on normalizers
        SetNormalizerReferences("AddUserToGroupNormalizer", "Users", users);
        SetNormalizerReferences("AddUserToGroupNormalizer", "Groups", groups);
        
        SetNormalizerReferences("RemoveUserFromGroupNormalizer", "Users", users);
        SetNormalizerReferences("RemoveUserFromGroupNormalizer", "Groups", groups);
    }

    private void HydrateUserRoleNormalizers(List<User> users, List<Role> roles)
    {
        SetNormalizerReferences("AssignUserToRoleNormalizer", "Users", users);
        SetNormalizerReferences("AssignUserToRoleNormalizer", "Roles", roles);
        
        SetNormalizerReferences("UnAssignUserFromRoleNormalizer", "Users", users);
        SetNormalizerReferences("UnAssignUserFromRoleNormalizer", "Roles", roles);
    }

    private void HydrateGroupRoleNormalizers(List<Group> groups, List<Role> roles)
    {
        SetNormalizerReferences("AddRoleToGroupNormalizer", "Roles", roles);
        SetNormalizerReferences("AddRoleToGroupNormalizer", "Groups", groups);
        
        SetNormalizerReferences("RemoveRoleFromGroupNormalizer", "Roles", roles);
        SetNormalizerReferences("RemoveRoleFromGroupNormalizer", "Groups", groups);
    }

    private void HydrateGroupGroupNormalizers(List<Group> groups)
    {
        SetNormalizerReferences("AddGroupToGroupNormalizer", "Groups", groups);
        SetNormalizerReferences("RemoveGroupFromGroupNormalizer", "Groups", groups);
    }

    private void HydratePermissionNormalizers()
    {
        try
        {
            // Load additional collections needed by permission normalizers
            var entities = Users.Values.Cast<Entity>()
                .Union(Groups.Values.Cast<Entity>())
                .Union(Roles.Values.Cast<Entity>())
                .ToList();

            // Load database collections for permission normalizers
            var permissionSchemes = _dbContext.EntityPermissions.ToList();
            var entityList = _dbContext.Entities.ToList();
            var resources = _dbContext.Resources.ToList();
            var uriAccessList = _dbContext.UriAccesses.ToList();
            var schemeTypes = _dbContext.Set<ACS.Service.Data.Models.SchemeType>().ToList();
            var verbTypes = _dbContext.VerbTypes.ToList();

            // Hydrate AddPermissionToEntity normalizer
            SetNormalizerReferences("AddPermissionToEntity", "PermissionSchemes", permissionSchemes);
            SetNormalizerReferences("AddPermissionToEntity", "Entities", entityList);
            SetNormalizerReferences("AddPermissionToEntity", "Resources", resources);
            SetNormalizerReferences("AddPermissionToEntity", "UriAccessList", uriAccessList);
            SetNormalizerReferences("AddPermissionToEntity", "SchemeTypes", schemeTypes);

            // Hydrate RemovePermissionFromEntity normalizer
            SetNormalizerReferences("RemovePermissionFromEntity", "PermissionSchemes", permissionSchemes);
            SetNormalizerReferences("RemovePermissionFromEntity", "Entities", entityList);
            SetNormalizerReferences("RemovePermissionFromEntity", "Resources", resources);
            SetNormalizerReferences("RemovePermissionFromEntity", "UriAccessList", uriAccessList);
            SetNormalizerReferences("RemovePermissionFromEntity", "SchemeTypes", schemeTypes);

            // Hydrate CreateUriAccessNormalizer
            SetNormalizerReferences("CreateUriAccessNormalizer", "VerbTypes", verbTypes);

            // Hydrate CreatePermissionSchemeNormalizer
            SetNormalizerReferences("CreatePermissionSchemeNormalizer", "PermissionSchemes", permissionSchemes);

            _logger.LogDebug("Permission normalizer hydration completed with {PermissionSchemeCount} permission schemes, {EntityCount} entities, {ResourceCount} resources", 
                permissionSchemes.Count, entityList.Count, resources.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hydrate permission normalizers");
        }
    }

    private void SetNormalizerReferences(string normalizerName, string propertyName, object value)
    {
        try
        {
            var normalizerType = Type.GetType($"ACS.Service.Delegates.Normalizers.{normalizerName}");
            if (normalizerType == null)
            {
                _logger.LogWarning("Normalizer type {NormalizerName} not found", normalizerName);
                return;
            }

            var property = normalizerType.GetProperty(propertyName, 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            
            if (property == null)
            {
                _logger.LogWarning("Property {PropertyName} not found on normalizer {NormalizerName}", 
                    propertyName, normalizerName);
                return;
            }

            if (!property.CanWrite)
            {
                _logger.LogWarning("Property {PropertyName} on normalizer {NormalizerName} is not writable", 
                    propertyName, normalizerName);
                return;
            }

            // Validate property type compatibility
            if (value != null && !property.PropertyType.IsAssignableFrom(value.GetType()))
            {
                _logger.LogWarning("Type mismatch: Cannot assign {ValueType} to property {PropertyName} of type {PropertyType} on normalizer {NormalizerName}",
                    value.GetType().Name, propertyName, property.PropertyType.Name, normalizerName);
                return;
            }

            property.SetValue(null, value);
            
            var count = value is System.Collections.ICollection collection ? collection.Count : 1;
            _logger.LogDebug("Successfully set {NormalizerName}.{PropertyName} with {Count} items", 
                normalizerName, propertyName, count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set normalizer reference {NormalizerName}.{PropertyName}", 
                normalizerName, propertyName);
        }
    }

    private (int SuccessCount, int FailureCount) ValidateNormalizerHydration()
    {
        var normalizerValidations = new[]
        {
            ("AddUserToGroupNormalizer", new[] { "Users", "Groups" }),
            ("RemoveUserFromGroupNormalizer", new[] { "Users", "Groups" }),
            ("AssignUserToRoleNormalizer", new[] { "Users", "Roles" }),
            ("UnAssignUserFromRoleNormalizer", new[] { "Users", "Roles" }),
            ("AddRoleToGroupNormalizer", new[] { "Roles", "Groups" }),
            ("RemoveRoleFromGroupNormalizer", new[] { "Roles", "Groups" }),
            ("AddGroupToGroupNormalizer", new[] { "Groups" }),
            ("RemoveGroupFromGroupNormalizer", new[] { "Groups" }),
            ("AddPermissionToEntity", new[] { "PermissionSchemes", "Entities", "Resources", "UriAccessList", "SchemeTypes" }),
            ("RemovePermissionFromEntity", new[] { "PermissionSchemes", "Entities", "Resources", "UriAccessList", "SchemeTypes" }),
            ("CreateUriAccessNormalizer", new[] { "VerbTypes" }),
            ("CreatePermissionSchemeNormalizer", new[] { "PermissionSchemes" })
        };

        int successCount = 0;
        int failureCount = 0;

        foreach (var (normalizerName, requiredProperties) in normalizerValidations)
        {
            try
            {
                var normalizerType = Type.GetType($"ACS.Service.Delegates.Normalizers.{normalizerName}");
                if (normalizerType == null)
                {
                    _logger.LogWarning("Validation failed: Normalizer type {NormalizerName} not found", normalizerName);
                    failureCount++;
                    continue;
                }

                bool allPropertiesHydrated = true;
                foreach (var propertyName in requiredProperties)
                {
                    var property = normalizerType.GetProperty(propertyName, 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    
                    if (property == null)
                    {
                        _logger.LogWarning("Validation failed: Property {PropertyName} not found on {NormalizerName}", 
                            propertyName, normalizerName);
                        allPropertiesHydrated = false;
                        continue;
                    }

                    var value = property.GetValue(null);
                    if (value == null)
                    {
                        _logger.LogWarning("Validation failed: Property {PropertyName} on {NormalizerName} is null", 
                            propertyName, normalizerName);
                        allPropertiesHydrated = false;
                    }
                    else if (value is System.Collections.ICollection collection && collection.Count == 0)
                    {
                        _logger.LogWarning("Validation warning: Property {PropertyName} on {NormalizerName} is empty collection", 
                            propertyName, normalizerName);
                    }
                }

                if (allPropertiesHydrated)
                {
                    successCount++;
                    _logger.LogDebug("Validation success: {NormalizerName} is properly hydrated", normalizerName);
                }
                else
                {
                    failureCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Validation error for normalizer {NormalizerName}", normalizerName);
                failureCount++;
            }
        }

        return (successCount, failureCount);
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