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
    
    // Enhanced performance metrics
    public long MemoryUsageBytes { get; private set; }
    public int RelationshipCount { get; private set; }
    public Dictionary<string, TimeSpan> LoadingPhaseTimings { get; private set; } = new();
    
    // Relationship indexes for fast lookups
    private readonly Dictionary<int, List<int>> _userGroupIndex = new();
    private readonly Dictionary<int, List<int>> _userRoleIndex = new();
    private readonly Dictionary<int, List<int>> _groupRoleIndex = new();
    private readonly Dictionary<int, List<int>> _groupHierarchyIndex = new();
    private readonly Dictionary<int, List<int>> _entityPermissionIndex = new();

    public InMemoryEntityGraph(ApplicationDbContext dbContext, ILogger<InMemoryEntityGraph> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task LoadFromDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var overallStartTime = DateTime.UtcNow;
        _logger.LogInformation("Starting optimized entity graph load from database");
        
        // Clear previous data
        ClearAll();

        try
        {
            // Phase 1: Bulk load all entities with minimal queries
            await LoadEntitiesBulkAsync(cancellationToken);
            
            // Phase 2: Build relationships with optimized queries
            await BuildOptimizedRelationships(cancellationToken);
            
            // Phase 3: Build indexes for fast lookups
            await BuildRelationshipIndexes();

            // Phase 4: Calculate memory usage and metrics
            CalculateMemoryUsage();

            LastLoadTime = DateTime.UtcNow;
            LoadDuration = LastLoadTime - overallStartTime;
            
            _logger.LogInformation("Optimized entity graph loaded successfully. " +
                "Users: {UserCount}, Groups: {GroupCount}, Roles: {RoleCount}, Permissions: {PermissionCount}, " +
                "Relationships: {RelationshipCount}, Memory: {MemoryMB:F2}MB, Load time: {LoadTime}ms", 
                Users.Count, Groups.Count, Roles.Count, Permissions.Count, 
                RelationshipCount, MemoryUsageBytes / 1024.0 / 1024.0, LoadDuration.TotalMilliseconds);
                
            LogLoadingPhaseTimings();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load entity graph from database");
            throw;
        }
    }

    private async Task LoadEntitiesBulkAsync(CancellationToken cancellationToken)
    {
        var phaseStart = DateTime.UtcNow;
        _logger.LogDebug("Starting bulk entity loading phase");

        // Load all entities in parallel with optimized queries
        var tasks = new[]
        {
            LoadUsersOptimizedAsync(cancellationToken),
            LoadGroupsOptimizedAsync(cancellationToken),
            LoadRolesOptimizedAsync(cancellationToken),
            LoadPermissionsOptimizedAsync(cancellationToken)
        };

        await Task.WhenAll(tasks);
        
        LoadingPhaseTimings["BulkEntityLoading"] = DateTime.UtcNow - phaseStart;
        _logger.LogDebug("Bulk entity loading completed in {Duration}ms", LoadingPhaseTimings["BulkEntityLoading"].TotalMilliseconds);
    }

    private async Task LoadUsersOptimizedAsync(CancellationToken cancellationToken)
    {
        var users = await _dbContext.Users
            .AsNoTracking()
            .Select(u => new { u.Id, u.Name })
            .ToListAsync(cancellationToken);

        Users.Clear();
        Users.EnsureCapacity(users.Count);
        
        foreach (var user in users)
        {
            var domainUser = new User
            {
                Id = user.Id,
                Name = user.Name
            };
            Users[user.Id] = domainUser;
        }
        
        _logger.LogDebug("Loaded {UserCount} users", users.Count);
    }

    private async Task LoadUsersAsync(CancellationToken cancellationToken)
    {
        await LoadUsersOptimizedAsync(cancellationToken);
    }

    private async Task LoadGroupsOptimizedAsync(CancellationToken cancellationToken)
    {
        var groups = await _dbContext.Groups
            .AsNoTracking()
            .Select(g => new { g.Id, g.Name })
            .ToListAsync(cancellationToken);

        Groups.Clear();
        Groups.EnsureCapacity(groups.Count);
        
        foreach (var group in groups)
        {
            var domainGroup = new Group
            {
                Id = group.Id,
                Name = group.Name
            };
            Groups[group.Id] = domainGroup;
        }
        
        _logger.LogDebug("Loaded {GroupCount} groups", groups.Count);
    }

    private async Task LoadGroupsAsync(CancellationToken cancellationToken)
    {
        await LoadGroupsOptimizedAsync(cancellationToken);
    }

    private async Task LoadRolesOptimizedAsync(CancellationToken cancellationToken)
    {
        var roles = await _dbContext.Roles
            .AsNoTracking()
            .Select(r => new { r.Id, r.Name })
            .ToListAsync(cancellationToken);

        Roles.Clear();
        Roles.EnsureCapacity(roles.Count);
        
        foreach (var role in roles)
        {
            var domainRole = new Role
            {
                Id = role.Id,
                Name = role.Name
            };
            Roles[role.Id] = domainRole;
        }
        
        _logger.LogDebug("Loaded {RoleCount} roles", roles.Count);
    }

    private async Task LoadRolesAsync(CancellationToken cancellationToken)
    {
        await LoadRolesOptimizedAsync(cancellationToken);
    }

    private async Task LoadPermissionsOptimizedAsync(CancellationToken cancellationToken)
    {
        // Load permissions with optimized projection to reduce memory
        var uriAccesses = await _dbContext.UriAccesses
            .AsNoTracking()
            .Select(ua => new
            {
                ua.Id,
                ua.Grant,
                ua.Deny,
                ResourceUri = ua.Resource.Uri,
                VerbName = ua.VerbType.VerbName,
                SchemeName = ua.PermissionScheme.SchemeType.SchemeName
            })
            .ToListAsync(cancellationToken);

        Permissions.Clear();
        Permissions.EnsureCapacity(uriAccesses.Count);
        
        foreach (var uriAccess in uriAccesses)
        {
            var permission = new Permission
            {
                Id = uriAccess.Id,
                Uri = uriAccess.ResourceUri,
                HttpVerb = Enum.Parse<HttpVerb>(uriAccess.VerbName),
                Grant = uriAccess.Grant,
                Deny = uriAccess.Deny,
                Scheme = Enum.Parse<Scheme>(uriAccess.SchemeName)
            };
            Permissions[permission.Id] = permission;
        }
        
        _logger.LogDebug("Loaded {PermissionCount} permissions from database", Permissions.Count);
    }

    private async Task LoadPermissionsAsync(CancellationToken cancellationToken)
    {
        await LoadPermissionsOptimizedAsync(cancellationToken);
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

    #region Optimized Relationship Building

    private async Task BuildOptimizedRelationships(CancellationToken cancellationToken)
    {
        var phaseStart = DateTime.UtcNow;
        _logger.LogDebug("Starting optimized relationship building phase");

        // Build all relationships with optimized queries
        var tasks = new[]
        {
            BuildUserGroupRelationshipsOptimized(cancellationToken),
            BuildUserRoleRelationshipsOptimized(cancellationToken),
            BuildGroupRoleRelationshipsOptimized(cancellationToken),
            BuildGroupHierarchyRelationshipsOptimized(cancellationToken),
            BuildEntityPermissionRelationshipsOptimized(cancellationToken)
        };

        await Task.WhenAll(tasks);
        
        LoadingPhaseTimings["RelationshipBuilding"] = DateTime.UtcNow - phaseStart;
        _logger.LogDebug("Optimized relationship building completed in {Duration}ms", LoadingPhaseTimings["RelationshipBuilding"].TotalMilliseconds);
    }

    private async Task BuildUserGroupRelationshipsOptimized(CancellationToken cancellationToken)
    {
        var mappings = await _dbContext.Users
            .Where(u => u.GroupId > 0)
            .AsNoTracking()
            .Select(u => new { u.Id, u.GroupId })
            .ToListAsync(cancellationToken);

        foreach (var mapping in mappings)
        {
            if (Users.TryGetValue(mapping.Id, out var user) && 
                Groups.TryGetValue(mapping.GroupId, out var group))
            {
                user.Parents.Add(group);
                group.Children.Add(user);
                
                // Build index
                if (!_userGroupIndex.TryGetValue(mapping.Id, out var userGroups))
                {
                    userGroups = new List<int>();
                    _userGroupIndex[mapping.Id] = userGroups;
                }
                userGroups.Add(mapping.GroupId);
            }
        }
        
        _logger.LogDebug("Built {Count} user-group relationships", mappings.Count);
    }

    private async Task BuildUserRoleRelationshipsOptimized(CancellationToken cancellationToken)
    {
        var mappings = await _dbContext.Users
            .Where(u => u.RoleId > 0)
            .AsNoTracking()
            .Select(u => new { u.Id, u.RoleId })
            .ToListAsync(cancellationToken);

        foreach (var mapping in mappings)
        {
            if (Users.TryGetValue(mapping.Id, out var user) && 
                Roles.TryGetValue(mapping.RoleId, out var role))
            {
                user.Parents.Add(role);
                role.Children.Add(user);
                
                // Build index
                if (!_userRoleIndex.TryGetValue(mapping.Id, out var userRoles))
                {
                    userRoles = new List<int>();
                    _userRoleIndex[mapping.Id] = userRoles;
                }
                userRoles.Add(mapping.RoleId);
            }
        }
        
        _logger.LogDebug("Built {Count} user-role relationships", mappings.Count);
    }

    private async Task BuildGroupRoleRelationshipsOptimized(CancellationToken cancellationToken)
    {
        var mappings = await _dbContext.Roles
            .Where(r => r.GroupId > 0)
            .AsNoTracking()
            .Select(r => new { r.Id, r.GroupId })
            .ToListAsync(cancellationToken);

        foreach (var mapping in mappings)
        {
            if (Groups.TryGetValue(mapping.GroupId, out var group) && 
                Roles.TryGetValue(mapping.Id, out var role))
            {
                group.Children.Add(role);
                role.Parents.Add(group);
                
                // Build index
                if (!_groupRoleIndex.TryGetValue(mapping.GroupId, out var groupRoles))
                {
                    groupRoles = new List<int>();
                    _groupRoleIndex[mapping.GroupId] = groupRoles;
                }
                groupRoles.Add(mapping.Id);
            }
        }
        
        _logger.LogDebug("Built {Count} group-role relationships", mappings.Count);
    }

    private async Task BuildGroupHierarchyRelationshipsOptimized(CancellationToken cancellationToken)
    {
        var mappings = await _dbContext.Groups
            .Where(g => g.ParentGroupId > 0)
            .AsNoTracking()
            .Select(g => new { ChildId = g.Id, ParentId = g.ParentGroupId })
            .ToListAsync(cancellationToken);

        foreach (var mapping in mappings)
        {
            if (Groups.TryGetValue(mapping.ChildId, out var childGroup) && 
                Groups.TryGetValue(mapping.ParentId, out var parentGroup))
            {
                childGroup.Parents.Add(parentGroup);
                parentGroup.Children.Add(childGroup);
                
                // Build index
                if (!_groupHierarchyIndex.TryGetValue(mapping.ParentId, out var childGroups))
                {
                    childGroups = new List<int>();
                    _groupHierarchyIndex[mapping.ParentId] = childGroups;
                }
                childGroups.Add(mapping.ChildId);
            }
        }
        
        _logger.LogDebug("Built {Count} group hierarchy relationships", mappings.Count);
    }

    private async Task BuildEntityPermissionRelationshipsOptimized(CancellationToken cancellationToken)
    {
        var entityPermissions = await _dbContext.UriAccesses
            .Where(ua => ua.PermissionScheme.EntityId.HasValue)
            .AsNoTracking()
            .Select(ua => new
            {
                ua.Id,
                EntityId = ua.PermissionScheme.EntityId!.Value,
                ua.Grant,
                ua.Deny,
                ResourceUri = ua.Resource.Uri,
                VerbName = ua.VerbType.VerbName,
                SchemeName = ua.PermissionScheme.SchemeType.SchemeName
            })
            .ToListAsync(cancellationToken);

        foreach (var permData in entityPermissions)
        {
            // Find the domain entity
            Entity? domainEntity = null;
            if (Users.TryGetValue(permData.EntityId, out var user))
                domainEntity = user;
            else if (Groups.TryGetValue(permData.EntityId, out var group))
                domainEntity = group;
            else if (Roles.TryGetValue(permData.EntityId, out var role))
                domainEntity = role;

            if (domainEntity != null)
            {
                var permission = new Permission
                {
                    Id = permData.Id,
                    Uri = permData.ResourceUri,
                    HttpVerb = Enum.Parse<HttpVerb>(permData.VerbName),
                    Grant = permData.Grant,
                    Deny = permData.Deny,
                    Scheme = Enum.Parse<Scheme>(permData.SchemeName)
                };

                domainEntity.Permissions.Add(permission);
                
                // Build index
                if (!_entityPermissionIndex.TryGetValue(permData.EntityId, out var entityPerms))
                {
                    entityPerms = new List<int>();
                    _entityPermissionIndex[permData.EntityId] = entityPerms;
                }
                entityPerms.Add(permission.Id);
            }
        }
        
        _logger.LogDebug("Built entity permission relationships for {Count} permissions", entityPermissions.Count);
    }

    private Task BuildRelationshipIndexes()
    {
        var phaseStart = DateTime.UtcNow;
        _logger.LogDebug("Building relationship indexes");

        // Calculate total relationships
        RelationshipCount = _userGroupIndex.Values.Sum(v => v.Count) +
                           _userRoleIndex.Values.Sum(v => v.Count) +
                           _groupRoleIndex.Values.Sum(v => v.Count) +
                           _groupHierarchyIndex.Values.Sum(v => v.Count) +
                           _entityPermissionIndex.Values.Sum(v => v.Count);

        LoadingPhaseTimings["IndexBuilding"] = DateTime.UtcNow - phaseStart;
        _logger.LogDebug("Relationship indexes built in {Duration}ms", LoadingPhaseTimings["IndexBuilding"].TotalMilliseconds);
        
        return Task.CompletedTask;
    }

    #endregion

    #region Helper Methods

    private void ClearAll()
    {
        Users.Clear();
        Groups.Clear();
        Roles.Clear();
        Permissions.Clear();
        
        _userGroupIndex.Clear();
        _userRoleIndex.Clear();
        _groupRoleIndex.Clear();
        _groupHierarchyIndex.Clear();
        _entityPermissionIndex.Clear();
        
        LoadingPhaseTimings.Clear();
        RelationshipCount = 0;
        MemoryUsageBytes = 0;
    }

    private void CalculateMemoryUsage()
    {
        var phaseStart = DateTime.UtcNow;
        
        // Rough estimation of memory usage
        var baseObjectSize = 64; // Approximate object overhead
        var entitySize = baseObjectSize + 50; // Approximate domain entity size
        var relationshipSize = 8; // Reference size
        
        MemoryUsageBytes = (Users.Count * entitySize) +
                          (Groups.Count * entitySize) +
                          (Roles.Count * entitySize) +
                          (Permissions.Count * (entitySize + 100)) + // Permissions have more data
                          (RelationshipCount * relationshipSize);
        
        LoadingPhaseTimings["MemoryCalculation"] = DateTime.UtcNow - phaseStart;
    }

    private void LogLoadingPhaseTimings()
    {
        _logger.LogDebug("Loading phase timings:");
        foreach (var (phase, duration) in LoadingPhaseTimings)
        {
            _logger.LogDebug("  {Phase}: {Duration}ms", phase, duration.TotalMilliseconds);
        }
    }

    #endregion

    #region Fast Lookup Methods

    public List<int> GetUserGroups(int userId)
    {
        return _userGroupIndex.TryGetValue(userId, out var groups) ? groups : new List<int>();
    }

    public List<int> GetUserRoles(int userId)
    {
        return _userRoleIndex.TryGetValue(userId, out var roles) ? roles : new List<int>();
    }

    public List<int> GetGroupRoles(int groupId)
    {
        return _groupRoleIndex.TryGetValue(groupId, out var roles) ? roles : new List<int>();
    }

    public List<int> GetChildGroups(int groupId)
    {
        return _groupHierarchyIndex.TryGetValue(groupId, out var children) ? children : new List<int>();
    }

    public List<int> GetEntityPermissions(int entityId)
    {
        return _entityPermissionIndex.TryGetValue(entityId, out var permissions) ? permissions : new List<int>();
    }

    #endregion

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