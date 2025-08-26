using ACS.Service.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace ACS.Service.Data.Repositories;

/// <summary>
/// Repository implementation for AuditLog-specific operations
/// </summary>
public class AuditLogRepository : Repository<AuditLog>, IAuditLogRepository
{
    public AuditLogRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<AuditLog>> FindByEntityTypeAsync(string entityType, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(al => al.EntityType == entityType)
            .OrderByDescending(al => al.ChangeDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<AuditLog>> FindByEntityIdAsync(int entityId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(al => al.EntityId == entityId)
            .OrderByDescending(al => al.ChangeDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<AuditLog>> FindByChangeTypeAsync(string changeType, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(al => al.ChangeType == changeType)
            .OrderByDescending(al => al.ChangeDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<AuditLog>> FindByChangedByAsync(string changedBy, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(al => al.ChangedBy == changedBy)
            .OrderByDescending(al => al.ChangeDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<AuditLog>> FindByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(al => al.ChangeDate >= startDate && al.ChangeDate <= endDate)
            .OrderByDescending(al => al.ChangeDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<AuditLog>> GetEntityAuditTrailAsync(string entityType, int entityId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(al => al.EntityType == entityType && al.EntityId == entityId)
            .OrderBy(al => al.ChangeDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<AuditLog>> FindSecurityAuditLogsAsync(DateTime? since = null, CancellationToken cancellationToken = default)
    {
        var securityKeywords = new[] { "login", "logout", "failed", "unauthorized", "security", "permission", "access" };
        
        var query = _dbSet.Where(al => 
            al.ChangeType.ToLower().Contains("security") ||
            al.ChangeDetails.ToLower().Contains("login") ||
            al.ChangeDetails.ToLower().Contains("failed") ||
            al.ChangeDetails.ToLower().Contains("unauthorized") ||
            securityKeywords.Any(keyword => al.ChangeDetails.ToLower().Contains(keyword)));

        if (since.HasValue)
            query = query.Where(al => al.ChangeDate >= since.Value);

        return await query
            .OrderByDescending(al => al.ChangeDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<AuditLogStatistics> GetAuditLogStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();
        
        if (startDate.HasValue)
            query = query.Where(al => al.ChangeDate >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(al => al.ChangeDate <= endDate.Value);

        var totalLogs = await query.CountAsync(cancellationToken);
        
        var logsByEntityType = await query
            .GroupBy(al => al.EntityType)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);

        var logsByChangeType = await query
            .GroupBy(al => al.ChangeType)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);

        var logsByUser = await query
            .GroupBy(al => al.ChangedBy)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);

        var logsByDate = await query
            .GroupBy(al => al.ChangeDate.Date)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);

        var securityEvents = await query
            .CountAsync(al => al.ChangeType.ToLower().Contains("security") || 
                             al.ChangeDetails.ToLower().Contains("security"), cancellationToken);

        var failedOperations = await query
            .CountAsync(al => al.ChangeDetails.ToLower().Contains("failed") || 
                             al.ChangeDetails.ToLower().Contains("error"), cancellationToken);

        var suspiciousActivities = await query
            .CountAsync(al => al.ChangeDetails.ToLower().Contains("suspicious") || 
                             al.ChangeDetails.ToLower().Contains("anomaly"), cancellationToken);

        var dateRange = endDate.HasValue && startDate.HasValue 
            ? (endDate.Value - startDate.Value).TotalDays 
            : totalLogs > 0 ? (DateTime.UtcNow - query.Min(al => al.ChangeDate)).TotalDays : 1;

        var averageLogsPerDay = dateRange > 0 ? totalLogs / dateRange : 0;

        var firstLogDate = totalLogs > 0 ? await query.MinAsync(al => (DateTime?)al.ChangeDate, cancellationToken) : null;
        var lastLogDate = totalLogs > 0 ? await query.MaxAsync(al => (DateTime?)al.ChangeDate, cancellationToken) : null;

        return new AuditLogStatistics
        {
            TotalLogs = totalLogs,
            LogsByEntityType = logsByEntityType,
            LogsByChangeType = logsByChangeType,
            LogsByUser = logsByUser,
            LogsByDate = logsByDate,
            SecurityEvents = securityEvents,
            FailedOperations = failedOperations,
            SuspiciousActivities = suspiciousActivities,
            AverageLogsPerDay = averageLogsPerDay,
            FirstLogDate = firstLogDate,
            LastLogDate = lastLogDate
        };
    }

    public async Task<IEnumerable<AuditLog>> FindComplianceAuditLogsAsync(IEnumerable<string> complianceTypes, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var complianceKeywords = complianceTypes.SelectMany(type => GetComplianceKeywords(type)).Distinct();
        
        return await _dbSet
            .Where(al => al.ChangeDate >= startDate && al.ChangeDate <= endDate &&
                        (complianceKeywords.Any(keyword => al.ChangeDetails.ToLower().Contains(keyword.ToLower())) ||
                         complianceKeywords.Any(keyword => al.ChangeType.ToLower().Contains(keyword.ToLower()))))
            .OrderByDescending(al => al.ChangeDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<UserActivityAudit>> GetUserActivityAuditAsync(int? userId = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
    {
        var query = from al in _dbSet
                   join u in _context.Users on al.ChangedBy equals u.Email into users
                   from user in users.DefaultIfEmpty()
                   where (userId == null || user.Id == userId) &&
                         (startDate == null || al.ChangeDate >= startDate) &&
                         (endDate == null || al.ChangeDate <= endDate)
                   select new UserActivityAudit
                   {
                       UserId = user != null ? user.Id : 0,
                       UserName = user != null ? user.Name : al.ChangedBy,
                       UserEmail = user != null ? user.Email : al.ChangedBy,
                       ActivityDate = al.ChangeDate,
                       ActivityType = al.ChangeType,
                       EntityType = al.EntityType,
                       EntityId = al.EntityId,
                       ChangeDetails = al.ChangeDetails,
                       IpAddress = ExtractIpAddress(al.ChangeDetails),
                       UserAgent = ExtractUserAgent(al.ChangeDetails)
                   };

        return await query
            .OrderByDescending(ua => ua.ActivityDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<AuditLog>> FindFailedOperationsAsync(DateTime? since = null, CancellationToken cancellationToken = default)
    {
        var failureKeywords = new[] { "failed", "error", "exception", "denied", "unauthorized", "forbidden", "timeout" };
        
        var query = _dbSet.Where(al => 
            failureKeywords.Any(keyword => al.ChangeDetails.ToLower().Contains(keyword)) ||
            failureKeywords.Any(keyword => al.ChangeType.ToLower().Contains(keyword)));

        if (since.HasValue)
            query = query.Where(al => al.ChangeDate >= since.Value);

        return await query
            .OrderByDescending(al => al.ChangeDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<AuditLog>> GetSuspiciousActivitiesAsync(CancellationToken cancellationToken = default)
    {
        var suspiciousPatterns = new[]
        {
            // Multiple failed login attempts
            "failed login",
            "brute force",
            "suspicious",
            "anomaly",
            "privilege escalation",
            "unauthorized access",
            "unusual activity",
            "security violation",
            "mass deletion",
            "bulk modification"
        };

        var recentDate = DateTime.UtcNow.AddDays(-7);
        
        return await _dbSet
            .Where(al => al.ChangeDate >= recentDate &&
                        suspiciousPatterns.Any(pattern => al.ChangeDetails.ToLower().Contains(pattern)))
            .OrderByDescending(al => al.ChangeDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> ArchiveOldLogsAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        var oldLogs = await _dbSet
            .Where(al => al.ChangeDate < cutoffDate)
            .ToListAsync(cancellationToken);

        if (oldLogs.Any())
        {
            // In a real implementation, you might move these to an archive table
            // For now, we'll just delete them
            _dbSet.RemoveRange(oldLogs);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return oldLogs.Count;
    }

    public async Task BulkInsertAuditLogsAsync(IEnumerable<AuditLog> auditLogs, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddRangeAsync(auditLogs, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<AuditLog>> GetAuditLogsAsync(AuditLogFilter filter, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        // Apply filters
        if (filter.EntityTypes?.Any() == true)
            query = query.Where(al => filter.EntityTypes.Contains(al.EntityType));

        if (filter.EntityIds?.Any() == true)
            query = query.Where(al => filter.EntityIds.Contains(al.EntityId));

        if (filter.ChangeTypes?.Any() == true)
            query = query.Where(al => filter.ChangeTypes.Contains(al.ChangeType));

        if (filter.ChangedBy?.Any() == true)
            query = query.Where(al => filter.ChangedBy.Contains(al.ChangedBy));

        if (filter.StartDate.HasValue)
            query = query.Where(al => al.ChangeDate >= filter.StartDate.Value);

        if (filter.EndDate.HasValue)
            query = query.Where(al => al.ChangeDate <= filter.EndDate.Value);

        if (filter.SecurityEventsOnly)
        {
            var securityKeywords = new[] { "security", "login", "logout", "authentication", "authorization" };
            query = query.Where(al => securityKeywords.Any(keyword => 
                al.ChangeType.ToLower().Contains(keyword) || 
                al.ChangeDetails.ToLower().Contains(keyword)));
        }

        if (filter.FailedOperationsOnly)
        {
            var failureKeywords = new[] { "failed", "error", "exception", "denied" };
            query = query.Where(al => failureKeywords.Any(keyword => 
                al.ChangeDetails.ToLower().Contains(keyword)));
        }

        if (filter.SuspiciousActivitiesOnly)
        {
            var suspiciousKeywords = new[] { "suspicious", "anomaly", "brute force", "unauthorized" };
            query = query.Where(al => suspiciousKeywords.Any(keyword => 
                al.ChangeDetails.ToLower().Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var searchTerm = filter.SearchTerm.ToLower();
            query = query.Where(al => 
                al.ChangeDetails.ToLower().Contains(searchTerm) ||
                al.ChangeType.ToLower().Contains(searchTerm) ||
                al.ChangedBy.ToLower().Contains(searchTerm) ||
                al.EntityType.ToLower().Contains(searchTerm));
        }

        // Apply ordering
        query = filter.OrderBy?.ToLower() switch
        {
            "changetype" => filter.OrderDirection?.ToLower() == "asc" 
                ? query.OrderBy(al => al.ChangeType) 
                : query.OrderByDescending(al => al.ChangeType),
            "changedby" => filter.OrderDirection?.ToLower() == "asc" 
                ? query.OrderBy(al => al.ChangedBy) 
                : query.OrderByDescending(al => al.ChangedBy),
            "entitytype" => filter.OrderDirection?.ToLower() == "asc" 
                ? query.OrderBy(al => al.EntityType) 
                : query.OrderByDescending(al => al.EntityType),
            _ => filter.OrderDirection?.ToLower() == "asc" 
                ? query.OrderBy(al => al.ChangeDate) 
                : query.OrderByDescending(al => al.ChangeDate)
        };

        return await GetPagedAsync(pageNumber, pageSize, query.Expression as Expression<Func<AuditLog, bool>>, 
            orderBy: _ => (query as IOrderedQueryable<AuditLog>) ?? query.OrderBy(al => al.ChangeDate), cancellationToken: cancellationToken);
    }

    private static IEnumerable<string> GetComplianceKeywords(string complianceType)
    {
        return complianceType.ToLower() switch
        {
            "gdpr" => new[] { "personal data", "consent", "data subject", "privacy", "deletion", "anonymization" },
            "soc2" => new[] { "access control", "availability", "confidentiality", "processing integrity", "privacy" },
            "hipaa" => new[] { "phi", "protected health", "healthcare", "medical", "patient" },
            "pci-dss" => new[] { "payment", "cardholder", "credit card", "financial" },
            "iso27001" => new[] { "information security", "risk management", "isms" },
            _ => new[] { complianceType }
        };
    }

    private static string ExtractIpAddress(string changeDetails)
    {
        // Simple extraction - in reality, you'd want more sophisticated parsing
        var ipPattern = @"\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b";
        var match = System.Text.RegularExpressions.Regex.Match(changeDetails, ipPattern);
        return match.Success ? match.Value : string.Empty;
    }

    private static string ExtractUserAgent(string changeDetails)
    {
        // Simple extraction - look for common user agent patterns
        if (changeDetails.Contains("User-Agent:"))
        {
            var start = changeDetails.IndexOf("User-Agent:") + 11;
            var end = changeDetails.IndexOf('\n', start);
            if (end == -1) end = changeDetails.Length;
            return changeDetails.Substring(start, end - start).Trim();
        }
        return string.Empty;
    }
}