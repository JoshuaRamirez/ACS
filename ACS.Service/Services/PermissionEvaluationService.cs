using ACS.Service.Data;
using ACS.Service.Data.Models;
using ACS.Service.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ACS.Service.Services;

public class PermissionEvaluationService : IPermissionEvaluationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<PermissionEvaluationService> _logger;
    private readonly IMemoryCache _cache;
    private readonly IAuditService _auditService;
    
    // In-memory stores for advanced features
    private readonly Dictionary<int, List<ConditionalPermission>> _conditionalPermissions = new();
    private readonly Dictionary<int, List<DelegatedPermission>> _delegatedPermissions = new();
    private readonly Dictionary<int, List<TemporaryPermission>> _temporaryPermissions = new();
    private readonly Dictionary<string, PermissionTemplate> _permissionTemplates = new();
    private readonly Dictionary<string, PermissionPolicy> _permissionPolicies = new();
    private ConflictResolutionStrategy _conflictResolutionStrategy = ConflictResolutionStrategy.DenyOverrides;
    
    // Cache statistics
    private readonly CacheStatistics _cacheStats = new() { LastReset = DateTime.UtcNow };

    public PermissionEvaluationService(
        ApplicationDbContext dbContext,
        ILogger<PermissionEvaluationService> logger,
        IMemoryCache cache,
        IAuditService auditService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _cache = cache;
        _auditService = auditService;
        InitializeDefaultTemplates();
        InitializeDefaultPolicies();
    }

    #region Core Permission Evaluation

    public async Task<bool> HasPermissionAsync(int entityId, string uri, string httpVerb)
    {
        if (Enum.TryParse<Domain.HttpVerb>(httpVerb, out var verb))
        {
            return await HasPermissionAsync(entityId, uri, verb);
        }
        return false;
    }

    public async Task<bool> HasPermissionAsync(int entityId, string uri, Domain.HttpVerb httpVerb)
    {
        // Check cache first
        var cacheKey = $"perm_{entityId}_{uri}_{httpVerb}";
        if (_cache.TryGetValue<bool>(cacheKey, out var cachedResult))
        {
            _cacheStats.HitCount++;
            return cachedResult;
        }
        
        _cacheStats.MissCount++;

        var result = await EvaluatePermissionAsync(entityId, uri, httpVerb);
        
        // Cache the result
        _cache.Set(cacheKey, result.IsAllowed, TimeSpan.FromMinutes(5));
        
        return result.IsAllowed;
    }

    public async Task<List<Domain.Permission>> GetEffectivePermissionsAsync(int entityId)
    {
        var permissions = new List<Domain.Permission>();
        
        // Get direct permissions
        permissions.AddRange(await GetDirectPermissionsAsync(entityId));
        
        // Get inherited permissions
        permissions.AddRange(await GetInheritedPermissionsAsync(entityId));
        
        // Get conditional permissions
        permissions.AddRange(await GetConditionalPermissionsAsync(entityId));
        
        // Get temporary permissions
        permissions.AddRange(await GetTemporaryPermissionsAsync(entityId));
        
        // Get delegated permissions
        permissions.AddRange(await GetDelegatedPermissionsAsync(entityId));
        
        // Resolve conflicts
        return await ResolvePermissionConflictsAsync(permissions);
    }

    public async Task<bool> CanUserAccessResourceAsync(int userId, string uri, Domain.HttpVerb httpVerb)
    {
        // Check if user exists
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null)
            return false;

        // Get user's entity ID
        var userEntity = await _dbContext.Entities
            .FirstOrDefaultAsync(e => e.EntityType == "User" && 
                                     e.Users.Any(u => u.Id == userId));
        
        if (userEntity == null)
            return false;

        return await HasPermissionAsync(userEntity.Id, uri, httpVerb);
    }

    public async Task<List<Domain.Permission>> GetUserPermissionsAsync(int userId)
    {
        var permissions = new List<Domain.Permission>();
        
        // Get direct user permissions
        var user = await _dbContext.Users
            .Include(u => u.Entity)
            .FirstOrDefaultAsync(u => u.Id == userId);
        
        if (user?.Entity != null)
        {
            permissions.AddRange(await GetEntityPermissionsAsync(user.EntityId, true));
        }
        
        // Get permissions from user's roles
        var userRoles = await _dbContext.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userId)
            .ToListAsync();
        
        foreach (var userRole in userRoles)
        {
            permissions.AddRange(await GetRolePermissionsAsync(userRole.RoleId));
        }
        
        // Get permissions from user's groups
        var userGroups = await _dbContext.UserGroups
            .Include(ug => ug.Group)
            .Where(ug => ug.UserId == userId)
            .ToListAsync();
        
        foreach (var userGroup in userGroups)
        {
            permissions.AddRange(await GetGroupPermissionsAsync(userGroup.GroupId));
        }
        
        return await ResolvePermissionConflictsAsync(permissions);
    }

    public async Task<List<Domain.Permission>> GetGroupPermissionsAsync(int groupId)
    {
        var permissions = new List<Domain.Permission>();
        
        // Get direct group permissions
        var group = await _dbContext.Groups
            .Include(g => g.Entity)
            .FirstOrDefaultAsync(g => g.Id == groupId);
        
        if (group?.Entity != null)
        {
            permissions.AddRange(await GetEntityPermissionsAsync(group.EntityId, false));
        }
        
        // Get permissions from group's roles
        var groupRoles = await _dbContext.GroupRoles
            .Include(gr => gr.Role)
            .Where(gr => gr.GroupId == groupId)
            .ToListAsync();
        
        foreach (var groupRole in groupRoles)
        {
            permissions.AddRange(await GetRolePermissionsAsync(groupRole.RoleId));
        }
        
        // Get permissions from parent groups
        var parentGroups = await _dbContext.GroupHierarchies
            .Where(gh => gh.ChildGroupId == groupId)
            .ToListAsync();
        
        foreach (var parentGroup in parentGroups)
        {
            permissions.AddRange(await GetGroupPermissionsAsync(parentGroup.ParentGroupId));
        }
        
        return permissions;
    }

    public async Task<List<Domain.Permission>> GetRolePermissionsAsync(int roleId)
    {
        var role = await _dbContext.Roles
            .Include(r => r.Entity)
            .FirstOrDefaultAsync(r => r.Id == roleId);
        
        if (role?.Entity != null)
        {
            return (await GetEntityPermissionsAsync(role.EntityId, false)).ToList();
        }
        
        return new List<Domain.Permission>();
    }

    #endregion

    #region Enhanced Permission Evaluation with Inheritance

    public async Task<IEnumerable<Domain.Permission>> GetEntityPermissionsAsync(int entityId, bool includeInherited = true)
    {
        var permissions = new List<Domain.Permission>();
        
        // Get direct permissions
        permissions.AddRange(await GetDirectPermissionsAsync(entityId));
        
        if (includeInherited)
        {
            permissions.AddRange(await GetInheritedPermissionsAsync(entityId));
        }
        
        return permissions;
    }

    public async Task<IEnumerable<Domain.Permission>> GetInheritedPermissionsAsync(int entityId)
    {
        var permissions = new List<Domain.Permission>();
        
        var entity = await _dbContext.Entities.FindAsync(entityId);
        if (entity == null)
            return permissions;

        switch (entity.EntityType)
        {
            case "User":
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.EntityId == entityId);
                if (user != null)
                {
                    // Get from roles
                    var userRoles = await _dbContext.UserRoles
                        .Where(ur => ur.UserId == user.Id)
                        .ToListAsync();
                    
                    foreach (var ur in userRoles)
                    {
                        permissions.AddRange(await GetRolePermissionsAsync(ur.RoleId));
                    }
                    
                    // Get from groups
                    var userGroups = await _dbContext.UserGroups
                        .Where(ug => ug.UserId == user.Id)
                        .ToListAsync();
                    
                    foreach (var ug in userGroups)
                    {
                        permissions.AddRange(await GetGroupPermissionsAsync(ug.GroupId));
                    }
                }
                break;
                
            case "Group":
                var group = await _dbContext.Groups.FirstOrDefaultAsync(g => g.EntityId == entityId);
                if (group != null)
                {
                    // Get from parent groups
                    var parentGroups = await _dbContext.GroupHierarchies
                        .Where(gh => gh.ChildGroupId == group.Id)
                        .ToListAsync();
                    
                    foreach (var pg in parentGroups)
                    {
                        permissions.AddRange(await GetGroupPermissionsAsync(pg.ParentGroupId));
                    }
                }
                break;
        }
        
        return permissions;
    }

    public async Task<IEnumerable<Domain.Permission>> GetDirectPermissionsAsync(int entityId)
    {
        var permissions = new List<Domain.Permission>();
        
        // Get permissions from database
        var schemeType = await _dbContext.SchemeTypes
            .FirstOrDefaultAsync(st => st.SchemeName == "ApiUriAuthorization");
        
        if (schemeType != null)
        {
            var permissionScheme = await _dbContext.EntityPermissions
                .FirstOrDefaultAsync(ps => ps.EntityId == entityId && ps.SchemeTypeId == schemeType.Id);
            
            if (permissionScheme != null)
            {
                var uriAccesses = await _dbContext.UriAccesses
                    .Include(ua => ua.Resource)
                    .Include(ua => ua.VerbType)
                    .Where(ua => ua.PermissionSchemeId == permissionScheme.Id)
                    .ToListAsync();
                
                foreach (var access in uriAccesses)
                {
                    permissions.Add(new Domain.Permission
                    {
                        Id = access.Id,
                        EntityId = entityId,
                        Uri = access.Resource.Uri,
                        HttpVerb = Enum.Parse<Domain.HttpVerb>(access.VerbType.VerbName),
                        Grant = access.Grant,
                        Deny = access.Deny,
                        Scheme = Domain.Scheme.ApiUriAuthorization
                    });
                }
            }
        }
        
        return permissions;
    }

    public async Task<PermissionEvaluationResult> EvaluatePermissionAsync(int entityId, string uri, Domain.HttpVerb httpVerb)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new PermissionEvaluationResult
        {
            IsAllowed = false,
            Reason = "No matching permission found"
        };

        // Check cache first
        var cacheKey = $"eval_{entityId}_{uri}_{httpVerb}";
        if (_cache.TryGetValue<PermissionEvaluationResult>(cacheKey, out var cachedResult))
        {
            cachedResult.FromCache = true;
            cachedResult.EvaluationTime = stopwatch.Elapsed;
            return cachedResult;
        }

        // Get all applicable permissions
        var allPermissions = await GetEffectivePermissionsAsync(entityId);
        
        // Find matching permissions
        var matchingPermissions = allPermissions
            .Where(p => MatchesUri(p.Uri, uri) && p.HttpVerb == httpVerb)
            .ToList();
        
        if (matchingPermissions.Any())
        {
            // Resolve conflicts
            var resolvedPermission = await ResolvePermissionConflictAsync(matchingPermissions);
            
            result.IsAllowed = resolvedPermission.Grant && !resolvedPermission.Deny;
            result.Reason = result.IsAllowed ? "Permission granted" : "Permission denied";
            result.AppliedPermissions = matchingPermissions;
            
            // Trace sources
            foreach (var perm in matchingPermissions)
            {
                result.Sources.Add(new PermissionSource
                {
                    EntityId = perm.EntityId,
                    Permission = perm,
                    SourceType = DetermineSourceType(perm)
                });
            }
        }

        // Check conditional permissions
        var conditionalPerms = await GetConditionalPermissionsAsync(entityId);
        var applicableConditional = conditionalPerms
            .Where(p => MatchesUri(p.Uri, uri) && p.HttpVerb == httpVerb)
            .ToList();
        
        if (applicableConditional.Any() && !result.IsAllowed)
        {
            // Conditional permissions can override if context is valid
            result.Reason = "Conditional permission requires context validation";
        }

        // Check temporary permissions
        var tempPerms = await GetTemporaryPermissionsAsync(entityId);
        var validTempPerms = tempPerms
            .Where(p => !p.IsExpired && MatchesUri(p.Uri, uri) && p.HttpVerb == httpVerb)
            .ToList();
        
        if (validTempPerms.Any())
        {
            result.IsAllowed = validTempPerms.Any(p => p.Grant && !p.Deny);
            if (result.IsAllowed)
            {
                result.Reason = "Temporary permission granted";
                result.AppliedPermissions.AddRange(validTempPerms);
            }
        }

        result.EvaluationTime = stopwatch.Elapsed;
        
        // Cache the result
        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
        
        // Audit the evaluation
        await _auditService.LogAccessAttemptAsync(uri, httpVerb.ToString(), entityId.ToString(), 
            result.IsAllowed, result.Reason);
        
        return result;
    }

    public async Task<IEnumerable<int>> GetPermissionSourcesAsync(int entityId, string uri, Domain.HttpVerb httpVerb)
    {
        var sources = new HashSet<int>();
        
        var result = await EvaluatePermissionAsync(entityId, uri, httpVerb);
        foreach (var source in result.Sources)
        {
            sources.Add(source.EntityId);
        }
        
        return sources;
    }

    #endregion

    #region Conditional Permissions

    public async Task<bool> EvaluateConditionalPermissionAsync(int entityId, string uri, Domain.HttpVerb httpVerb, 
        Dictionary<string, object> context)
    {
        var conditionalPerms = await GetConditionalPermissionsAsync(entityId);
        var matching = conditionalPerms
            .Where(p => MatchesUri(p.Uri, uri) && p.HttpVerb == httpVerb)
            .ToList();

        foreach (var perm in matching)
        {
            if (await ValidateConditionAsync(perm.Condition, context))
            {
                // Check time validity
                if (perm.ValidFrom.HasValue && DateTime.UtcNow < perm.ValidFrom.Value)
                    continue;
                    
                if (perm.ValidUntil.HasValue && DateTime.UtcNow > perm.ValidUntil.Value)
                    continue;
                
                return perm.Grant && !perm.Deny;
            }
        }
        
        return false;
    }

    public async Task AddConditionalPermissionAsync(int entityId, ConditionalPermission permission)
    {
        if (!_conditionalPermissions.ContainsKey(entityId))
            _conditionalPermissions[entityId] = new List<ConditionalPermission>();
        
        permission.EntityId = entityId;
        _conditionalPermissions[entityId].Add(permission);
        
        await InvalidatePermissionCacheAsync(entityId);
        
        _logger.LogInformation("Added conditional permission for entity {EntityId}: {Uri} {Verb}",
            entityId, permission.Uri, permission.HttpVerb);
    }

    public async Task<IEnumerable<ConditionalPermission>> GetConditionalPermissionsAsync(int entityId)
    {
        if (_conditionalPermissions.ContainsKey(entityId))
            return await Task.FromResult(_conditionalPermissions[entityId]);
        
        return await Task.FromResult(Enumerable.Empty<ConditionalPermission>());
    }

    public async Task<bool> RemoveConditionalPermissionAsync(int entityId, int conditionId)
    {
        if (_conditionalPermissions.ContainsKey(entityId))
        {
            var removed = _conditionalPermissions[entityId].RemoveAll(p => p.Id == conditionId) > 0;
            if (removed)
            {
                await InvalidatePermissionCacheAsync(entityId);
            }
            return removed;
        }
        return false;
    }

    public async Task<bool> ValidateConditionAsync(string condition, Dictionary<string, object> context)
    {
        // Simple condition evaluation - in production, use expression trees or rules engine
        try
        {
            // Example conditions: "role == 'admin'", "department == 'IT'", "time >= '09:00'"
            var parts = condition.Split(' ');
            if (parts.Length != 3)
                return false;
            
            var contextKey = parts[0];
            var op = parts[1];
            var expectedValue = parts[2].Trim('\'', '"');
            
            if (!context.ContainsKey(contextKey))
                return false;
            
            var actualValue = context[contextKey]?.ToString() ?? "";
            
            return op switch
            {
                "==" => actualValue.Equals(expectedValue, StringComparison.OrdinalIgnoreCase),
                "!=" => !actualValue.Equals(expectedValue, StringComparison.OrdinalIgnoreCase),
                ">=" => string.Compare(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase) >= 0,
                "<=" => string.Compare(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase) <= 0,
                ">" => string.Compare(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase) > 0,
                "<" => string.Compare(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase) < 0,
                "contains" => actualValue.Contains(expectedValue, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating condition: {Condition}", condition);
            return false;
        }
    }

    #endregion

    #region Permission Caching

    public async Task<bool> HasCachedPermissionAsync(int entityId, string uri, Domain.HttpVerb httpVerb)
    {
        var cacheKey = $"perm_{entityId}_{uri}_{httpVerb}";
        return await Task.FromResult(_cache.TryGetValue(cacheKey, out _));
    }

    public async Task InvalidatePermissionCacheAsync(int entityId)
    {
        // Remove all cache entries for this entity
        var keysToRemove = new List<string>();
        
        // In production, track keys or use cache tags
        // For now, we'll invalidate common patterns
        foreach (Domain.HttpVerb verb in Enum.GetValues(typeof(Domain.HttpVerb)))
        {
            _cache.Remove($"perm_{entityId}_*_{verb}");
            _cache.Remove($"eval_{entityId}_*_{verb}");
        }
        
        _logger.LogInformation("Invalidated permission cache for entity {EntityId}", entityId);
        await Task.CompletedTask;
    }

    public async Task InvalidateAllPermissionCachesAsync()
    {
        // In production, use cache tags or clear entire cache
        // For now, we'll log the action
        _logger.LogInformation("Invalidated all permission caches");
        _cacheStats.LastReset = DateTime.UtcNow;
        _cacheStats.HitCount = 0;
        _cacheStats.MissCount = 0;
        await Task.CompletedTask;
    }

    public async Task PreloadPermissionsAsync(int entityId)
    {
        var permissions = await GetEffectivePermissionsAsync(entityId);
        
        // Cache common operations
        var commonResources = new[] { "/api/users", "/api/groups", "/api/roles", "/api/permissions" };
        var commonVerbs = new[] { Domain.HttpVerb.GET, Domain.HttpVerb.POST, Domain.HttpVerb.PUT, Domain.HttpVerb.DELETE };
        
        foreach (var resource in commonResources)
        {
            foreach (var verb in commonVerbs)
            {
                var hasPermission = permissions.Any(p => MatchesUri(p.Uri, resource) && p.HttpVerb == verb && p.Grant && !p.Deny);
                var cacheKey = $"perm_{entityId}_{resource}_{verb}";
                _cache.Set(cacheKey, hasPermission, TimeSpan.FromMinutes(5));
            }
        }
        
        _logger.LogInformation("Preloaded permissions for entity {EntityId}", entityId);
    }

    public async Task<CacheStatistics> GetCacheStatisticsAsync()
    {
        _cacheStats.TotalEntries = _cache.Count;
        _cacheStats.CacheSizeBytes = GC.GetTotalMemory(false); // Approximation
        return await Task.FromResult(_cacheStats);
    }

    #endregion

    #region Permission Hierarchy and Inheritance

    public async Task<PermissionHierarchy> GetPermissionHierarchyAsync(int entityId)
    {
        var hierarchy = new PermissionHierarchy
        {
            EntityId = entityId,
            DirectPermissions = (await GetDirectPermissionsAsync(entityId)).ToList()
        };

        var entity = await _dbContext.Entities.FindAsync(entityId);
        if (entity == null)
            return hierarchy;

        if (entity.EntityType == "User")
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.EntityId == entityId);
            if (user != null)
            {
                // Get role permissions
                var userRoles = await _dbContext.UserRoles.Where(ur => ur.UserId == user.Id).ToListAsync();
                foreach (var ur in userRoles)
                {
                    hierarchy.RolePermissions[ur.RoleId] = await GetRolePermissionsAsync(ur.RoleId);
                }
                
                // Get group permissions
                var userGroups = await _dbContext.UserGroups.Where(ug => ug.UserId == user.Id).ToListAsync();
                foreach (var ug in userGroups)
                {
                    hierarchy.GroupPermissions[ug.GroupId] = await GetGroupPermissionsAsync(ug.GroupId);
                }
            }
        }

        hierarchy.EffectivePermissions = await GetEffectivePermissionsAsync(entityId);
        return hierarchy;
    }

    public async Task<IEnumerable<Domain.Permission>> GetPermissionsFromParentsAsync(int entityId)
    {
        return await GetInheritedPermissionsAsync(entityId);
    }

    public async Task<IEnumerable<Domain.Permission>> GetPermissionsFromRolesAsync(int entityId)
    {
        var permissions = new List<Domain.Permission>();
        
        var entity = await _dbContext.Entities.FindAsync(entityId);
        if (entity?.EntityType == "User")
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.EntityId == entityId);
            if (user != null)
            {
                var userRoles = await _dbContext.UserRoles.Where(ur => ur.UserId == user.Id).ToListAsync();
                foreach (var ur in userRoles)
                {
                    permissions.AddRange(await GetRolePermissionsAsync(ur.RoleId));
                }
            }
        }
        
        return permissions;
    }

    public async Task<IEnumerable<Domain.Permission>> GetPermissionsFromGroupsAsync(int entityId)
    {
        var permissions = new List<Domain.Permission>();
        
        var entity = await _dbContext.Entities.FindAsync(entityId);
        if (entity?.EntityType == "User")
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.EntityId == entityId);
            if (user != null)
            {
                var userGroups = await _dbContext.UserGroups.Where(ug => ug.UserId == user.Id).ToListAsync();
                foreach (var ug in userGroups)
                {
                    permissions.AddRange(await GetGroupPermissionsAsync(ug.GroupId));
                }
            }
        }
        
        return permissions;
    }

    public async Task<PermissionInheritanceChain> TracePermissionSourceAsync(int entityId, string uri, Domain.HttpVerb httpVerb)
    {
        var chain = new PermissionInheritanceChain
        {
            EntityId = entityId,
            Resource = uri,
            Verb = httpVerb,
            Chain = new List<InheritanceLink>()
        };

        var result = await EvaluatePermissionAsync(entityId, uri, httpVerb);
        chain.IsAllowed = result.IsAllowed;
        
        var level = 0;
        foreach (var source in result.Sources)
        {
            chain.Chain.Add(new InheritanceLink
            {
                SourceEntityId = source.EntityId,
                SourceType = source.SourceType,
                Permission = source.Permission,
                Level = level++
            });
        }
        
        return chain;
    }

    #endregion

    #region Permission Conflicts and Resolution

    public async Task<IEnumerable<PermissionConflict>> DetectPermissionConflictsAsync(int entityId)
    {
        var conflicts = new List<PermissionConflict>();
        var permissions = await GetEffectivePermissionsAsync(entityId);
        
        // Group by resource and verb
        var grouped = permissions.GroupBy(p => new { p.Uri, p.HttpVerb });
        
        foreach (var group in grouped)
        {
            var perms = group.ToList();
            if (perms.Count > 1)
            {
                // Check for grant/deny conflicts
                var hasGrant = perms.Any(p => p.Grant);
                var hasDeny = perms.Any(p => p.Deny);
                
                if (hasGrant && hasDeny)
                {
                    conflicts.Add(new PermissionConflict
                    {
                        Resource = group.Key.Uri,
                        Verb = group.Key.HttpVerb,
                        ConflictingPermissions = perms,
                        ConflictType = "Grant/Deny",
                        ResolvedPermission = await ResolvePermissionConflictAsync(perms)
                    });
                }
            }
        }
        
        return conflicts;
    }

    public async Task<Domain.Permission> ResolvePermissionConflictAsync(IEnumerable<Domain.Permission> conflictingPermissions)
    {
        var permissions = conflictingPermissions.ToList();
        if (!permissions.Any())
            throw new ArgumentException("No permissions to resolve");
        
        if (permissions.Count == 1)
            return permissions.First();
        
        Domain.Permission resolved = permissions.First();
        
        switch (_conflictResolutionStrategy)
        {
            case ConflictResolutionStrategy.DenyOverrides:
                // If any permission denies, deny wins
                if (permissions.Any(p => p.Deny))
                {
                    resolved = permissions.First(p => p.Deny);
                }
                break;
                
            case ConflictResolutionStrategy.GrantOverrides:
                // If any permission grants, grant wins
                if (permissions.Any(p => p.Grant))
                {
                    resolved = permissions.First(p => p.Grant);
                }
                break;
                
            case ConflictResolutionStrategy.MostSpecific:
                // More specific URI wins (longer path)
                resolved = permissions.OrderByDescending(p => p.Uri.Length).First();
                break;
                
            case ConflictResolutionStrategy.MostRecent:
                // Most recently added wins (highest ID)
                resolved = permissions.OrderByDescending(p => p.Id).First();
                break;
                
            case ConflictResolutionStrategy.HighestPriority:
                // Would need priority field on permissions
                resolved = permissions.First();
                break;
        }
        
        return await Task.FromResult(resolved);
    }

    public async Task<ConflictResolutionStrategy> GetConflictResolutionStrategyAsync()
    {
        return await Task.FromResult(_conflictResolutionStrategy);
    }

    public async Task SetConflictResolutionStrategyAsync(ConflictResolutionStrategy strategy)
    {
        _conflictResolutionStrategy = strategy;
        await InvalidateAllPermissionCachesAsync();
        _logger.LogInformation("Changed conflict resolution strategy to {Strategy}", strategy);
    }

    public async Task<IEnumerable<Domain.Permission>> GetConflictingPermissionsAsync(int entityId, string uri, Domain.HttpVerb httpVerb)
    {
        var permissions = await GetEffectivePermissionsAsync(entityId);
        return permissions.Where(p => MatchesUri(p.Uri, uri) && p.HttpVerb == httpVerb);
    }

    #endregion

    #region Permission Templates and Presets

    public async Task<PermissionTemplate?> GetPermissionTemplateAsync(string templateName)
    {
        if (_permissionTemplates.ContainsKey(templateName))
            return await Task.FromResult(_permissionTemplates[templateName]);
        return null;
    }

    public async Task<IEnumerable<PermissionTemplate>> GetAllPermissionTemplatesAsync()
    {
        return await Task.FromResult(_permissionTemplates.Values);
    }

    public async Task ApplyPermissionTemplateAsync(int entityId, string templateName)
    {
        var template = await GetPermissionTemplateAsync(templateName);
        if (template == null)
            throw new InvalidOperationException($"Template {templateName} not found");
        
        foreach (var permission in template.Permissions)
        {
            permission.EntityId = entityId;
            // In production, save to database
        }
        
        await InvalidatePermissionCacheAsync(entityId);
        _logger.LogInformation("Applied template {Template} to entity {EntityId}", templateName, entityId);
    }

    public async Task<PermissionTemplate> CreatePermissionTemplateAsync(string name, IEnumerable<Domain.Permission> permissions)
    {
        var template = new PermissionTemplate
        {
            Name = name,
            Description = $"Custom template {name}",
            Permissions = permissions.ToList(),
            CreatedAt = DateTime.UtcNow
        };
        
        _permissionTemplates[name] = template;
        _logger.LogInformation("Created permission template {Template}", name);
        return await Task.FromResult(template);
    }

    public async Task<bool> DeletePermissionTemplateAsync(string templateName)
    {
        var removed = _permissionTemplates.Remove(templateName);
        if (removed)
        {
            _logger.LogInformation("Deleted permission template {Template}", templateName);
        }
        return await Task.FromResult(removed);
    }

    #endregion

    #region Bulk Permission Operations

    public async Task<Dictionary<int, bool>> EvaluatePermissionsBulkAsync(IEnumerable<int> entityIds, string uri, Domain.HttpVerb httpVerb)
    {
        var results = new Dictionary<int, bool>();
        
        // Evaluate in parallel for performance
        var tasks = entityIds.Select(async id =>
        {
            var hasPermission = await HasPermissionAsync(id, uri, httpVerb);
            return (id, hasPermission);
        });
        
        var evaluations = await Task.WhenAll(tasks);
        
        foreach (var (id, hasPermission) in evaluations)
        {
            results[id] = hasPermission;
        }
        
        return results;
    }

    public async Task<Dictionary<string, bool>> EvaluateMultipleResourcesAsync(int entityId, IEnumerable<string> uris, Domain.HttpVerb httpVerb)
    {
        var results = new Dictionary<string, bool>();
        
        var tasks = uris.Select(async uri =>
        {
            var hasPermission = await HasPermissionAsync(entityId, uri, httpVerb);
            return (uri, hasPermission);
        });
        
        var evaluations = await Task.WhenAll(tasks);
        
        foreach (var (uri, hasPermission) in evaluations)
        {
            results[uri] = hasPermission;
        }
        
        return results;
    }

    public async Task GrantPermissionsBulkAsync(int entityId, IEnumerable<Domain.Permission> permissions)
    {
        foreach (var permission in permissions)
        {
            permission.EntityId = entityId;
            // In production, save to database
        }
        
        await InvalidatePermissionCacheAsync(entityId);
        _logger.LogInformation("Granted {Count} permissions to entity {EntityId}", 
            permissions.Count(), entityId);
    }

    public async Task RevokePermissionsBulkAsync(int entityId, IEnumerable<Domain.Permission> permissions)
    {
        // In production, remove from database
        await InvalidatePermissionCacheAsync(entityId);
        _logger.LogInformation("Revoked {Count} permissions from entity {EntityId}", 
            permissions.Count(), entityId);
    }

    public async Task<IEnumerable<Domain.Permission>> GetPermissionsBulkAsync(IEnumerable<int> entityIds)
    {
        var allPermissions = new List<Domain.Permission>();
        
        foreach (var id in entityIds)
        {
            allPermissions.AddRange(await GetEffectivePermissionsAsync(id));
        }
        
        return allPermissions;
    }

    #endregion

    #region Permission Analysis and Reporting

    public async Task<PermissionMatrix> GeneratePermissionMatrixAsync(IEnumerable<int> entityIds, IEnumerable<string> resources)
    {
        var matrix = new PermissionMatrix
        {
            EntityIds = entityIds.ToList(),
            Resources = resources.ToList(),
            GeneratedAt = DateTime.UtcNow
        };

        foreach (var entityId in entityIds)
        {
            matrix.Matrix[entityId] = new Dictionary<string, Dictionary<Domain.HttpVerb, bool>>();
            
            foreach (var resource in resources)
            {
                matrix.Matrix[entityId][resource] = new Dictionary<Domain.HttpVerb, bool>();
                
                foreach (Domain.HttpVerb verb in Enum.GetValues(typeof(Domain.HttpVerb)))
                {
                    matrix.Matrix[entityId][resource][verb] = await HasPermissionAsync(entityId, resource, verb);
                }
            }
        }
        
        return matrix;
    }

    public async Task<EffectivePermissionReport> GenerateEffectivePermissionReportAsync(int entityId)
    {
        var report = new EffectivePermissionReport
        {
            EntityId = entityId,
            DirectPermissions = (await GetDirectPermissionsAsync(entityId)).ToList(),
            InheritedPermissions = (await GetInheritedPermissionsAsync(entityId)).ToList(),
            ConditionalPermissions = (await GetConditionalPermissionsAsync(entityId)).ToList(),
            TemporaryPermissions = (await GetTemporaryPermissionsAsync(entityId)).ToList(),
            DelegatedPermissions = (await GetDelegatedPermissionsAsync(entityId)).ToList(),
            EffectivePermissions = await GetEffectivePermissionsAsync(entityId),
            Conflicts = (await DetectPermissionConflictsAsync(entityId)).ToList(),
            GeneratedAt = DateTime.UtcNow
        };
        
        return report;
    }

    public async Task<IEnumerable<PermissionGap>> IdentifyPermissionGapsAsync(int entityId, IEnumerable<string> requiredResources)
    {
        var gaps = new List<PermissionGap>();
        var currentPermissions = await GetEffectivePermissionsAsync(entityId);
        
        foreach (var resource in requiredResources)
        {
            foreach (Domain.HttpVerb verb in Enum.GetValues(typeof(Domain.HttpVerb)))
            {
                var hasPermission = currentPermissions.Any(p => 
                    MatchesUri(p.Uri, resource) && p.HttpVerb == verb && p.Grant && !p.Deny);
                
                if (!hasPermission)
                {
                    gaps.Add(new PermissionGap
                    {
                        Resource = resource,
                        Verb = verb,
                        Reason = "Missing required permission",
                        SuggestedPermissions = new List<Domain.Permission>
                        {
                            new Domain.Permission
                            {
                                Uri = resource,
                                HttpVerb = verb,
                                Grant = true,
                                Deny = false,
                                Scheme = Domain.Scheme.ApiUriAuthorization
                            }
                        }
                    });
                }
            }
        }
        
        return gaps;
    }

    public async Task<IEnumerable<ExcessivePermission>> IdentifyExcessivePermissionsAsync(int entityId)
    {
        var excessive = new List<ExcessivePermission>();
        var permissions = await GetEffectivePermissionsAsync(entityId);
        
        // Identify overly broad permissions
        foreach (var perm in permissions.Where(p => p.Uri.Contains("*")))
        {
            excessive.Add(new ExcessivePermission
            {
                Permission = perm,
                Reason = "Wildcard permission may be too broad",
                LastUsed = DateTime.UtcNow.AddDays(-30), // Would need tracking
                UsageCount = 0
            });
        }
        
        return excessive;
    }

    public async Task<PermissionAuditReport> AuditPermissionsAsync(int entityId, DateTime? since = null)
    {
        var report = new PermissionAuditReport
        {
            EntityId = entityId,
            AuditDate = DateTime.UtcNow,
            Changes = new List<PermissionChange>(),
            Anomalies = new List<PermissionAnomaly>(),
            Statistics = new Dictionary<string, int>()
        };
        
        // Get current permissions
        var currentPermissions = await GetEffectivePermissionsAsync(entityId);
        
        report.Statistics["TotalPermissions"] = currentPermissions.Count;
        report.Statistics["GrantPermissions"] = currentPermissions.Count(p => p.Grant);
        report.Statistics["DenyPermissions"] = currentPermissions.Count(p => p.Deny);
        report.Statistics["WildcardPermissions"] = currentPermissions.Count(p => p.Uri.Contains("*"));
        
        // Detect anomalies
        var conflicts = await DetectPermissionConflictsAsync(entityId);
        foreach (var conflict in conflicts)
        {
            report.Anomalies.Add(new PermissionAnomaly
            {
                AnomalyType = "Conflict",
                Description = $"Conflicting permissions for {conflict.Resource} {conflict.Verb}",
                RelatedPermission = conflict.ResolvedPermission ?? conflict.ConflictingPermissions.First(),
                DetectedAt = DateTime.UtcNow
            });
        }
        
        return report;
    }

    #endregion

    #region Permission Delegation

    public async Task<bool> CanDelegatePermissionAsync(int delegatorId, int delegateeId, Domain.Permission permission)
    {
        // Check if delegator has the permission
        var hasPerm = await HasPermissionAsync(delegatorId, permission.Uri, permission.HttpVerb);
        if (!hasPerm)
            return false;
        
        // Check if delegator has delegation rights (would need additional permission)
        // For now, assume anyone with a permission can delegate it
        return true;
    }

    public async Task DelegatePermissionAsync(int delegatorId, int delegateeId, Domain.Permission permission, DateTime? expiresAt = null)
    {
        if (!await CanDelegatePermissionAsync(delegatorId, delegateeId, permission))
            throw new UnauthorizedAccessException("Cannot delegate this permission");
        
        var delegated = new DelegatedPermission
        {
            DelegationId = _delegatedPermissions.Count + 1,
            DelegatorId = delegatorId,
            DelegateeId = delegateeId,
            DelegatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            Uri = permission.Uri,
            HttpVerb = permission.HttpVerb,
            Grant = permission.Grant,
            Deny = permission.Deny,
            Scheme = permission.Scheme,
            EntityId = delegateeId
        };
        
        if (!_delegatedPermissions.ContainsKey(delegateeId))
            _delegatedPermissions[delegateeId] = new List<DelegatedPermission>();
        
        _delegatedPermissions[delegateeId].Add(delegated);
        
        await InvalidatePermissionCacheAsync(delegateeId);
        _logger.LogInformation("Delegated permission from {Delegator} to {Delegatee}: {Uri} {Verb}",
            delegatorId, delegateeId, permission.Uri, permission.HttpVerb);
    }

    public async Task<IEnumerable<DelegatedPermission>> GetDelegatedPermissionsAsync(int entityId)
    {
        if (_delegatedPermissions.ContainsKey(entityId))
        {
            return await Task.FromResult(_delegatedPermissions[entityId]
                .Where(p => !p.IsRevoked && (!p.ExpiresAt.HasValue || p.ExpiresAt.Value > DateTime.UtcNow)));
        }
        return await Task.FromResult(Enumerable.Empty<DelegatedPermission>());
    }

    public async Task<IEnumerable<DelegatedPermission>> GetPermissionsDelegatedByAsync(int entityId)
    {
        var delegated = new List<DelegatedPermission>();
        
        foreach (var kvp in _delegatedPermissions)
        {
            delegated.AddRange(kvp.Value.Where(p => p.DelegatorId == entityId && !p.IsRevoked));
        }
        
        return await Task.FromResult(delegated);
    }

    public async Task RevokeDelegatedPermissionAsync(int delegationId)
    {
        foreach (var kvp in _delegatedPermissions)
        {
            var perm = kvp.Value.FirstOrDefault(p => p.DelegationId == delegationId);
            if (perm != null)
            {
                perm.IsRevoked = true;
                await InvalidatePermissionCacheAsync(kvp.Key);
                _logger.LogInformation("Revoked delegated permission {DelegationId}", delegationId);
                return;
            }
        }
    }

    #endregion

    #region Time-based Permissions

    public async Task<bool> HasTemporaryPermissionAsync(int entityId, string uri, Domain.HttpVerb httpVerb, DateTime? asOf = null)
    {
        var checkTime = asOf ?? DateTime.UtcNow;
        var tempPerms = await GetTemporaryPermissionsAsync(entityId);
        
        return tempPerms.Any(p => 
            MatchesUri(p.Uri, uri) && 
            p.HttpVerb == httpVerb && 
            p.GrantedAt <= checkTime && 
            p.ExpiresAt > checkTime &&
            p.Grant && !p.Deny);
    }

    public async Task GrantTemporaryPermissionAsync(int entityId, Domain.Permission permission, DateTime expiresAt)
    {
        var tempPerm = new TemporaryPermission
        {
            EntityId = entityId,
            Uri = permission.Uri,
            HttpVerb = permission.HttpVerb,
            Grant = permission.Grant,
            Deny = permission.Deny,
            Scheme = permission.Scheme,
            GrantedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            GrantedBy = "SYSTEM",
            Reason = "Temporary access granted"
        };
        
        if (!_temporaryPermissions.ContainsKey(entityId))
            _temporaryPermissions[entityId] = new List<TemporaryPermission>();
        
        _temporaryPermissions[entityId].Add(tempPerm);
        
        await InvalidatePermissionCacheAsync(entityId);
        _logger.LogInformation("Granted temporary permission to entity {EntityId}: {Uri} {Verb} until {ExpiresAt}",
            entityId, permission.Uri, permission.HttpVerb, expiresAt);
    }

    public async Task<IEnumerable<TemporaryPermission>> GetTemporaryPermissionsAsync(int entityId)
    {
        if (_temporaryPermissions.ContainsKey(entityId))
        {
            return await Task.FromResult(_temporaryPermissions[entityId].Where(p => !p.IsExpired));
        }
        return await Task.FromResult(Enumerable.Empty<TemporaryPermission>());
    }

    public async Task<IEnumerable<TemporaryPermission>> GetExpiredPermissionsAsync(int entityId)
    {
        if (_temporaryPermissions.ContainsKey(entityId))
        {
            return await Task.FromResult(_temporaryPermissions[entityId].Where(p => p.IsExpired));
        }
        return await Task.FromResult(Enumerable.Empty<TemporaryPermission>());
    }

    public async Task CleanupExpiredPermissionsAsync()
    {
        var totalCleaned = 0;
        
        foreach (var kvp in _temporaryPermissions)
        {
            var expired = kvp.Value.Where(p => p.IsExpired).ToList();
            foreach (var perm in expired)
            {
                kvp.Value.Remove(perm);
                totalCleaned++;
            }
            
            if (expired.Any())
            {
                await InvalidatePermissionCacheAsync(kvp.Key);
            }
        }
        
        _logger.LogInformation("Cleaned up {Count} expired temporary permissions", totalCleaned);
    }

    #endregion

    #region Permission Policies

    public async Task<bool> EvaluatePolicyAsync(int entityId, string policyName, Dictionary<string, object> context)
    {
        if (!_permissionPolicies.ContainsKey(policyName))
            return false;
        
        var policy = _permissionPolicies[policyName];
        if (!policy.IsActive)
            return false;
        
        // Evaluate policy rule
        return await ValidateConditionAsync(policy.PolicyRule, context);
    }

    public async Task<PermissionPolicy?> GetPolicyAsync(string policyName)
    {
        if (_permissionPolicies.ContainsKey(policyName))
            return await Task.FromResult(_permissionPolicies[policyName]);
        return null;
    }

    public async Task<IEnumerable<PermissionPolicy>> GetApplicablePoliciesAsync(int entityId)
    {
        // In production, filter by entity attributes
        return await Task.FromResult(_permissionPolicies.Values.Where(p => p.IsActive));
    }

    public async Task CreatePolicyAsync(PermissionPolicy policy)
    {
        _permissionPolicies[policy.Name] = policy;
        _logger.LogInformation("Created permission policy {Policy}", policy.Name);
        await Task.CompletedTask;
    }

    public async Task UpdatePolicyAsync(string policyName, PermissionPolicy policy)
    {
        if (_permissionPolicies.ContainsKey(policyName))
        {
            _permissionPolicies[policyName] = policy;
            await InvalidateAllPermissionCachesAsync();
            _logger.LogInformation("Updated permission policy {Policy}", policyName);
        }
    }

    public async Task DeletePolicyAsync(string policyName)
    {
        if (_permissionPolicies.Remove(policyName))
        {
            await InvalidateAllPermissionCachesAsync();
            _logger.LogInformation("Deleted permission policy {Policy}", policyName);
        }
    }

    #endregion

    #region Permission Optimization

    public async Task OptimizePermissionsAsync(int entityId)
    {
        // Remove redundant permissions
        await RemoveRedundantPermissionsAsync(entityId);
        
        // Consolidate similar permissions
        await ConsolidatePermissionsAsync(entityId);
        
        await InvalidatePermissionCacheAsync(entityId);
        _logger.LogInformation("Optimized permissions for entity {EntityId}", entityId);
    }

    public async Task<IEnumerable<RedundantPermission>> FindRedundantPermissionsAsync(int entityId)
    {
        var redundant = new List<RedundantPermission>();
        var permissions = await GetEffectivePermissionsAsync(entityId);
        
        // Find permissions superseded by wildcards
        var wildcardPerms = permissions.Where(p => p.Uri.Contains("*")).ToList();
        
        foreach (var specific in permissions.Where(p => !p.Uri.Contains("*")))
        {
            foreach (var wildcard in wildcardPerms)
            {
                if (MatchesUri(wildcard.Uri, specific.Uri) && wildcard.HttpVerb == specific.HttpVerb)
                {
                    redundant.Add(new RedundantPermission
                    {
                        Permission = specific,
                        SupersededBy = wildcard,
                        Reason = "Superseded by wildcard permission"
                    });
                }
            }
        }
        
        return redundant;
    }

    public async Task RemoveRedundantPermissionsAsync(int entityId)
    {
        var redundant = await FindRedundantPermissionsAsync(entityId);
        
        // In production, remove from database
        foreach (var r in redundant)
        {
            _logger.LogInformation("Removing redundant permission: {Uri} {Verb} superseded by {SuperUri}",
                r.Permission.Uri, r.Permission.HttpVerb, r.SupersededBy.Uri);
        }
        
        await InvalidatePermissionCacheAsync(entityId);
    }

    public async Task<PermissionOptimizationReport> AnalyzePermissionEfficiencyAsync(int entityId)
    {
        var report = new PermissionOptimizationReport
        {
            EntityId = entityId,
            GeneratedAt = DateTime.UtcNow
        };
        
        var permissions = await GetEffectivePermissionsAsync(entityId);
        report.TotalPermissions = permissions.Count;
        
        var redundant = await FindRedundantPermissionsAsync(entityId);
        report.RedundantPermissions = redundant.Count();
        
        var conflicts = await DetectPermissionConflictsAsync(entityId);
        report.ConflictingPermissions = conflicts.Count();
        
        // Calculate efficiency score
        var score = 100.0;
        score -= (report.RedundantPermissions * 5);
        score -= (report.ConflictingPermissions * 10);
        report.EfficiencyScore = Math.Max(0, score);
        
        // Add recommendations
        if (report.RedundantPermissions > 0)
            report.Recommendations.Add($"Remove {report.RedundantPermissions} redundant permissions");
        
        if (report.ConflictingPermissions > 0)
            report.Recommendations.Add($"Resolve {report.ConflictingPermissions} permission conflicts");
        
        if (permissions.Count(p => p.Uri.Contains("*")) > permissions.Count / 2)
            report.Recommendations.Add("Consider using more specific permissions instead of wildcards");
        
        return report;
    }

    public async Task ConsolidatePermissionsAsync(int entityId)
    {
        var permissions = await GetEffectivePermissionsAsync(entityId);
        
        // Group similar permissions
        var grouped = permissions.GroupBy(p => p.HttpVerb);
        
        foreach (var group in grouped)
        {
            var uris = group.Select(p => p.Uri).Distinct().ToList();
            
            // If many specific URIs under same path, suggest wildcard
            var pathGroups = uris.GroupBy(u => GetBasePath(u));
            foreach (var pathGroup in pathGroups.Where(g => g.Count() > 5))
            {
                _logger.LogInformation("Consider consolidating {Count} permissions under {Path}/* for verb {Verb}",
                    pathGroup.Count(), pathGroup.Key, group.Key);
            }
        }
        
        await Task.CompletedTask;
    }

    #endregion

    #region Helper Methods

    private bool MatchesUri(string pattern, string uri)
    {
        if (pattern == uri)
            return true;
        
        if (pattern.Contains("*"))
        {
            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(uri, regexPattern, RegexOptions.IgnoreCase);
        }
        
        if (pattern.Contains("{") && pattern.Contains("}"))
        {
            var regexPattern = "^" + Regex.Replace(pattern, @"\{[^}]+\}", "([^/]+)") + "$";
            return Regex.IsMatch(uri, regexPattern, RegexOptions.IgnoreCase);
        }
        
        return false;
    }

    private string DetermineSourceType(Domain.Permission permission)
    {
        // In production, track source when creating permissions
        if (_conditionalPermissions.Values.Any(list => list.Any(p => p.Id == permission.Id)))
            return "Conditional";
        
        if (_temporaryPermissions.Values.Any(list => list.Any(p => p.Id == permission.Id)))
            return "Temporary";
        
        if (_delegatedPermissions.Values.Any(list => list.Any(p => p.Id == permission.Id)))
            return "Delegated";
        
        return "Direct";
    }

    private async Task<List<Domain.Permission>> ResolvePermissionConflictsAsync(List<Domain.Permission> permissions)
    {
        var resolved = new List<Domain.Permission>();
        var grouped = permissions.GroupBy(p => new { p.Uri, p.HttpVerb });
        
        foreach (var group in grouped)
        {
            if (group.Count() == 1)
            {
                resolved.Add(group.First());
            }
            else
            {
                resolved.Add(await ResolvePermissionConflictAsync(group));
            }
        }
        
        return resolved;
    }

    private string GetBasePath(string uri)
    {
        var parts = uri.Split('/');
        if (parts.Length > 2)
            return string.Join("/", parts.Take(3));
        return uri;
    }

    private void InitializeDefaultTemplates()
    {
        // Admin template
        _permissionTemplates["Admin"] = new PermissionTemplate
        {
            Name = "Admin",
            Description = "Full administrative access",
            Permissions = new List<Domain.Permission>
            {
                new() { Uri = "*", HttpVerb = Domain.HttpVerb.GET, Grant = true },
                new() { Uri = "*", HttpVerb = Domain.HttpVerb.POST, Grant = true },
                new() { Uri = "*", HttpVerb = Domain.HttpVerb.PUT, Grant = true },
                new() { Uri = "*", HttpVerb = Domain.HttpVerb.DELETE, Grant = true }
            },
            CreatedAt = DateTime.UtcNow
        };
        
        // ReadOnly template
        _permissionTemplates["ReadOnly"] = new PermissionTemplate
        {
            Name = "ReadOnly",
            Description = "Read-only access to all resources",
            Permissions = new List<Domain.Permission>
            {
                new() { Uri = "*", HttpVerb = Domain.HttpVerb.GET, Grant = true },
                new() { Uri = "*", HttpVerb = Domain.HttpVerb.POST, Deny = true },
                new() { Uri = "*", HttpVerb = Domain.HttpVerb.PUT, Deny = true },
                new() { Uri = "*", HttpVerb = Domain.HttpVerb.DELETE, Deny = true }
            },
            CreatedAt = DateTime.UtcNow
        };
        
        // Operator template
        _permissionTemplates["Operator"] = new PermissionTemplate
        {
            Name = "Operator",
            Description = "Operational access without delete",
            Permissions = new List<Domain.Permission>
            {
                new() { Uri = "*", HttpVerb = Domain.HttpVerb.GET, Grant = true },
                new() { Uri = "*", HttpVerb = Domain.HttpVerb.POST, Grant = true },
                new() { Uri = "*", HttpVerb = Domain.HttpVerb.PUT, Grant = true },
                new() { Uri = "*", HttpVerb = Domain.HttpVerb.DELETE, Deny = true }
            },
            CreatedAt = DateTime.UtcNow
        };
    }

    private void InitializeDefaultPolicies()
    {
        // Time-based access policy
        _permissionPolicies["BusinessHours"] = new PermissionPolicy
        {
            Name = "BusinessHours",
            Description = "Access only during business hours",
            PolicyRule = "hour >= '08:00' && hour <= '18:00'",
            IsActive = true,
            Priority = 1
        };
        
        // Department-based policy
        _permissionPolicies["ITOnly"] = new PermissionPolicy
        {
            Name = "ITOnly",
            Description = "Access restricted to IT department",
            PolicyRule = "department == 'IT'",
            RequiredClaims = new List<string> { "department" },
            IsActive = true,
            Priority = 2
        };
        
        // Location-based policy
        _permissionPolicies["OnPremiseOnly"] = new PermissionPolicy
        {
            Name = "OnPremiseOnly",
            Description = "Access only from on-premise network",
            PolicyRule = "location == 'onpremise'",
            RequiredClaims = new List<string> { "location" },
            IsActive = false,
            Priority = 3
        };
    }

    #endregion
}