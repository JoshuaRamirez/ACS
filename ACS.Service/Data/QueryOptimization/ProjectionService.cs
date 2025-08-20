using ACS.Service.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace ACS.Service.Data.QueryOptimization;

/// <summary>
/// Service implementation for creating optimized projections
/// </summary>
public class ProjectionService : IProjectionService
{
    private readonly ApplicationDbContext _context;

    public ProjectionService(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    #region User Projections

    public async Task<IEnumerable<UserBasicInfo>> GetUserBasicInfoAsync(Expression<Func<User, bool>>? filter = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Users.AsNoTracking();
        
        if (filter != null)
            query = query.Where(filter);

        return await query
            .Select(u => new UserBasicInfo
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt,
                FailedLoginAttempts = u.FailedLoginAttempts
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<UserSecuritySummary>> GetUserSecuritySummaryAsync(Expression<Func<User, bool>>? filter = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Users.AsNoTracking();
        
        if (filter != null)
            query = query.Where(filter);

        return await query
            .Select(u => new UserSecuritySummary
            {
                UserId = u.Id,
                UserName = u.Name,
                Email = u.Email,
                DirectRoleCount = u.UserRoles.Count(),
                GroupCount = u.UserGroups.Count(),
                InheritedRoleCount = u.UserGroups
                    .SelectMany(ug => ug.Group.GroupRoles)
                    .Select(gr => gr.RoleId)
                    .Distinct()
                    .Count(),
                TotalPermissionCount = _context.UriAccesses.Count(ua =>
                    ua.Grant && (
                        ua.PermissionScheme.Entity.Users.Any(user => user.Id == u.Id) ||
                        ua.PermissionScheme.Entity.Roles.Any(r => r.UserRoles.Any(ur => ur.UserId == u.Id)) ||
                        ua.PermissionScheme.Entity.Groups.Any(g => g.UserGroups.Any(ug => ug.UserId == u.Id))
                    )),
                HasAdminRoles = u.UserRoles.Any(ur => ur.Role.Name.ToLower().Contains("admin")) ||
                               u.UserGroups.Any(ug => ug.Group.GroupRoles.Any(gr => gr.Role.Name.ToLower().Contains("admin"))),
                LastSecurityUpdate = _context.AuditLogs
                    .Where(al => al.EntityType == "User" && al.EntityId == u.Id && 
                                (al.ChangeType.Contains("Role") || al.ChangeType.Contains("Permission")))
                    .Max(al => (DateTime?)al.ChangeDate),
                RoleNames = u.UserRoles.Select(ur => ur.Role.Name).ToList(),
                GroupNames = u.UserGroups.Select(ug => ug.Group.Name).ToList()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<UserActivitySummary>> GetUserActivitySummaryAsync(DateTime? since = null, CancellationToken cancellationToken = default)
    {
        var sinceDate = since ?? DateTime.UtcNow.AddDays(-30);

        return await _context.Users
            .AsNoTracking()
            .Select(u => new UserActivitySummary
            {
                UserId = u.Id,
                UserName = u.Name,
                Email = u.Email,
                LoginCount = _context.AuditLogs.Count(al =>
                    al.ChangedBy == u.Email &&
                    al.ChangeDate >= sinceDate &&
                    al.ChangeDetails.ToLower().Contains("login") &&
                    !al.ChangeDetails.ToLower().Contains("failed")),
                FailedLoginCount = _context.AuditLogs.Count(al =>
                    al.ChangedBy == u.Email &&
                    al.ChangeDate >= sinceDate &&
                    al.ChangeDetails.ToLower().Contains("failed login")),
                LastLoginAt = u.LastLoginAt,
                ActionCount = _context.AuditLogs.Count(al =>
                    al.ChangedBy == u.Email &&
                    al.ChangeDate >= sinceDate),
                RecentActions = _context.AuditLogs
                    .Where(al => al.ChangedBy == u.Email && al.ChangeDate >= sinceDate)
                    .OrderByDescending(al => al.ChangeDate)
                    .Take(5)
                    .Select(al => al.ChangeType)
                    .ToList(),
                HasSuspiciousActivity = _context.AuditLogs.Any(al =>
                    al.ChangedBy == u.Email &&
                    al.ChangeDate >= sinceDate &&
                    (al.ChangeDetails.ToLower().Contains("suspicious") ||
                     al.ChangeDetails.ToLower().Contains("anomaly") ||
                     al.ChangeDetails.ToLower().Contains("brute force")))
            })
            .ToListAsync(cancellationToken);
    }

    #endregion

    #region Group Projections

    public async Task<IEnumerable<GroupHierarchySummary>> GetGroupHierarchySummaryAsync(int? rootGroupId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Groups.AsNoTracking();
        
        if (rootGroupId.HasValue)
        {
            // Get descendants of the root group using recursive CTE would be complex in LINQ
            // For now, we'll get all groups and filter in memory (not ideal for large datasets)
            query = query.Where(g => g.Id == rootGroupId.Value || 
                                   g.ParentGroupRelations.Any(gh => gh.ParentGroupId == rootGroupId.Value));
        }

        return await query
            .Select(g => new GroupHierarchySummary
            {
                GroupId = g.Id,
                GroupName = g.Name,
                ParentGroupId = g.ParentGroupRelations.Select(gh => gh.ParentGroupId).FirstOrDefault(),
                ParentGroupName = g.ParentGroupRelations.Select(gh => gh.ParentGroup.Name).FirstOrDefault(),
                Level = 0, // This would need to be calculated with recursive logic
                Path = g.Name, // This would need to be calculated with recursive logic
                UserCount = g.UserGroups.Count(),
                RoleCount = g.GroupRoles.Count(),
                ChildGroupCount = g.ChildGroupRelations.Count(),
                TotalDescendantUsers = g.UserGroups.Count() // This is simplified, should include descendants
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<GroupMembershipSummary>> GetGroupMembershipSummaryAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Groups
            .AsNoTracking()
            .Select(g => new GroupMembershipSummary
            {
                GroupId = g.Id,
                GroupName = g.Name,
                DirectUserCount = g.UserGroups.Count(),
                TotalUserCount = g.UserGroups.Count(), // Simplified - should include inherited users
                RoleCount = g.GroupRoles.Count(),
                PermissionCount = _context.UriAccesses.Count(ua =>
                    ua.Grant && ua.PermissionScheme.Entity.Groups.Any(grp => grp.Id == g.Id)),
                CreatedAt = g.CreatedAt,
                TopUserNames = g.UserGroups
                    .OrderBy(ug => ug.User.Name)
                    .Take(5)
                    .Select(ug => ug.User.Name)
                    .ToList(),
                RoleNames = g.GroupRoles.Select(gr => gr.Role.Name).ToList()
            })
            .ToListAsync(cancellationToken);
    }

    #endregion

    #region Role Projections

    public async Task<IEnumerable<RoleSummary>> GetRoleSummaryAsync(Expression<Func<Role, bool>>? filter = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Roles.AsNoTracking();
        
        if (filter != null)
            query = query.Where(filter);

        return await query
            .Select(r => new RoleSummary
            {
                RoleId = r.Id,
                RoleName = r.Name,
                UserCount = r.UserRoles.Count(),
                GroupCount = r.GroupRoles.Count(),
                PermissionCount = _context.UriAccesses.Count(ua =>
                    ua.PermissionScheme.EntityId == r.EntityId),
                ResourceCount = _context.UriAccesses
                    .Where(ua => ua.PermissionScheme.EntityId == r.EntityId)
                    .Select(ua => ua.ResourceId)
                    .Distinct()
                    .Count(),
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                VerbTypes = _context.UriAccesses
                    .Where(ua => ua.PermissionScheme.EntityId == r.EntityId)
                    .Select(ua => ua.VerbType.VerbName)
                    .Distinct()
                    .ToList(),
                ResourceTypes = _context.UriAccesses
                    .Where(ua => ua.PermissionScheme.EntityId == r.EntityId)
                    .Select(ua => ua.Resource.Uri.Split('/')[0])
                    .Distinct()
                    .ToList()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<RoleAssignmentSummary>> GetRoleAssignmentSummaryAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .AsNoTracking()
            .Select(r => new RoleAssignmentSummary
            {
                RoleId = r.Id,
                RoleName = r.Name,
                DirectUserAssignments = r.UserRoles.Count(),
                GroupAssignments = r.GroupRoles.Count(),
                EffectiveUserCount = r.UserRoles.Count() + 
                    r.GroupRoles.SelectMany(gr => gr.Group.UserGroups).Select(ug => ug.UserId).Distinct().Count(),
                IsSystemRole = r.Name.ToLower().Contains("system") || r.Name.ToLower().Contains("admin"),
                IsHighPrivilege = _context.UriAccesses.Count(ua => 
                    ua.Grant && 
                    ua.PermissionScheme.EntityId == r.EntityId) > 10, // Arbitrary threshold
                TopAssignedUsers = r.UserRoles
                    .OrderBy(ur => ur.User.Name)
                    .Take(5)
                    .Select(ur => ur.User.Name)
                    .ToList(),
                AssignedGroups = r.GroupRoles.Select(gr => gr.Group.Name).ToList()
            })
            .ToListAsync(cancellationToken);
    }

    #endregion

    #region Permission Projections

    public async Task<IEnumerable<PermissionMatrixEntry>> GetPermissionMatrixAsync(IEnumerable<int>? entityIds = null, CancellationToken cancellationToken = default)
    {
        var query = _context.UriAccesses.AsNoTracking();
        
        if (entityIds != null && entityIds.Any())
            query = query.Where(ua => entityIds.Contains(ua.PermissionScheme.EntityId));

        return await query
            .Select(ua => new PermissionMatrixEntry
            {
                EntityId = ua.PermissionScheme.EntityId,
                EntityType = ua.PermissionScheme.Entity.EntityType,
                EntityName = ua.PermissionScheme.Entity.EntityType == "User"
                    ? ua.PermissionScheme.Entity.Users.First().Name
                    : ua.PermissionScheme.Entity.EntityType == "Group"
                        ? ua.PermissionScheme.Entity.Groups.First().Name
                        : ua.PermissionScheme.Entity.Roles.First().Name,
                ResourceUri = ua.Resource.Uri,
                VerbName = ua.VerbType.VerbName,
                IsGrant = ua.Grant,
                IsDeny = ua.Deny,
                SchemeType = ua.PermissionScheme.SchemeType.SchemeName,
                CreatedAt = ua.PermissionScheme.Entity.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<ResourceAccessSummary>> GetResourceAccessSummaryAsync(IEnumerable<string>? resourceUris = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Resources.AsNoTracking();
        
        if (resourceUris != null && resourceUris.Any())
            query = query.Where(r => resourceUris.Contains(r.Uri));

        return await query
            .Select(r => new ResourceAccessSummary
            {
                ResourceId = r.Id,
                ResourceUri = r.Uri,
                ResourceType = r.Uri.Split('/').FirstOrDefault() ?? "Unknown",
                TotalAccessRules = r.UriAccesses.Count(),
                GrantRules = r.UriAccesses.Count(ua => ua.Grant),
                DenyRules = r.UriAccesses.Count(ua => ua.Deny),
                EntityCount = r.UriAccesses.Select(ua => ua.PermissionScheme.EntityId).Distinct().Count(),
                UserCount = r.UriAccesses
                    .SelectMany(ua => ua.PermissionScheme.Entity.Users)
                    .Select(u => u.Id)
                    .Union(r.UriAccesses
                        .SelectMany(ua => ua.PermissionScheme.Entity.Roles)
                        .SelectMany(role => role.UserRoles)
                        .Select(ur => ur.UserId))
                    .Union(r.UriAccesses
                        .SelectMany(ua => ua.PermissionScheme.Entity.Groups)
                        .SelectMany(g => g.UserGroups)
                        .Select(ug => ug.UserId))
                    .Distinct()
                    .Count(),
                VerbCount = r.UriAccesses.Select(ua => ua.VerbTypeId).Distinct().Count(),
                HasConflicts = r.UriAccesses.Any(ua1 => 
                    r.UriAccesses.Any(ua2 => 
                        ua1.VerbTypeId == ua2.VerbTypeId && 
                        ua1.Id != ua2.Id && 
                        ua1.Grant != ua2.Grant)),
                VerbTypes = r.UriAccesses.Select(ua => ua.VerbType.VerbName).Distinct().ToList(),
                AccessingEntityTypes = r.UriAccesses
                    .Select(ua => ua.PermissionScheme.Entity.EntityType)
                    .Distinct()
                    .ToList()
            })
            .ToListAsync(cancellationToken);
    }

    #endregion

    #region Analytics Projections

    public async Task<SystemUsageAnalytics> GetSystemUsageAnalyticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var auditLogs = await _context.AuditLogs
            .AsNoTracking()
            .Where(al => al.ChangeDate >= startDate && al.ChangeDate <= endDate)
            .ToListAsync(cancellationToken);

        var totalUsers = await _context.Users.CountAsync(cancellationToken);
        var activeUsers = await _context.Users.CountAsync(u => u.IsActive, cancellationToken);
        var totalGroups = await _context.Groups.CountAsync(cancellationToken);
        var totalRoles = await _context.Roles.CountAsync(cancellationToken);
        var totalResources = await _context.Resources.CountAsync(cancellationToken);
        var totalPermissions = await _context.UriAccesses.CountAsync(cancellationToken);

        var loginLogs = auditLogs.Where(al => al.ChangeDetails.ToLower().Contains("login") && 
                                            !al.ChangeDetails.ToLower().Contains("failed"));

        return new SystemUsageAnalytics
        {
            AnalysisStartDate = startDate,
            AnalysisEndDate = endDate,
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,
            TotalGroups = totalGroups,
            TotalRoles = totalRoles,
            TotalResources = totalResources,
            TotalPermissions = totalPermissions,
            LoginCount = loginLogs.Count(),
            ActionCount = auditLogs.Count,
            ActionsByType = auditLogs
                .GroupBy(al => al.ChangeType)
                .ToDictionary(g => g.Key, g => g.Count()),
            DailyActiveUsers = loginLogs
                .GroupBy(al => al.ChangeDate.Date)
                .ToDictionary(g => g.Key, g => g.Select(al => al.ChangedBy).Distinct().Count()),
            ResourceUsage = auditLogs
                .Where(al => al.EntityType == "Resource")
                .GroupBy(al => al.EntityId.ToString())
                .Take(10)
                .ToDictionary(g => g.Key, g => g.Count()),
            TopActiveUsers = loginLogs
                .GroupBy(al => al.ChangedBy)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => g.Key),
            MostAccessedResources = auditLogs
                .Where(al => al.EntityType == "Resource")
                .GroupBy(al => al.EntityId.ToString())
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => g.Key)
        };
    }

    public async Task<SecurityAnalytics> GetSecurityAnalyticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var securityLogs = await _context.AuditLogs
            .AsNoTracking()
            .Where(al => al.ChangeDate >= startDate && al.ChangeDate <= endDate &&
                        (al.ChangeType.ToLower().Contains("security") ||
                         al.ChangeDetails.ToLower().Contains("login") ||
                         al.ChangeDetails.ToLower().Contains("failed") ||
                         al.ChangeDetails.ToLower().Contains("unauthorized") ||
                         al.ChangeDetails.ToLower().Contains("permission")))
            .ToListAsync(cancellationToken);

        var successfulLogins = securityLogs.Where(al => 
            al.ChangeDetails.ToLower().Contains("login") && 
            !al.ChangeDetails.ToLower().Contains("failed"));

        var failedLogins = securityLogs.Where(al => 
            al.ChangeDetails.ToLower().Contains("failed login"));

        var unauthorizedAccess = securityLogs.Where(al => 
            al.ChangeDetails.ToLower().Contains("unauthorized"));

        var permissionChanges = securityLogs.Where(al => 
            al.ChangeDetails.ToLower().Contains("permission") ||
            al.ChangeType.ToLower().Contains("role"));

        var suspiciousActivities = securityLogs.Where(al => 
            al.ChangeDetails.ToLower().Contains("suspicious") ||
            al.ChangeDetails.ToLower().Contains("anomaly") ||
            al.ChangeDetails.ToLower().Contains("brute force"));

        return new SecurityAnalytics
        {
            AnalysisStartDate = startDate,
            AnalysisEndDate = endDate,
            TotalSecurityEvents = securityLogs.Count,
            SuccessfulLogins = successfulLogins.Count(),
            FailedLogins = failedLogins.Count(),
            UnauthorizedAccess = unauthorizedAccess.Count(),
            PermissionChanges = permissionChanges.Count(),
            SuspiciousActivities = suspiciousActivities.Count(),
            UsersWithExcessiveFailures = await _context.Users
                .CountAsync(u => u.FailedLoginAttempts >= 5, cancellationToken),
            EventsByType = securityLogs
                .GroupBy(al => al.ChangeType)
                .ToDictionary(g => g.Key, g => g.Count()),
            DailySecurityEvents = securityLogs
                .GroupBy(al => al.ChangeDate.Date)
                .ToDictionary(g => g.Key, g => g.Count()),
            EventsByUser = securityLogs
                .GroupBy(al => al.ChangedBy)
                .ToDictionary(g => g.Key, g => g.Count()),
            TopRiskUsers = failedLogins
                .GroupBy(al => al.ChangedBy)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => g.Key),
            RecentSecurityAlerts = suspiciousActivities
                .OrderByDescending(al => al.ChangeDate)
                .Take(5)
                .Select(al => $"{al.ChangeType}: {al.ChangeDetails[..Math.Min(100, al.ChangeDetails.Length)]}")
        };
    }

    #endregion

    #region Custom Projections

    public async Task<IEnumerable<T>> ExecuteProjectionAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
    {
        return await query.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<PagedProjectionResult<T>> GetPagedProjectionAsync<T>(IQueryable<T> query, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new PagedProjectionResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    #endregion
}