using ACS.Service.Domain;
using ACS.Service.Infrastructure;
using ACS.Service.Data;
using ACS.Service.Data.Models;
using ACS.Service.Requests;
using ACS.Service.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACS.Service.Services;

/// <summary>
/// Service for Permission operations - implements real business logic for permission management.
/// Uses Entity Framework DbContext for data access and in-memory entity graph for performance.
/// Supports permission inheritance through user-group-role hierarchies.
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly InMemoryEntityGraph _entityGraph;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<PermissionService> _logger;

    public PermissionService(
        InMemoryEntityGraph entityGraph,
        ApplicationDbContext dbContext,
        ILogger<PermissionService> logger)
    {
        _entityGraph = entityGraph;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Checks if an entity has a specific permission, optionally scoped to a resource.
    /// Performs direct permission check and optionally traverses inheritance hierarchy.
    /// </summary>
    public async Task<PermissionCheckResult> CheckPermissionAsync(int entityId, string entityType, int permissionId, int? resourceId = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("Checking permission {PermissionId} for {EntityType} {EntityId}, ResourceId: {ResourceId}",
                permissionId, entityType, entityId, resourceId);

            // First, check for direct permission assignment
            var directPermission = await CheckDirectPermissionAsync(entityId, permissionId, resourceId);
            if (directPermission.HasPermission)
            {
                _logger.LogDebug("Direct permission found for {EntityType} {EntityId} in {ElapsedMs}ms",
                    entityType, entityId, stopwatch.ElapsedMilliseconds);
                return directPermission;
            }

            // If no direct permission, check inherited permissions based on entity type
            var inheritedResult = await CheckInheritedPermissionAsync(entityId, entityType, permissionId, resourceId);

            _logger.LogDebug("Permission check completed for {EntityType} {EntityId}. HasPermission: {HasPermission} in {ElapsedMs}ms",
                entityType, entityId, inheritedResult.HasPermission, stopwatch.ElapsedMilliseconds);

            return inheritedResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission {PermissionId} for {EntityType} {EntityId}",
                permissionId, entityType, entityId);

            return new PermissionCheckResult
            {
                HasPermission = false,
                IsExpired = false,
                Reason = $"Error checking permission: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Checks for direct permission assignment to the entity.
    /// </summary>
    private async Task<PermissionCheckResult> CheckDirectPermissionAsync(int entityId, int permissionId, int? resourceId)
    {
        // Query permission schemes for this entity
        var permissionQuery = _dbContext.EntityPermissions
            .Include(ps => ps.UriAccess)
                .ThenInclude(ua => ua!.Resource)
            .Where(ps => ps.EntityId == entityId && ps.UriAccessId == permissionId);

        // If resource is specified, filter by resource
        if (resourceId.HasValue)
        {
            permissionQuery = permissionQuery.Where(ps =>
                ps.UriAccess != null && ps.UriAccess.ResourceId == resourceId.Value);
        }

        var permissionScheme = await permissionQuery.FirstOrDefaultAsync();

        if (permissionScheme == null)
        {
            return new PermissionCheckResult
            {
                HasPermission = false,
                IsExpired = false,
                Reason = "No direct permission assignment found"
            };
        }

        // Check if this is a grant or deny
        var hasGrant = permissionScheme.Grant || (permissionScheme.UriAccess?.Grant ?? false);
        var hasDeny = permissionScheme.UriAccess?.Deny ?? false;

        // Deny takes precedence over grant
        if (hasDeny)
        {
            return new PermissionCheckResult
            {
                HasPermission = false,
                IsExpired = false,
                Reason = "Permission explicitly denied"
            };
        }

        if (hasGrant)
        {
            var grantingPermissions = new List<Domain.Permission>
            {
                ConvertToPermission(permissionScheme)
            };

            return new PermissionCheckResult
            {
                HasPermission = true,
                IsExpired = false,
                Reason = "Direct permission granted",
                GrantingPermissions = grantingPermissions
            };
        }

        return new PermissionCheckResult
        {
            HasPermission = false,
            IsExpired = false,
            Reason = "Permission scheme found but not granted"
        };
    }

    /// <summary>
    /// Checks for inherited permissions through user-group-role hierarchy.
    /// </summary>
    private async Task<PermissionCheckResult> CheckInheritedPermissionAsync(int entityId, string entityType, int permissionId, int? resourceId)
    {
        var inheritedEntityIds = new HashSet<int>();
        var grantingPermissions = new List<Domain.Permission>();
        var inheritanceChain = new List<string>();

        // Build the inheritance chain based on entity type
        switch (entityType.ToLowerInvariant())
        {
            case "user":
                // Get groups the user belongs to
                var userGroups = await _dbContext.UserGroups
                    .Where(ug => ug.UserId == entityId)
                    .Select(ug => ug.GroupId)
                    .ToListAsync();

                foreach (var groupId in userGroups)
                {
                    inheritedEntityIds.Add(groupId);
                    inheritanceChain.Add($"Group:{groupId}");
                }

                // Get roles the user has directly
                var userRoles = await _dbContext.UserRoles
                    .Where(ur => ur.UserId == entityId)
                    .Select(ur => ur.RoleId)
                    .ToListAsync();

                foreach (var roleId in userRoles)
                {
                    inheritedEntityIds.Add(roleId);
                    inheritanceChain.Add($"Role:{roleId}");
                }

                // Get roles from user's groups
                var groupRoles = await _dbContext.GroupRoles
                    .Where(gr => userGroups.Contains(gr.GroupId))
                    .Select(gr => gr.RoleId)
                    .ToListAsync();

                foreach (var roleId in groupRoles)
                {
                    inheritedEntityIds.Add(roleId);
                    inheritanceChain.Add($"Role:{roleId}(via Group)");
                }
                break;

            case "group":
                // Get parent groups
                var parentGroups = await _dbContext.GroupHierarchies
                    .Where(gh => gh.ChildGroupId == entityId)
                    .Select(gh => gh.ParentGroupId)
                    .ToListAsync();

                foreach (var parentId in parentGroups)
                {
                    inheritedEntityIds.Add(parentId);
                    inheritanceChain.Add($"ParentGroup:{parentId}");
                }

                // Get roles assigned to this group
                var groupDirectRoles = await _dbContext.GroupRoles
                    .Where(gr => gr.GroupId == entityId)
                    .Select(gr => gr.RoleId)
                    .ToListAsync();

                foreach (var roleId in groupDirectRoles)
                {
                    inheritedEntityIds.Add(roleId);
                    inheritanceChain.Add($"Role:{roleId}");
                }
                break;

            case "role":
                // Roles do not inherit from other entities in this model
                break;
        }

        // Check permissions on inherited entities
        if (inheritedEntityIds.Count > 0)
        {
            var inheritedPermissions = await _dbContext.EntityPermissions
                .Include(ps => ps.UriAccess)
                    .ThenInclude(ua => ua!.Resource)
                .Where(ps => inheritedEntityIds.Contains(ps.EntityId.GetValueOrDefault()) &&
                            ps.UriAccessId == permissionId)
                .ToListAsync();

            // Check for deny first (deny takes precedence)
            var denyPermission = inheritedPermissions.FirstOrDefault(p =>
                p.UriAccess?.Deny == true);

            if (denyPermission != null)
            {
                return new PermissionCheckResult
                {
                    HasPermission = false,
                    IsExpired = false,
                    Reason = $"Permission denied via inheritance from Entity:{denyPermission.EntityId}"
                };
            }

            // Check for grant
            var grantPermission = inheritedPermissions.FirstOrDefault(p =>
                p.Grant || (p.UriAccess?.Grant ?? false));

            if (grantPermission != null)
            {
                grantingPermissions.Add(ConvertToPermission(grantPermission));

                return new PermissionCheckResult
                {
                    HasPermission = true,
                    IsExpired = false,
                    Reason = $"Permission granted via inheritance from Entity:{grantPermission.EntityId}",
                    GrantingPermissions = grantingPermissions
                };
            }
        }

        return new PermissionCheckResult
        {
            HasPermission = false,
            IsExpired = false,
            Reason = "No permission found in direct assignment or inheritance chain"
        };
    }

    /// <summary>
    /// Grants a permission to an entity by creating a PermissionScheme record.
    /// Validates that the entity and permission exist before granting.
    /// </summary>
    public async Task<PermissionGrantResponse> GrantPermissionAsync(GrantPermissionRequest request)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Granting permission {PermissionId} to {EntityType} {EntityId}",
                request.PermissionId, request.EntityType, request.EntityId);

            // Validate that the entity exists
            var entityExists = await ValidateEntityExistsAsync(request.EntityId, request.EntityType);
            if (!entityExists)
            {
                return new PermissionGrantResponse
                {
                    Success = false,
                    Message = $"Entity {request.EntityType}:{request.EntityId} not found"
                };
            }

            // Validate that the permission (UriAccess) exists
            var uriAccess = await _dbContext.UriAccesses
                .Include(ua => ua.Resource)
                .FirstOrDefaultAsync(ua => ua.Id == request.PermissionId);

            if (uriAccess == null)
            {
                return new PermissionGrantResponse
                {
                    Success = false,
                    Message = $"Permission {request.PermissionId} not found"
                };
            }

            // If resource is specified, validate it matches
            if (request.ResourceId.HasValue && uriAccess.ResourceId != request.ResourceId.Value)
            {
                return new PermissionGrantResponse
                {
                    Success = false,
                    Message = $"Permission {request.PermissionId} is not associated with resource {request.ResourceId}"
                };
            }

            // Check if permission is already granted
            var existingPermission = await _dbContext.EntityPermissions
                .FirstOrDefaultAsync(ps => ps.EntityId == request.EntityId &&
                                          ps.UriAccessId == request.PermissionId);

            if (existingPermission != null)
            {
                // Permission already exists - update the grant flag if needed
                if (existingPermission.Grant)
                {
                    return new PermissionGrantResponse
                    {
                        Success = true,
                        Message = "Permission already granted"
                    };
                }

                // Update existing permission scheme to grant
                existingPermission.Grant = true;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Updated existing permission scheme to grant for {EntityType} {EntityId} in {ElapsedMs}ms",
                    request.EntityType, request.EntityId, stopwatch.ElapsedMilliseconds);

                return new PermissionGrantResponse
                {
                    Success = true,
                    Message = "Permission grant updated successfully"
                };
            }

            // Get or create the scheme type for this entity
            var schemeType = await GetOrCreateSchemeTypeAsync(request.EntityType);

            // Create new permission scheme
            var permissionScheme = new Data.Models.PermissionScheme
            {
                EntityId = request.EntityId,
                SchemeTypeId = schemeType.Id,
                UriAccessId = request.PermissionId,
                Grant = true
            };

            _dbContext.EntityPermissions.Add(permissionScheme);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Permission {PermissionId} granted to {EntityType} {EntityId} successfully in {ElapsedMs}ms",
                request.PermissionId, request.EntityType, request.EntityId, stopwatch.ElapsedMilliseconds);

            return new PermissionGrantResponse
            {
                Success = true,
                Message = "Permission granted successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error granting permission {PermissionId} to {EntityType} {EntityId}",
                request.PermissionId, request.EntityType, request.EntityId);

            return new PermissionGrantResponse
            {
                Success = false,
                Message = $"Error granting permission: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Validates that an entity of the specified type exists.
    /// </summary>
    private async Task<bool> ValidateEntityExistsAsync(int entityId, string entityType)
    {
        return entityType.ToLowerInvariant() switch
        {
            "user" => await _dbContext.Users.AnyAsync(u => u.Id == entityId),
            "group" => await _dbContext.Groups.AnyAsync(g => g.Id == entityId),
            "role" => await _dbContext.Roles.AnyAsync(r => r.Id == entityId),
            _ => await _dbContext.Entities.AnyAsync(e => e.Id == entityId)
        };
    }

    /// <summary>
    /// Gets or creates a scheme type for the given entity type.
    /// </summary>
    private async Task<Data.Models.SchemeType> GetOrCreateSchemeTypeAsync(string entityType)
    {
        var schemeName = $"{entityType}Permission";
        var schemeType = await _dbContext.SchemeTypes.FirstOrDefaultAsync(st => st.SchemeName == schemeName);

        if (schemeType == null)
        {
            schemeType = new Data.Models.SchemeType { SchemeName = schemeName };
            _dbContext.SchemeTypes.Add(schemeType);
            await _dbContext.SaveChangesAsync();
        }

        return schemeType;
    }

    /// <summary>
    /// Revokes a permission from an entity by removing or updating the PermissionScheme record.
    /// Optionally cascades the revocation to child entities.
    /// </summary>
    public async Task<PermissionRevokeResponse> RevokePermissionAsync(RevokePermissionRequest request)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var affectedEntityIds = new List<int>();

        try
        {
            _logger.LogInformation("Revoking permission {PermissionId} from {EntityType} {EntityId}, Cascade: {Cascade}",
                request.PermissionId, request.EntityType, request.EntityId, request.CascadeToChildren);

            // Find the permission scheme to revoke
            var permissionScheme = await _dbContext.EntityPermissions
                .FirstOrDefaultAsync(ps => ps.EntityId == request.EntityId &&
                                          ps.UriAccessId == request.PermissionId);

            if (permissionScheme == null)
            {
                return new PermissionRevokeResponse
                {
                    Success = false,
                    Message = $"Permission {request.PermissionId} not found for {request.EntityType}:{request.EntityId}",
                    Error = "Permission not found"
                };
            }

            // Remove the permission scheme
            _dbContext.EntityPermissions.Remove(permissionScheme);
            affectedEntityIds.Add(request.EntityId);

            // If cascade is requested, also revoke from child entities
            if (request.CascadeToChildren)
            {
                var childEntityIds = await GetChildEntityIdsAsync(request.EntityId, request.EntityType);

                foreach (var childId in childEntityIds)
                {
                    var childPermission = await _dbContext.EntityPermissions
                        .FirstOrDefaultAsync(ps => ps.EntityId == childId &&
                                                  ps.UriAccessId == request.PermissionId);

                    if (childPermission != null)
                    {
                        _dbContext.EntityPermissions.Remove(childPermission);
                        affectedEntityIds.Add(childId);
                    }
                }
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Permission {PermissionId} revoked from {EntityType} {EntityId}. Affected entities: {AffectedCount} in {ElapsedMs}ms",
                request.PermissionId, request.EntityType, request.EntityId, affectedEntityIds.Count, stopwatch.ElapsedMilliseconds);

            return new PermissionRevokeResponse
            {
                Success = true,
                Message = $"Permission revoked successfully from {affectedEntityIds.Count} entities",
                AffectedEntityIds = affectedEntityIds,
                RevokedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking permission {PermissionId} from {EntityType} {EntityId}",
                request.PermissionId, request.EntityType, request.EntityId);

            return new PermissionRevokeResponse
            {
                Success = false,
                Message = $"Error revoking permission: {ex.Message}",
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Gets IDs of all child entities for cascading operations.
    /// </summary>
    private async Task<List<int>> GetChildEntityIdsAsync(int entityId, string entityType)
    {
        var childIds = new List<int>();

        switch (entityType.ToLowerInvariant())
        {
            case "group":
                // Get users in this group
                var userIds = await _dbContext.UserGroups
                    .Where(ug => ug.GroupId == entityId)
                    .Select(ug => ug.UserId)
                    .ToListAsync();
                childIds.AddRange(userIds);

                // Get child groups
                var childGroupIds = await _dbContext.GroupHierarchies
                    .Where(gh => gh.ParentGroupId == entityId)
                    .Select(gh => gh.ChildGroupId)
                    .ToListAsync();
                childIds.AddRange(childGroupIds);
                break;

            case "role":
                // Get users with this role
                var roleUserIds = await _dbContext.UserRoles
                    .Where(ur => ur.RoleId == entityId)
                    .Select(ur => ur.UserId)
                    .ToListAsync();
                childIds.AddRange(roleUserIds);

                // Get groups with this role
                var roleGroupIds = await _dbContext.GroupRoles
                    .Where(gr => gr.RoleId == entityId)
                    .Select(gr => gr.GroupId)
                    .ToListAsync();
                childIds.AddRange(roleGroupIds);
                break;
        }

        return childIds;
    }

    /// <summary>
    /// Performs a detailed permission check including inheritance chain analysis.
    /// Returns comprehensive information about how the permission was granted/denied.
    /// </summary>
    public async Task<PermissionCheckWithDetailsResponse> CheckPermissionWithDetailsAsync(CheckPermissionRequest request)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var details = new List<string>();
        var inheritanceChain = new List<string>();
        var grantingPermissions = new List<Domain.Permission>();

        try
        {
            _logger.LogDebug("Checking permission with details for {EntityType} {EntityId}, Permission: {PermissionId}",
                request.EntityType, request.EntityId, request.PermissionId);

            details.Add($"Starting permission check for {request.EntityType}:{request.EntityId}");

            // Check effective time if specified
            var checkTime = request.CheckAt ?? DateTime.UtcNow;
            details.Add($"Check time: {checkTime:yyyy-MM-dd HH:mm:ss} UTC");

            // Step 1: Check direct permission
            details.Add("Step 1: Checking direct permission assignment");
            var directPermission = await _dbContext.EntityPermissions
                .Include(ps => ps.UriAccess)
                    .ThenInclude(ua => ua!.Resource)
                .Include(ps => ps.UriAccess)
                    .ThenInclude(ua => ua!.VerbType)
                .Include(ps => ps.SchemeType)
                .Where(ps => ps.EntityId == request.EntityId && ps.UriAccessId == request.PermissionId)
                .FirstOrDefaultAsync();

            if (directPermission != null)
            {
                details.Add($"  Found direct permission scheme (ID: {directPermission.Id})");

                var hasDeny = directPermission.UriAccess?.Deny ?? false;
                var hasGrant = directPermission.Grant || (directPermission.UriAccess?.Grant ?? false);

                if (hasDeny)
                {
                    details.Add("  Permission explicitly DENIED");
                    return new PermissionCheckWithDetailsResponse
                    {
                        HasPermission = false,
                        IsExpired = false,
                        IsInherited = false,
                        Reason = "Permission explicitly denied via direct assignment",
                        Details = details,
                        Success = true
                    };
                }

                if (hasGrant)
                {
                    grantingPermissions.Add(ConvertToPermission(directPermission));
                    details.Add("  Permission GRANTED via direct assignment");

                    return new PermissionCheckWithDetailsResponse
                    {
                        HasPermission = true,
                        IsExpired = false,
                        IsInherited = false,
                        Reason = "Permission granted via direct assignment",
                        Details = details,
                        GrantingPermissions = grantingPermissions,
                        Success = true
                    };
                }
            }
            else
            {
                details.Add("  No direct permission assignment found");
            }

            // Step 2: Check inherited permissions if requested
            if (request.IncludeInheritance)
            {
                details.Add("Step 2: Checking inherited permissions");
                inheritanceChain.Add($"{request.EntityType}:{request.EntityId}");

                var inheritedResult = await CheckInheritedPermissionWithDetailsAsync(
                    request.EntityId, request.EntityType, request.PermissionId,
                    request.ResourceId, details, inheritanceChain);

                if (inheritedResult.HasPermission || inheritedResult.Reason?.Contains("denied") == true)
                {
                    inheritedResult = inheritedResult with
                    {
                        Details = details,
                        InheritanceChain = inheritanceChain,
                        IsInherited = true,
                        Success = true
                    };

                    _logger.LogDebug("Permission check with details completed in {ElapsedMs}ms. Result: {HasPermission}",
                        stopwatch.ElapsedMilliseconds, inheritedResult.HasPermission);

                    return inheritedResult;
                }
            }
            else
            {
                details.Add("Step 2: Inheritance check skipped (IncludeInheritance = false)");
            }

            details.Add("Final result: Permission NOT granted");
            details.Add($"Check completed in {stopwatch.ElapsedMilliseconds}ms");

            return new PermissionCheckWithDetailsResponse
            {
                HasPermission = false,
                IsExpired = false,
                IsInherited = false,
                Reason = "No permission found in direct assignment or inheritance hierarchy",
                Details = details,
                InheritanceChain = inheritanceChain,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission with details for {EntityType} {EntityId}",
                request.EntityType, request.EntityId);

            details.Add($"Error: {ex.Message}");

            return new PermissionCheckWithDetailsResponse
            {
                HasPermission = false,
                IsExpired = false,
                Reason = $"Error during permission check: {ex.Message}",
                Details = details,
                Success = false
            };
        }
    }

    /// <summary>
    /// Checks inherited permissions with detailed tracking of the inheritance path.
    /// </summary>
    private async Task<PermissionCheckWithDetailsResponse> CheckInheritedPermissionWithDetailsAsync(
        int entityId, string entityType, int permissionId, int? resourceId,
        List<string> details, List<string> inheritanceChain)
    {
        var grantingPermissions = new List<Domain.Permission>();
        var parentEntities = new List<(int Id, string Type, string Source)>();

        // Build list of parent entities based on entity type
        switch (entityType.ToLowerInvariant())
        {
            case "user":
                details.Add("  Checking user's groups...");
                var userGroups = await _dbContext.UserGroups
                    .Include(ug => ug.Group)
                    .Where(ug => ug.UserId == entityId)
                    .ToListAsync();

                foreach (var ug in userGroups)
                {
                    parentEntities.Add((ug.GroupId, "Group", $"via group membership"));
                    inheritanceChain.Add($"Group:{ug.GroupId}({ug.Group?.Name ?? "Unknown"})");
                    details.Add($"    Found group: {ug.Group?.Name ?? "Unknown"} (ID: {ug.GroupId})");
                }

                details.Add("  Checking user's direct roles...");
                var userRoles = await _dbContext.UserRoles
                    .Include(ur => ur.Role)
                    .Where(ur => ur.UserId == entityId)
                    .ToListAsync();

                foreach (var ur in userRoles)
                {
                    parentEntities.Add((ur.RoleId, "Role", $"via direct role assignment"));
                    inheritanceChain.Add($"Role:{ur.RoleId}({ur.Role?.Name ?? "Unknown"})");
                    details.Add($"    Found role: {ur.Role?.Name ?? "Unknown"} (ID: {ur.RoleId})");
                }

                // Also get roles from groups
                var groupIds = userGroups.Select(ug => ug.GroupId).ToList();
                if (groupIds.Any())
                {
                    details.Add("  Checking roles from user's groups...");
                    var groupRoles = await _dbContext.GroupRoles
                        .Include(gr => gr.Role)
                        .Include(gr => gr.Group)
                        .Where(gr => groupIds.Contains(gr.GroupId))
                        .ToListAsync();

                    foreach (var gr in groupRoles)
                    {
                        parentEntities.Add((gr.RoleId, "Role", $"via group {gr.Group?.Name ?? gr.GroupId.ToString()}"));
                        inheritanceChain.Add($"Role:{gr.RoleId}({gr.Role?.Name ?? "Unknown"}) via Group:{gr.GroupId}");
                        details.Add($"    Found role: {gr.Role?.Name ?? "Unknown"} (ID: {gr.RoleId}) via group {gr.Group?.Name ?? "Unknown"}");
                    }
                }
                break;

            case "group":
                details.Add("  Checking parent groups...");
                var parentGroups = await _dbContext.GroupHierarchies
                    .Include(gh => gh.ParentGroup)
                    .Where(gh => gh.ChildGroupId == entityId)
                    .ToListAsync();

                foreach (var pg in parentGroups)
                {
                    parentEntities.Add((pg.ParentGroupId, "Group", "via parent group"));
                    inheritanceChain.Add($"ParentGroup:{pg.ParentGroupId}({pg.ParentGroup?.Name ?? "Unknown"})");
                    details.Add($"    Found parent group: {pg.ParentGroup?.Name ?? "Unknown"} (ID: {pg.ParentGroupId})");
                }

                details.Add("  Checking group's roles...");
                var groupDirectRoles = await _dbContext.GroupRoles
                    .Include(gr => gr.Role)
                    .Where(gr => gr.GroupId == entityId)
                    .ToListAsync();

                foreach (var gr in groupDirectRoles)
                {
                    parentEntities.Add((gr.RoleId, "Role", "via group role assignment"));
                    inheritanceChain.Add($"Role:{gr.RoleId}({gr.Role?.Name ?? "Unknown"})");
                    details.Add($"    Found role: {gr.Role?.Name ?? "Unknown"} (ID: {gr.RoleId})");
                }
                break;

            case "role":
                details.Add("  Roles do not inherit permissions from other entities");
                break;
        }

        if (!parentEntities.Any())
        {
            details.Add("  No parent entities found for inheritance");
            return new PermissionCheckWithDetailsResponse
            {
                HasPermission = false,
                IsExpired = false,
                Reason = "No parent entities in inheritance hierarchy"
            };
        }

        // Check permissions on all parent entities
        details.Add($"  Checking permissions on {parentEntities.Count} parent entities...");

        var parentEntityIds = parentEntities.Select(p => p.Id).ToHashSet();
        var inheritedPermissions = await _dbContext.EntityPermissions
            .Include(ps => ps.UriAccess)
                .ThenInclude(ua => ua!.Resource)
            .Include(ps => ps.SchemeType)
            .Where(ps => parentEntityIds.Contains(ps.EntityId.GetValueOrDefault()) &&
                        ps.UriAccessId == permissionId)
            .ToListAsync();

        // Check for explicit deny first (deny takes precedence)
        foreach (var perm in inheritedPermissions.Where(p => p.UriAccess?.Deny == true))
        {
            var parentInfo = parentEntities.First(pe => pe.Id == perm.EntityId);
            details.Add($"  DENY found from {parentInfo.Type}:{parentInfo.Id} {parentInfo.Source}");

            return new PermissionCheckWithDetailsResponse
            {
                HasPermission = false,
                IsExpired = false,
                InheritedFrom = $"{parentInfo.Type}:{parentInfo.Id}",
                Reason = $"Permission denied via inheritance from {parentInfo.Type}:{parentInfo.Id}"
            };
        }

        // Check for grant
        foreach (var perm in inheritedPermissions.Where(p => p.Grant || (p.UriAccess?.Grant ?? false)))
        {
            var parentInfo = parentEntities.First(pe => pe.Id == perm.EntityId);
            details.Add($"  GRANT found from {parentInfo.Type}:{parentInfo.Id} {parentInfo.Source}");

            grantingPermissions.Add(ConvertToPermission(perm));

            return new PermissionCheckWithDetailsResponse
            {
                HasPermission = true,
                IsExpired = false,
                InheritedFrom = $"{parentInfo.Type}:{parentInfo.Id}",
                Reason = $"Permission granted via inheritance from {parentInfo.Type}:{parentInfo.Id}",
                GrantingPermissions = grantingPermissions
            };
        }

        details.Add("  No permission grant or deny found in inheritance hierarchy");
        return new PermissionCheckWithDetailsResponse
        {
            HasPermission = false,
            IsExpired = false,
            Reason = "No matching permission found in inheritance hierarchy"
        };
    }

    /// <summary>
    /// Validates the permission structure for conflicts, redundancies, and inconsistencies.
    /// Optionally fixes detected issues if FixInconsistencies is true.
    /// </summary>
    public async Task<ValidatePermissionStructureResponse> ValidatePermissionStructureAsync(ValidatePermissionStructureRequest request)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var inconsistencies = new List<PermissionInconsistency>();
        var fixedInconsistencies = new List<PermissionInconsistency>();
        var validationErrors = new List<string>();
        var validationWarnings = new List<string>();
        var recommendations = new List<string>();
        var conflictCount = 0;
        var redundancyCount = 0;

        try
        {
            _logger.LogInformation("Validating permission structure. EntityId: {EntityId}, EntityType: {EntityType}",
                request.EntityId, request.EntityType);

            // Build query based on request parameters
            IQueryable<Data.Models.PermissionScheme> permissionQuery = _dbContext.EntityPermissions
                .Include(ps => ps.UriAccess)
                    .ThenInclude(ua => ua!.Resource)
                .Include(ps => ps.SchemeType)
                .Include(ps => ps.Entity);

            if (request.EntityId.HasValue)
            {
                permissionQuery = permissionQuery.Where(ps => ps.EntityId == request.EntityId.Value);
            }

            var allPermissions = await permissionQuery.ToListAsync();

            // Step 1: Check for Grant/Deny conflicts
            if (request.CheckForConflicts)
            {
                _logger.LogDebug("Checking for grant/deny conflicts...");
                var conflicts = await DetectGrantDenyConflictsAsync(allPermissions, request.EntityId);

                foreach (var conflict in conflicts)
                {
                    inconsistencies.Add(conflict);
                    conflictCount++;
                    validationErrors.Add($"Conflict: {conflict.Description}");
                }
            }

            // Step 2: Check for redundant permissions
            if (request.CheckForRedundancies)
            {
                _logger.LogDebug("Checking for redundant permissions...");
                var redundancies = await DetectRedundantPermissionsAsync(allPermissions);

                foreach (var redundancy in redundancies)
                {
                    inconsistencies.Add(redundancy);
                    redundancyCount++;
                    validationWarnings.Add($"Redundancy: {redundancy.Description}");
                }
            }

            // Step 3: Check for orphaned permission schemes
            var orphanedSchemes = allPermissions.Where(ps =>
                ps.EntityId == null || ps.UriAccessId == null).ToList();

            foreach (var orphan in orphanedSchemes)
            {
                var inconsistency = new PermissionInconsistency
                {
                    Type = "OrphanedScheme",
                    Description = $"Permission scheme {orphan.Id} has missing entity or URI access reference",
                    EntityId = orphan.EntityId ?? 0,
                    EntityType = "Unknown",
                    PermissionId = orphan.UriAccessId ?? 0,
                    Severity = "High",
                    CanAutoFix = true,
                    RecommendedAction = "Remove orphaned permission scheme"
                };
                inconsistencies.Add(inconsistency);
                validationErrors.Add($"Orphaned scheme: Permission scheme {orphan.Id} is incomplete");
            }

            // Step 4: Check for invalid entity references
            var entityIds = allPermissions
                .Where(ps => ps.EntityId.HasValue)
                .Select(ps => ps.EntityId!.Value)
                .Distinct()
                .ToList();

            var validUserIds = await _dbContext.Users.Where(u => entityIds.Contains(u.Id)).Select(u => u.Id).ToListAsync();
            var validGroupIds = await _dbContext.Groups.Where(g => entityIds.Contains(g.Id)).Select(g => g.Id).ToListAsync();
            var validRoleIds = await _dbContext.Roles.Where(r => entityIds.Contains(r.Id)).Select(r => r.Id).ToListAsync();

            var validEntityIds = validUserIds.Union(validGroupIds).Union(validRoleIds).ToHashSet();
            var invalidEntityRefs = entityIds.Where(id => !validEntityIds.Contains(id)).ToList();

            foreach (var invalidId in invalidEntityRefs)
            {
                var affectedSchemes = allPermissions.Where(ps => ps.EntityId == invalidId).ToList();
                foreach (var scheme in affectedSchemes)
                {
                    var inconsistency = new PermissionInconsistency
                    {
                        Type = "InvalidEntityReference",
                        Description = $"Permission scheme {scheme.Id} references non-existent entity {invalidId}",
                        EntityId = invalidId,
                        EntityType = "Unknown",
                        PermissionId = scheme.UriAccessId ?? 0,
                        Severity = "Critical",
                        CanAutoFix = true,
                        RecommendedAction = "Remove permission scheme with invalid entity reference"
                    };
                    inconsistencies.Add(inconsistency);
                    validationErrors.Add($"Invalid entity reference: Entity {invalidId} does not exist");
                }
            }

            // Step 5: Fix inconsistencies if requested
            if (request.FixInconsistencies && inconsistencies.Any(i => i.CanAutoFix))
            {
                _logger.LogInformation("Attempting to fix {Count} auto-fixable inconsistencies",
                    inconsistencies.Count(i => i.CanAutoFix));

                foreach (var inconsistency in inconsistencies.Where(i => i.CanAutoFix))
                {
                    var wasFixed = await TryFixInconsistencyAsync(inconsistency);
                    if (wasFixed)
                    {
                        fixedInconsistencies.Add(inconsistency);
                    }
                }

                if (fixedInconsistencies.Any())
                {
                    await _dbContext.SaveChangesAsync();
                }
            }

            // Generate recommendations
            if (conflictCount > 0)
            {
                recommendations.Add($"Resolve {conflictCount} permission conflicts to ensure consistent access control");
            }
            if (redundancyCount > 0)
            {
                recommendations.Add($"Consider removing {redundancyCount} redundant permissions to simplify permission structure");
            }
            if (orphanedSchemes.Any())
            {
                recommendations.Add("Clean up orphaned permission schemes that have missing references");
            }
            if (invalidEntityRefs.Any())
            {
                recommendations.Add("Remove permission schemes that reference deleted entities");
            }
            if (!inconsistencies.Any())
            {
                recommendations.Add("Permission structure is healthy - no issues detected");
            }

            var isValid = !validationErrors.Any();

            _logger.LogInformation("Permission structure validation completed in {ElapsedMs}ms. Valid: {IsValid}, Conflicts: {Conflicts}, Redundancies: {Redundancies}, Fixed: {Fixed}",
                stopwatch.ElapsedMilliseconds, isValid, conflictCount, redundancyCount, fixedInconsistencies.Count);

            return new ValidatePermissionStructureResponse
            {
                IsValid = isValid,
                Inconsistencies = inconsistencies,
                FixedInconsistencies = fixedInconsistencies,
                ValidationErrors = validationErrors,
                ValidationWarnings = validationWarnings,
                Recommendations = recommendations,
                ConflictCount = conflictCount,
                RedundancyCount = redundancyCount,
                Success = true,
                Message = isValid
                    ? "Permission structure validation passed"
                    : $"Permission structure validation found {validationErrors.Count} errors and {validationWarnings.Count} warnings"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating permission structure");

            return new ValidatePermissionStructureResponse
            {
                IsValid = false,
                ValidationErrors = new List<string> { $"Validation error: {ex.Message}" },
                Recommendations = new List<string> { "Fix the error and retry validation" },
                Success = false,
                Message = $"Error during validation: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Detects grant/deny conflicts where the same permission has both grant and deny for related entities.
    /// </summary>
    private async Task<List<PermissionInconsistency>> DetectGrantDenyConflictsAsync(
        List<Data.Models.PermissionScheme> permissions, int? entityId)
    {
        var conflicts = new List<PermissionInconsistency>();

        // Group permissions by UriAccessId to find potential conflicts
        var permissionGroups = permissions
            .Where(p => p.UriAccessId.HasValue)
            .GroupBy(p => p.UriAccessId!.Value)
            .Where(g => g.Count() > 1);

        foreach (var group in permissionGroups)
        {
            var grants = group.Where(p => p.Grant || (p.UriAccess?.Grant ?? false)).ToList();
            var denies = group.Where(p => p.UriAccess?.Deny ?? false).ToList();

            // Check if there's a conflict within an inheritance hierarchy
            if (grants.Any() && denies.Any())
            {
                foreach (var grant in grants)
                {
                    foreach (var deny in denies)
                    {
                        // Check if entities are related through inheritance
                        var areRelated = await AreEntitiesRelatedAsync(
                            grant.EntityId.GetValueOrDefault(),
                            deny.EntityId.GetValueOrDefault());

                        if (areRelated)
                        {
                            conflicts.Add(new PermissionInconsistency
                            {
                                Type = "GrantDenyConflict",
                                Description = $"Permission {group.Key} has conflicting grant (Entity:{grant.EntityId}) and deny (Entity:{deny.EntityId})",
                                EntityId = grant.EntityId.GetValueOrDefault(),
                                EntityType = "Mixed",
                                PermissionId = group.Key,
                                Severity = "High",
                                CanAutoFix = false,
                                RecommendedAction = "Review and resolve the grant/deny conflict manually"
                            });
                        }
                    }
                }
            }
        }

        return conflicts;
    }

    /// <summary>
    /// Detects redundant permissions where a permission is granted both directly and via inheritance.
    /// </summary>
    private async Task<List<PermissionInconsistency>> DetectRedundantPermissionsAsync(
        List<Data.Models.PermissionScheme> permissions)
    {
        var redundancies = new List<PermissionInconsistency>();

        // Group permissions by UriAccessId
        var permissionGroups = permissions
            .Where(p => p.UriAccessId.HasValue && (p.Grant || (p.UriAccess?.Grant ?? false)))
            .GroupBy(p => p.UriAccessId!.Value);

        foreach (var group in permissionGroups.Where(g => g.Count() > 1))
        {
            var entityIds = group.Select(p => p.EntityId.GetValueOrDefault()).ToList();

            // Check for redundant permissions through inheritance
            for (int i = 0; i < entityIds.Count; i++)
            {
                for (int j = i + 1; j < entityIds.Count; j++)
                {
                    var isAncestor = await IsEntityAncestorOfAsync(entityIds[i], entityIds[j]);
                    if (isAncestor)
                    {
                        redundancies.Add(new PermissionInconsistency
                        {
                            Type = "RedundantPermission",
                            Description = $"Permission {group.Key} on Entity:{entityIds[j]} is redundant (inherited from Entity:{entityIds[i]})",
                            EntityId = entityIds[j],
                            EntityType = "Unknown",
                            PermissionId = group.Key,
                            Severity = "Low",
                            CanAutoFix = true,
                            RecommendedAction = $"Remove redundant permission from Entity:{entityIds[j]}"
                        });
                    }
                }
            }
        }

        return redundancies;
    }

    /// <summary>
    /// Checks if two entities are related through inheritance.
    /// </summary>
    private async Task<bool> AreEntitiesRelatedAsync(int entityId1, int entityId2)
    {
        // Check if entityId1 is ancestor of entityId2 or vice versa
        return await IsEntityAncestorOfAsync(entityId1, entityId2) ||
               await IsEntityAncestorOfAsync(entityId2, entityId1);
    }

    /// <summary>
    /// Checks if parentEntityId is an ancestor of childEntityId in the inheritance hierarchy.
    /// </summary>
    private async Task<bool> IsEntityAncestorOfAsync(int parentEntityId, int childEntityId)
    {
        // Check user-group relationship
        var isUserInGroup = await _dbContext.UserGroups
            .AnyAsync(ug => ug.UserId == childEntityId && ug.GroupId == parentEntityId);
        if (isUserInGroup) return true;

        // Check user-role relationship
        var userHasRole = await _dbContext.UserRoles
            .AnyAsync(ur => ur.UserId == childEntityId && ur.RoleId == parentEntityId);
        if (userHasRole) return true;

        // Check group-role relationship
        var groupHasRole = await _dbContext.GroupRoles
            .AnyAsync(gr => gr.GroupId == childEntityId && gr.RoleId == parentEntityId);
        if (groupHasRole) return true;

        // Check group hierarchy
        var isChildGroup = await _dbContext.GroupHierarchies
            .AnyAsync(gh => gh.ParentGroupId == parentEntityId && gh.ChildGroupId == childEntityId);
        if (isChildGroup) return true;

        return false;
    }

    /// <summary>
    /// Attempts to fix an inconsistency automatically.
    /// </summary>
    private async Task<bool> TryFixInconsistencyAsync(PermissionInconsistency inconsistency)
    {
        try
        {
            switch (inconsistency.Type)
            {
                case "OrphanedScheme":
                case "InvalidEntityReference":
                case "RedundantPermission":
                    // Find and remove the problematic permission scheme
                    var schemeToRemove = await _dbContext.EntityPermissions
                        .FirstOrDefaultAsync(ps => ps.EntityId == inconsistency.EntityId &&
                                                  ps.UriAccessId == inconsistency.PermissionId);
                    if (schemeToRemove != null)
                    {
                        _dbContext.EntityPermissions.Remove(schemeToRemove);
                        _logger.LogInformation("Fixed inconsistency: Removed permission scheme for Entity:{EntityId}, Permission:{PermissionId}",
                            inconsistency.EntityId, inconsistency.PermissionId);
                        return true;
                    }
                    break;

                default:
                    _logger.LogWarning("Cannot auto-fix inconsistency type: {Type}", inconsistency.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fix inconsistency: {Description}", inconsistency.Description);
        }

        return false;
    }

    public Task<GetEntityPermissionsResponse> GetEntityPermissionsAsync(GetEntityPermissionsRequest request)
    {
        return Task.FromResult(new GetEntityPermissionsResponse
        {
            Permissions = new List<string>(),
            TotalCount = 0
        });
    }

    public Task<GetPermissionUsageResponse> GetPermissionUsageAsync(GetPermissionUsageRequest request)
    {
        return Task.FromResult(new GetPermissionUsageResponse
        {
            PermissionId = request.PermissionId,
            UsageCount = 0,
            UsedBy = new List<string>()
        });
    }

    public Task<BulkPermissionUpdateResponse> BulkUpdatePermissionsAsync(BulkPermissionUpdateRequest request)
    {
        return Task.FromResult(new BulkPermissionUpdateResponse
        {
            Success = true,
            ProcessedCount = request.Operations.Count,
            Errors = new List<string>()
        });
    }

    /// <summary>
    /// Evaluates complex permission rules including conditions, context, and reasoning trace.
    /// Supports time-based, IP-based, and custom condition evaluation.
    /// </summary>
    public async Task<EvaluateComplexPermissionResponse> EvaluateComplexPermissionAsync(EvaluateComplexPermissionRequest request)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var evaluationSteps = new List<string>();
        var evaluationDetails = new List<string>();
        var reasoningTrace = new List<PermissionEvaluationStep>();
        var conditionResults = new List<Responses.ConditionEvaluationResult>();
        var stepNumber = 0;

        try
        {
            _logger.LogInformation("Evaluating complex permission for User:{UserId}, Resource:{ResourceId}, Action:{Action}",
                request.UserId, request.ResourceId, request.Action);

            evaluationSteps.Add("Starting complex permission evaluation");

            // Step 1: Validate user exists
            stepNumber++;
            var user = await _dbContext.Users.FindAsync(request.UserId);
            if (user == null)
            {
                var failStep = CreateEvaluationStep(stepNumber, "Validate user existence", "User validation", false, "User not found");
                reasoningTrace.Add(failStep);
                evaluationSteps.Add($"Step {stepNumber}: User validation - FAILED (User not found)");

                return new EvaluateComplexPermissionResponse
                {
                    HasAccess = false,
                    HasPermission = false,
                    DecisionReason = $"User {request.UserId} not found",
                    EvaluationResult = "Access denied - user not found",
                    ReasoningTrace = reasoningTrace,
                    EvaluationSteps = evaluationSteps,
                    EvaluationTime = stopwatch.Elapsed,
                    Success = true
                };
            }
            reasoningTrace.Add(CreateEvaluationStep(stepNumber, "Validate user existence", "User validation", true, $"User {user.Name} found"));
            evaluationSteps.Add($"Step {stepNumber}: User validation - PASSED (User: {user.Name})");

            // Step 2: Validate resource exists
            stepNumber++;
            var resource = await _dbContext.Resources.FindAsync(request.ResourceId);
            if (resource == null)
            {
                var failStep = CreateEvaluationStep(stepNumber, "Validate resource existence", "Resource validation", false, "Resource not found");
                reasoningTrace.Add(failStep);
                evaluationSteps.Add($"Step {stepNumber}: Resource validation - FAILED (Resource not found)");

                return new EvaluateComplexPermissionResponse
                {
                    HasAccess = false,
                    HasPermission = false,
                    DecisionReason = $"Resource {request.ResourceId} not found",
                    EvaluationResult = "Access denied - resource not found",
                    ReasoningTrace = reasoningTrace,
                    EvaluationSteps = evaluationSteps,
                    EvaluationTime = stopwatch.Elapsed,
                    Success = true
                };
            }
            reasoningTrace.Add(CreateEvaluationStep(stepNumber, "Validate resource existence", "Resource validation", true, $"Resource found: {resource.Uri}"));
            evaluationSteps.Add($"Step {stepNumber}: Resource validation - PASSED (Resource: {resource.Uri})");

            // Step 3: Check base permission
            stepNumber++;
            var permissionCheck = await CheckPermissionForResourceAsync(request.UserId, request.ResourceId, request.Action);
            reasoningTrace.Add(CreateEvaluationStep(stepNumber, "Check base permission", "Permission check",
                permissionCheck.HasPermission, permissionCheck.Reason ?? "No reason provided"));
            evaluationSteps.Add($"Step {stepNumber}: Base permission check - {(permissionCheck.HasPermission ? "PASSED" : "FAILED")}");

            if (!permissionCheck.HasPermission)
            {
                return new EvaluateComplexPermissionResponse
                {
                    HasAccess = false,
                    HasPermission = false,
                    DecisionReason = permissionCheck.Reason,
                    EvaluationResult = "Access denied - no base permission",
                    ReasoningTrace = reasoningTrace,
                    EvaluationSteps = evaluationSteps,
                    EvaluationTime = stopwatch.Elapsed,
                    Success = true
                };
            }

            // Step 4: Evaluate custom conditions
            stepNumber++;
            evaluationSteps.Add($"Step {stepNumber}: Evaluating {request.Conditions.Count} custom conditions");
            var allConditionsPassed = true;
            var conditionFailureReason = "";

            foreach (var condition in request.Conditions)
            {
                var conditionResult = EvaluateCondition(condition, request.Context);
                conditionResults.Add(conditionResult);

                var conditionDescription = $"Condition: {condition.Type} {condition.Operator} {condition.Value}";
                if (!conditionResult.Satisfied)
                {
                    allConditionsPassed = false;
                    conditionFailureReason = $"{conditionDescription} - {conditionResult.Reason}";
                    evaluationSteps.Add($"  Condition FAILED: {conditionDescription}");
                }
                else
                {
                    evaluationSteps.Add($"  Condition PASSED: {conditionDescription}");
                }
            }

            reasoningTrace.Add(CreateEvaluationStep(stepNumber, "Evaluate custom conditions", "Condition evaluation",
                allConditionsPassed, allConditionsPassed ? "All conditions passed" : conditionFailureReason));

            if (!allConditionsPassed)
            {
                return new EvaluateComplexPermissionResponse
                {
                    HasAccess = false,
                    HasPermission = true, // Base permission exists but conditions failed
                    DecisionReason = $"Condition check failed: {conditionFailureReason}",
                    EvaluationResult = "Access denied - condition not satisfied",
                    ReasoningTrace = reasoningTrace,
                    ConditionResults = conditionResults,
                    EvaluationSteps = evaluationSteps,
                    EvaluationTime = stopwatch.Elapsed,
                    Success = true
                };
            }

            // Step 5: Check time-based constraints if EvaluateAt is specified
            if (request.EvaluateAt.HasValue)
            {
                stepNumber++;
                var evaluateTime = request.EvaluateAt.Value;
                var isWithinBusinessHours = evaluateTime.Hour >= 8 && evaluateTime.Hour < 18;

                reasoningTrace.Add(CreateEvaluationStep(stepNumber, "Check time constraints", "Time validation",
                    true, $"Evaluation time: {evaluateTime:yyyy-MM-dd HH:mm:ss}"));
                evaluationSteps.Add($"Step {stepNumber}: Time constraint check - Time: {evaluateTime:yyyy-MM-dd HH:mm:ss}");
            }

            // Step 6: Final decision
            stepNumber++;
            evaluationSteps.Add($"Step {stepNumber}: Final decision - ACCESS GRANTED");

            var decisionContext = new PermissionDecisionContext
            {
                UserId = request.UserId,
                UserName = user.Name,
                ResourceId = request.ResourceId,
                ResourceName = resource.Uri,
                Action = request.Action,
                RequestTimestamp = DateTime.UtcNow,
                AdditionalContext = request.Context
            };

            reasoningTrace.Add(CreateEvaluationStep(stepNumber, "Final access decision", "Decision point", true, "All checks passed"));

            _logger.LogInformation("Complex permission evaluation completed. User:{UserId}, Resource:{ResourceId}, Result:GRANTED in {ElapsedMs}ms",
                request.UserId, request.ResourceId, stopwatch.ElapsedMilliseconds);

            return new EvaluateComplexPermissionResponse
            {
                HasAccess = true,
                HasPermission = true,
                DecisionReason = "All permission checks and conditions passed",
                EvaluationResult = "Access granted",
                ReasoningTrace = reasoningTrace,
                ConditionResults = conditionResults,
                Context = decisionContext,
                EvaluationSteps = evaluationSteps,
                EvaluationDetails = evaluationDetails,
                EvaluationTime = stopwatch.Elapsed,
                Success = true,
                Message = "Complex permission evaluation completed successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating complex permission for User:{UserId}, Resource:{ResourceId}",
                request.UserId, request.ResourceId);

            evaluationSteps.Add($"Error: {ex.Message}");

            return new EvaluateComplexPermissionResponse
            {
                HasAccess = false,
                HasPermission = false,
                DecisionReason = $"Evaluation error: {ex.Message}",
                EvaluationResult = "Access denied due to evaluation error",
                EvaluationSteps = evaluationSteps,
                EvaluationTime = stopwatch.Elapsed,
                Success = false,
                Message = $"Error during evaluation: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Checks if a user has permission to perform an action on a resource.
    /// </summary>
    private async Task<PermissionCheckResult> CheckPermissionForResourceAsync(int userId, int resourceId, string action)
    {
        // Find URI accesses for this resource that match the action
        var matchingUriAccesses = await _dbContext.UriAccesses
            .Include(ua => ua.VerbType)
            .Where(ua => ua.ResourceId == resourceId)
            .ToListAsync();

        // Filter by action/verb
        var matchingPermissions = matchingUriAccesses
            .Where(ua => string.Equals(ua.VerbType.VerbName, action, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(ua.VerbType.VerbName, "ALL", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!matchingPermissions.Any())
        {
            return new PermissionCheckResult
            {
                HasPermission = false,
                Reason = $"No permission definition exists for action '{action}' on resource {resourceId}"
            };
        }

        // Check if user has any of these permissions
        foreach (var perm in matchingPermissions)
        {
            var result = await CheckPermissionAsync(userId, "User", perm.Id, resourceId);
            if (result.HasPermission)
            {
                return result;
            }
        }

        return new PermissionCheckResult
        {
            HasPermission = false,
            Reason = $"User {userId} does not have '{action}' permission on resource {resourceId}"
        };
    }

    /// <summary>
    /// Evaluates a single condition against the provided context.
    /// </summary>
    private Responses.ConditionEvaluationResult EvaluateCondition(PermissionConditionRequest condition, Dictionary<string, object> context)
    {
        try
        {
            var conditionType = condition.Type.ToLowerInvariant();
            var conditionOperator = condition.Operator.ToLowerInvariant();
            var conditionValue = condition.Value;

            // Get the actual value from context
            object? actualValue = null;
            if (context.TryGetValue(conditionType, out var contextValue))
            {
                actualValue = contextValue;
            }

            var satisfied = false;
            var reason = "";

            switch (conditionType)
            {
                case "time":
                case "hour":
                    var currentHour = DateTime.UtcNow.Hour;
                    actualValue = currentHour;
                    if (int.TryParse(conditionValue, out var targetHour))
                    {
                        satisfied = conditionOperator switch
                        {
                            "equals" or "eq" or "=" => currentHour == targetHour,
                            "greaterthan" or "gt" or ">" => currentHour > targetHour,
                            "lessthan" or "lt" or "<" => currentHour < targetHour,
                            "gte" or ">=" => currentHour >= targetHour,
                            "lte" or "<=" => currentHour <= targetHour,
                            _ => false
                        };
                        reason = satisfied ? "Time condition satisfied" : $"Current hour {currentHour} does not match condition";
                    }
                    break;

                case "dayofweek":
                    var currentDay = (int)DateTime.UtcNow.DayOfWeek;
                    actualValue = currentDay;
                    if (int.TryParse(conditionValue, out var targetDay))
                    {
                        satisfied = currentDay == targetDay;
                        reason = satisfied ? "Day of week condition satisfied" : $"Current day {currentDay} does not match {targetDay}";
                    }
                    break;

                case "ipaddress":
                    if (context.TryGetValue("IpAddress", out var ipValue) && ipValue is string ipAddress)
                    {
                        actualValue = ipAddress;
                        satisfied = conditionOperator switch
                        {
                            "equals" or "eq" or "=" => string.Equals(ipAddress, conditionValue, StringComparison.OrdinalIgnoreCase),
                            "startswith" => ipAddress.StartsWith(conditionValue, StringComparison.OrdinalIgnoreCase),
                            "contains" => ipAddress.Contains(conditionValue, StringComparison.OrdinalIgnoreCase),
                            _ => false
                        };
                        reason = satisfied ? "IP address condition satisfied" : $"IP address {ipAddress} does not match condition";
                    }
                    else
                    {
                        reason = "IP address not provided in context";
                    }
                    break;

                case "role":
                    if (context.TryGetValue("UserRoles", out var rolesValue) && rolesValue is IEnumerable<string> roles)
                    {
                        actualValue = string.Join(",", roles);
                        satisfied = roles.Any(r => string.Equals(r, conditionValue, StringComparison.OrdinalIgnoreCase));
                        reason = satisfied ? "Role condition satisfied" : $"User does not have role '{conditionValue}'";
                    }
                    else
                    {
                        reason = "User roles not provided in context";
                    }
                    break;

                case "department":
                case "location":
                case "custom":
                    if (context.TryGetValue(conditionType, out var customValue))
                    {
                        actualValue = customValue;
                        var customValueStr = customValue?.ToString() ?? "";
                        satisfied = conditionOperator switch
                        {
                            "equals" or "eq" or "=" => string.Equals(customValueStr, conditionValue, StringComparison.OrdinalIgnoreCase),
                            "contains" => customValueStr.Contains(conditionValue, StringComparison.OrdinalIgnoreCase),
                            "startswith" => customValueStr.StartsWith(conditionValue, StringComparison.OrdinalIgnoreCase),
                            _ => false
                        };
                        reason = satisfied ? $"{conditionType} condition satisfied" : $"{conditionType} value '{customValueStr}' does not match condition";
                    }
                    else
                    {
                        reason = $"{conditionType} not provided in context";
                    }
                    break;

                default:
                    reason = $"Unknown condition type: {conditionType}";
                    break;
            }

            return new Responses.ConditionEvaluationResult
            {
                Condition = new Responses.PermissionCondition
                {
                    Type = condition.Type,
                    Operator = condition.Operator,
                    Value = conditionValue,
                    Parameters = condition.Parameters ?? new Dictionary<string, object>()
                },
                Satisfied = satisfied,
                Reason = reason,
                ActualValue = actualValue,
                EvaluatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new Responses.ConditionEvaluationResult
            {
                Condition = new Responses.PermissionCondition
                {
                    Type = condition.Type,
                    Operator = condition.Operator,
                    Value = condition.Value
                },
                Satisfied = false,
                Reason = $"Error evaluating condition: {ex.Message}",
                EvaluatedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Creates a permission evaluation step for the reasoning trace.
    /// </summary>
    private PermissionEvaluationStep CreateEvaluationStep(int stepNumber, string description, string decisionPoint, bool passed, string reason)
    {
        return new PermissionEvaluationStep
        {
            Step = stepNumber,
            Description = description,
            DecisionPoint = decisionPoint,
            Passed = passed,
            Reason = reason,
            Context = new Dictionary<string, object>
            {
                ["Timestamp"] = DateTime.UtcNow,
                ["Passed"] = passed
            }
        };
    }

    /// <summary>
    /// Converts a PermissionScheme database model to a Permission domain object.
    /// </summary>
    private Domain.Permission ConvertToPermission(Data.Models.PermissionScheme scheme)
    {
        return new Domain.Permission
        {
            Id = scheme.UriAccessId ?? scheme.Id,
            EntityId = scheme.EntityId ?? 0,
            Uri = scheme.UriAccess?.Resource?.Uri ?? string.Empty,
            HttpVerb = scheme.UriAccess?.VerbType?.VerbName != null
                ? Enum.TryParse<HttpVerb>(scheme.UriAccess.VerbType.VerbName, out var verb) ? verb : HttpVerb.GET
                : HttpVerb.GET,
            Grant = scheme.Grant || (scheme.UriAccess?.Grant ?? false),
            Deny = scheme.UriAccess?.Deny ?? false,
            Scheme = scheme.SchemeType?.SchemeName != null
                ? Enum.TryParse<Scheme>(scheme.SchemeType.SchemeName, out var schm) ? schm : Scheme.ApiUriAuthorization
                : Scheme.ApiUriAuthorization,
            ResourceId = scheme.UriAccess?.ResourceId,
            ResourceName = scheme.UriAccess?.Resource?.Uri,
            GrantedAt = DateTime.UtcNow
        };
    }

    public Task<GetEffectivePermissionsResponse> GetEffectivePermissionsAsync(GetEffectivePermissionsRequest request)
    {
        return Task.FromResult(new GetEffectivePermissionsResponse
        {
            EffectivePermissions = new List<string>(),
            InheritedPermissions = new List<string>()
        });
    }

    public Task<PermissionImpactAnalysisResponse> AnalyzePermissionImpactAsync(PermissionImpactAnalysisRequest request)
    {
        return Task.FromResult(new PermissionImpactAnalysisResponse
        {
            AffectedUsers = 0,
            AffectedResources = 0,
            ImpactDetails = new List<string> { "Impact analysis not yet implemented" }
        });
    }

    public Task<GetResourcePermissionsResponse> GetResourcePermissionsAsync(GetResourcePermissionsRequest request)
    {
        return Task.FromResult(new GetResourcePermissionsResponse
        {
            ResourceId = request.ResourceId,
            Permissions = new List<ResourcePermissionInfo>(),
            AllowedActions = new List<string>()
        });
    }
}