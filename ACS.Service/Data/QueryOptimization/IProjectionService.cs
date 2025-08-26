using ACS.Service.Data.Models;
using System.Linq.Expressions;

namespace ACS.Service.Data.QueryOptimization;

/// <summary>
/// Service for creating optimized projections to reduce data transfer
/// </summary>
public interface IProjectionService
{
    #region User Projections

    /// <summary>
    /// Get user basic information projection
    /// </summary>
    Task<IEnumerable<UserBasicInfo>> GetUserBasicInfoAsync(Expression<Func<User, bool>>? filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get user security summary projection
    /// </summary>
    Task<IEnumerable<UserSecuritySummary>> GetUserSecuritySummaryAsync(Expression<Func<User, bool>>? filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get user activity summary projection
    /// </summary>
    Task<IEnumerable<UserActivitySummary>> GetUserActivitySummaryAsync(DateTime? since = null, CancellationToken cancellationToken = default);

    #endregion

    #region Group Projections

    /// <summary>
    /// Get group hierarchy summary projection
    /// </summary>
    Task<IEnumerable<GroupHierarchySummary>> GetGroupHierarchySummaryAsync(int? rootGroupId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get group membership summary projection
    /// </summary>
    Task<IEnumerable<GroupMembershipSummary>> GetGroupMembershipSummaryAsync(CancellationToken cancellationToken = default);

    #endregion

    #region Role Projections

    /// <summary>
    /// Get role summary projection
    /// </summary>
    Task<IEnumerable<RoleSummary>> GetRoleSummaryAsync(Expression<Func<Role, bool>>? filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get role assignment summary projection
    /// </summary>
    Task<IEnumerable<RoleAssignmentSummary>> GetRoleAssignmentSummaryAsync(CancellationToken cancellationToken = default);

    #endregion

    #region Permission Projections

    /// <summary>
    /// Get permission matrix projection
    /// </summary>
    Task<IEnumerable<PermissionMatrixEntry>> GetPermissionMatrixAsync(IEnumerable<int>? entityIds = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get resource access summary projection
    /// </summary>
    Task<IEnumerable<ResourceAccessSummary>> GetResourceAccessSummaryAsync(IEnumerable<string>? resourceUris = null, CancellationToken cancellationToken = default);

    #endregion

    #region Analytics Projections

    /// <summary>
    /// Get system usage analytics projection
    /// </summary>
    Task<SystemUsageAnalytics> GetSystemUsageAnalyticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get security analytics projection
    /// </summary>
    Task<SecurityAnalytics> GetSecurityAnalyticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    #endregion

    #region Custom Projections

    /// <summary>
    /// Execute custom projection query
    /// </summary>
    Task<IEnumerable<T>> ExecuteProjectionAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Get paged projection results
    /// </summary>
    Task<PagedProjectionResult<T>> GetPagedProjectionAsync<T>(IQueryable<T> query, int pageNumber, int pageSize, CancellationToken cancellationToken = default) where T : class;

    #endregion
}

#region Projection Models

/// <summary>
/// Basic user information projection
/// </summary>
public class UserBasicInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public int FailedLoginAttempts { get; set; }
}

/// <summary>
/// User security summary projection
/// </summary>
public class UserSecuritySummary
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int DirectRoleCount { get; set; }
    public int GroupCount { get; set; }
    public int InheritedRoleCount { get; set; }
    public int TotalPermissionCount { get; set; }
    public bool HasAdminRoles { get; set; }
    public DateTime? LastSecurityUpdate { get; set; }
    public IEnumerable<string> RoleNames { get; set; } = new List<string>();
    public IEnumerable<string> GroupNames { get; set; } = new List<string>();
}

/// <summary>
/// User activity summary projection
/// </summary>
public class UserActivitySummary
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int LoginCount { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public int ActionCount { get; set; }
    public IEnumerable<string> RecentActions { get; set; } = new List<string>();
    public bool HasSuspiciousActivity { get; set; }
}

/// <summary>
/// Group hierarchy summary projection
/// </summary>
public class GroupHierarchySummary
{
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public int? ParentGroupId { get; set; }
    public string? ParentGroupName { get; set; }
    public int Level { get; set; }
    public string Path { get; set; } = string.Empty;
    public int UserCount { get; set; }
    public int RoleCount { get; set; }
    public int ChildGroupCount { get; set; }
    public int TotalDescendantUsers { get; set; }
}

/// <summary>
/// Group membership summary projection
/// </summary>
public class GroupMembershipSummary
{
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public int DirectUserCount { get; set; }
    public int TotalUserCount { get; set; }
    public int RoleCount { get; set; }
    public int PermissionCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public IEnumerable<string> TopUserNames { get; set; } = new List<string>();
    public IEnumerable<string> RoleNames { get; set; } = new List<string>();
}

/// <summary>
/// Role summary projection
/// </summary>
public class RoleSummary
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public int UserCount { get; set; }
    public int GroupCount { get; set; }
    public int PermissionCount { get; set; }
    public int ResourceCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public IEnumerable<string> VerbTypes { get; set; } = new List<string>();
    public IEnumerable<string> ResourceTypes { get; set; } = new List<string>();
}

/// <summary>
/// Role assignment summary projection
/// </summary>
public class RoleAssignmentSummary
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public int DirectUserAssignments { get; set; }
    public int GroupAssignments { get; set; }
    public int EffectiveUserCount { get; set; }
    public bool IsSystemRole { get; set; }
    public bool IsHighPrivilege { get; set; }
    public IEnumerable<string> TopAssignedUsers { get; set; } = new List<string>();
    public IEnumerable<string> AssignedGroups { get; set; } = new List<string>();
}

/// <summary>
/// Permission matrix entry projection
/// </summary>
public class PermissionMatrixEntry
{
    public int EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string ResourceUri { get; set; } = string.Empty;
    public string VerbName { get; set; } = string.Empty;
    public bool IsGrant { get; set; }
    public bool IsDeny { get; set; }
    public string SchemeType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Resource access summary projection
/// </summary>
public class ResourceAccessSummary
{
    public int ResourceId { get; set; }
    public string ResourceUri { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public int TotalAccessRules { get; set; }
    public int GrantRules { get; set; }
    public int DenyRules { get; set; }
    public int EntityCount { get; set; }
    public int UserCount { get; set; }
    public int VerbCount { get; set; }
    public bool HasConflicts { get; set; }
    public IEnumerable<string> VerbTypes { get; set; } = new List<string>();
    public IEnumerable<string> AccessingEntityTypes { get; set; } = new List<string>();
}

/// <summary>
/// System usage analytics projection
/// </summary>
public class SystemUsageAnalytics
{
    public DateTime AnalysisStartDate { get; set; }
    public DateTime AnalysisEndDate { get; set; }
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalGroups { get; set; }
    public int TotalRoles { get; set; }
    public int TotalResources { get; set; }
    public int TotalPermissions { get; set; }
    public int LoginCount { get; set; }
    public int ActionCount { get; set; }
    public Dictionary<string, int> ActionsByType { get; set; } = new();
    public Dictionary<DateTime, int> DailyActiveUsers { get; set; } = new();
    public Dictionary<string, int> ResourceUsage { get; set; } = new();
    public IEnumerable<string> TopActiveUsers { get; set; } = new List<string>();
    public IEnumerable<string> MostAccessedResources { get; set; } = new List<string>();
}

/// <summary>
/// Security analytics projection
/// </summary>
public class SecurityAnalytics
{
    public DateTime AnalysisStartDate { get; set; }
    public DateTime AnalysisEndDate { get; set; }
    public int TotalSecurityEvents { get; set; }
    public int SuccessfulLogins { get; set; }
    public int FailedLogins { get; set; }
    public int UnauthorizedAccess { get; set; }
    public int PermissionChanges { get; set; }
    public int SuspiciousActivities { get; set; }
    public int UsersWithExcessiveFailures { get; set; }
    public Dictionary<string, int> EventsByType { get; set; } = new();
    public Dictionary<DateTime, int> DailySecurityEvents { get; set; } = new();
    public Dictionary<string, int> EventsByUser { get; set; } = new();
    public IEnumerable<string> TopRiskUsers { get; set; } = new List<string>();
    public IEnumerable<string> RecentSecurityAlerts { get; set; } = new List<string>();
}

/// <summary>
/// Paged projection result
/// </summary>
public class PagedProjectionResult<T>
{
    public IEnumerable<T> Items { get; set; } = new List<T>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}

#endregion