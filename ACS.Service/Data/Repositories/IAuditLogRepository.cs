using ACS.Service.Data.Models;
using System.Linq.Expressions;

namespace ACS.Service.Data.Repositories;

/// <summary>
/// Repository interface for AuditLog-specific operations
/// </summary>
public interface IAuditLogRepository : IRepository<AuditLog>
{
    /// <summary>
    /// Find audit logs by entity type
    /// </summary>
    Task<IEnumerable<AuditLog>> FindByEntityTypeAsync(string entityType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find audit logs by entity ID
    /// </summary>
    Task<IEnumerable<AuditLog>> FindByEntityIdAsync(int entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find audit logs by change type
    /// </summary>
    Task<IEnumerable<AuditLog>> FindByChangeTypeAsync(string changeType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find audit logs by user who made the changes
    /// </summary>
    Task<IEnumerable<AuditLog>> FindByChangedByAsync(string changedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find audit logs within date range
    /// </summary>
    Task<IEnumerable<AuditLog>> FindByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit trail for specific entity
    /// </summary>
    Task<IEnumerable<AuditLog>> GetEntityAuditTrailAsync(string entityType, int entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find security-related audit logs
    /// </summary>
    Task<IEnumerable<AuditLog>> FindSecurityAuditLogsAsync(DateTime? since = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit log statistics
    /// </summary>
    Task<AuditLogStatistics> GetAuditLogStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find compliance audit logs for regulatory reporting
    /// </summary>
    Task<IEnumerable<AuditLog>> FindComplianceAuditLogsAsync(IEnumerable<string> complianceTypes, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get user activity audit logs
    /// </summary>
    Task<IEnumerable<UserActivityAudit>> GetUserActivityAuditAsync(int? userId = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find failed operations audit logs
    /// </summary>
    Task<IEnumerable<AuditLog>> FindFailedOperationsAsync(DateTime? since = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit logs requiring attention (suspicious activities)
    /// </summary>
    Task<IEnumerable<AuditLog>> GetSuspiciousActivitiesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Archive old audit logs
    /// </summary>
    Task<int> ArchiveOldLogsAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk insert audit logs for performance
    /// </summary>
    Task BulkInsertAuditLogsAsync(IEnumerable<AuditLog> auditLogs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit logs with advanced filtering
    /// </summary>
    Task<PagedResult<AuditLog>> GetAuditLogsAsync(AuditLogFilter filter, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>
/// User activity audit information
/// </summary>
public class UserActivityAudit
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public DateTime ActivityDate { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string ChangeDetails { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
}

/// <summary>
/// Audit log statistics model
/// </summary>
public class AuditLogStatistics
{
    public int TotalLogs { get; set; }
    public Dictionary<string, int> LogsByEntityType { get; set; } = new();
    public Dictionary<string, int> LogsByChangeType { get; set; } = new();
    public Dictionary<string, int> LogsByUser { get; set; } = new();
    public Dictionary<DateTime, int> LogsByDate { get; set; } = new();
    public int SecurityEvents { get; set; }
    public int FailedOperations { get; set; }
    public int SuspiciousActivities { get; set; }
    public double AverageLogsPerDay { get; set; }
    public DateTime? FirstLogDate { get; set; }
    public DateTime? LastLogDate { get; set; }
}

/// <summary>
/// Audit log filter for advanced queries
/// </summary>
public class AuditLogFilter
{
    public IEnumerable<string>? EntityTypes { get; set; }
    public IEnumerable<int>? EntityIds { get; set; }
    public IEnumerable<string>? ChangeTypes { get; set; }
    public IEnumerable<string>? ChangedBy { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool SecurityEventsOnly { get; set; } = false;
    public bool FailedOperationsOnly { get; set; } = false;
    public bool SuspiciousActivitiesOnly { get; set; } = false;
    public string? SearchTerm { get; set; }
    public IEnumerable<string>? IpAddresses { get; set; }
    public string? OrderBy { get; set; } = "ChangeDate";
    public string? OrderDirection { get; set; } = "DESC";
}