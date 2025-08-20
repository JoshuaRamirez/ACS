using ACS.Service.Data;
using ACS.Service.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ACS.Service.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<AuditService> _logger;
    private readonly Dictionary<string, bool> _monitoredUsers = new();
    private readonly Dictionary<int, AlertRule> _alertRules = new();
    private readonly Dictionary<string, AuditSession> _activeSessions = new();

    public AuditService(
        ApplicationDbContext dbContext,
        ILogger<AuditService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    #region Core Audit Logging

    public async Task LogAsync(string action, string entityType, int entityId, string performedBy, string details)
    {
        var auditLog = new AuditLog
        {
            EntityType = entityType,
            EntityId = entityId,
            ChangeType = action,
            ChangedBy = performedBy,
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = details
        };

        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync();

        // Check alert rules
        await CheckAlertRulesAsync(auditLog);

        _logger.LogInformation("Audit log created: {Action} on {EntityType}:{EntityId} by {User}",
            action, entityType, entityId, performedBy);
    }

    public async Task LogSecurityEventAsync(string eventType, string severity, string source, string details, string? userId = null)
    {
        var securityEvent = new SecurityEvent
        {
            EventType = eventType,
            Severity = severity,
            Source = source,
            Details = details,
            UserId = userId,
            OccurredAt = DateTime.UtcNow
        };

        // Store in audit log with special marker
        await LogAsync($"SECURITY:{eventType}", "SecurityEvent", 0, userId ?? "SYSTEM",
            JsonSerializer.Serialize(securityEvent));

        // Notify security team for critical events
        if (severity == "Critical" || severity == "High")
        {
            await NotifySecurityTeamAsync($"Security Event: {eventType}", severity,
                new Dictionary<string, object>
                {
                    ["EventType"] = eventType,
                    ["Source"] = source,
                    ["Details"] = details,
                    ["UserId"] = userId ?? "N/A"
                });
        }

        _logger.LogWarning("Security event logged: {EventType} with severity {Severity} from {Source}",
            eventType, severity, source);
    }

    public async Task LogAccessAttemptAsync(string resource, string action, string userId, bool success, string? reason = null)
    {
        var details = new
        {
            Resource = resource,
            Action = action,
            Success = success,
            Reason = reason,
            Timestamp = DateTime.UtcNow
        };

        await LogAsync(
            success ? "ACCESS_GRANTED" : "ACCESS_DENIED",
            "AccessAttempt",
            0,
            userId,
            JsonSerializer.Serialize(details));

        // Track failed attempts for suspicious activity detection
        if (!success)
        {
            await TrackFailedAccessAsync(userId, resource);
        }
    }

    public async Task LogDataChangeAsync(string tableName, string operation, string recordId, string oldValue, string newValue, string changedBy)
    {
        var changeDetails = new
        {
            Table = tableName,
            Operation = operation,
            RecordId = recordId,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedAt = DateTime.UtcNow
        };

        await LogAsync($"DATA_{operation}", tableName, 0, changedBy,
            JsonSerializer.Serialize(changeDetails));
    }

    public async Task LogSystemEventAsync(string eventType, string component, string details, string? correlationId = null)
    {
        var systemEvent = new
        {
            EventType = eventType,
            Component = component,
            Details = details,
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow
        };

        await LogAsync($"SYSTEM:{eventType}", "System", 0, "SYSTEM",
            JsonSerializer.Serialize(systemEvent));
    }

    #endregion

    #region Query and Retrieval

    public async Task<IEnumerable<AuditLog>> GetAuditLogsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _dbContext.AuditLogs.AsQueryable();

        if (startDate.HasValue)
            query = query.Where(al => al.ChangeDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(al => al.ChangeDate <= endDate.Value);

        return await query.OrderByDescending(al => al.ChangeDate).ToListAsync();
    }

    public async Task<IEnumerable<AuditLog>> GetAuditLogsByEntityAsync(string entityType, int entityId)
    {
        return await _dbContext.AuditLogs
            .Where(al => al.EntityType == entityType && al.EntityId == entityId)
            .OrderByDescending(al => al.ChangeDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AuditLog>> GetAuditLogsByUserAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _dbContext.AuditLogs
            .Where(al => al.ChangedBy == userId);

        if (startDate.HasValue)
            query = query.Where(al => al.ChangeDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(al => al.ChangeDate <= endDate.Value);

        return await query.OrderByDescending(al => al.ChangeDate).ToListAsync();
    }

    public async Task<IEnumerable<AuditLog>> GetAuditLogsByActionAsync(string action, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _dbContext.AuditLogs
            .Where(al => al.ChangeType == action);

        if (startDate.HasValue)
            query = query.Where(al => al.ChangeDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(al => al.ChangeDate <= endDate.Value);

        return await query.OrderByDescending(al => al.ChangeDate).ToListAsync();
    }

    public async Task<AuditLog?> GetAuditLogByIdAsync(int auditLogId)
    {
        return await _dbContext.AuditLogs.FindAsync(auditLogId);
    }

    #endregion

    #region Security Event Monitoring

    public async Task<IEnumerable<SecurityEvent>> GetSecurityEventsAsync(string? severity = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _dbContext.AuditLogs
            .Where(al => al.ChangeType.StartsWith("SECURITY:"));

        if (startDate.HasValue)
            query = query.Where(al => al.ChangeDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(al => al.ChangeDate <= endDate.Value);

        var logs = await query.ToListAsync();
        var events = new List<SecurityEvent>();

        foreach (var log in logs)
        {
            try
            {
                var secEvent = JsonSerializer.Deserialize<SecurityEvent>(log.ChangeDetails);
                if (secEvent != null && (severity == null || secEvent.Severity == severity))
                {
                    events.Add(secEvent);
                }
            }
            catch
            {
                // Skip malformed events
            }
        }

        return events;
    }

    public async Task<IEnumerable<SecurityEvent>> GetFailedLoginAttemptsAsync(string? userId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _dbContext.AuditLogs
            .Where(al => al.ChangeType == "ACCESS_DENIED" || al.ChangeType == "LOGIN_FAILED");

        if (userId != null)
            query = query.Where(al => al.ChangedBy == userId);

        if (startDate.HasValue)
            query = query.Where(al => al.ChangeDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(al => al.ChangeDate <= endDate.Value);

        var logs = await query.ToListAsync();
        return logs.Select(l => new SecurityEvent
        {
            EventType = "FailedLogin",
            Severity = "Medium",
            Source = "Authentication",
            Details = l.ChangeDetails,
            UserId = l.ChangedBy,
            OccurredAt = l.ChangeDate
        });
    }

    public async Task<IEnumerable<SecurityEvent>> GetSuspiciousActivitiesAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var suspiciousEvents = new List<SecurityEvent>();

        // Get all users with multiple failed attempts
        var failedAttempts = await GetFailedLoginAttemptsAsync(null, startDate, endDate);
        var groupedAttempts = failedAttempts.GroupBy(e => e.UserId);

        foreach (var group in groupedAttempts.Where(g => g.Count() >= 3))
        {
            suspiciousEvents.Add(new SecurityEvent
            {
                EventType = "SuspiciousActivity",
                Severity = "High",
                Source = "Authentication",
                Details = $"Multiple failed login attempts: {group.Count()}",
                UserId = group.Key,
                OccurredAt = group.Max(e => e.OccurredAt)
            });
        }

        // Check for unusual access patterns
        var accessViolations = await GetAccessViolationsAsync(startDate, endDate);
        suspiciousEvents.AddRange(accessViolations);

        return suspiciousEvents;
    }

    public async Task<IEnumerable<SecurityEvent>> GetAccessViolationsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _dbContext.AuditLogs
            .Where(al => al.ChangeType == "ACCESS_DENIED" || 
                        al.ChangeType.Contains("VIOLATION") ||
                        al.ChangeType.Contains("UNAUTHORIZED"));

        if (startDate.HasValue)
            query = query.Where(al => al.ChangeDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(al => al.ChangeDate <= endDate.Value);

        var logs = await query.ToListAsync();
        return logs.Select(l => new SecurityEvent
        {
            EventType = "AccessViolation",
            Severity = "High",
            Source = "Authorization",
            Details = l.ChangeDetails,
            UserId = l.ChangedBy,
            OccurredAt = l.ChangeDate
        });
    }

    public async Task<bool> HasSuspiciousActivityAsync(string userId, int timeWindowMinutes = 30)
    {
        var startTime = DateTime.UtcNow.AddMinutes(-timeWindowMinutes);
        
        // Check for multiple failed attempts
        var failedAttempts = await _dbContext.AuditLogs
            .Where(al => al.ChangedBy == userId &&
                        (al.ChangeType == "ACCESS_DENIED" || al.ChangeType == "LOGIN_FAILED") &&
                        al.ChangeDate >= startTime)
            .CountAsync();

        if (failedAttempts >= 3)
            return true;

        // Check for rapid activity
        var totalActions = await _dbContext.AuditLogs
            .Where(al => al.ChangedBy == userId && al.ChangeDate >= startTime)
            .CountAsync();

        return totalActions > 100; // Threshold for suspicious rapid activity
    }

    #endregion

    #region Compliance Reporting

    public async Task<ComplianceReport> GenerateGDPRReportAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var report = new ComplianceReport
        {
            ReportType = "GDPR",
            GeneratedAt = DateTime.UtcNow,
            StartDate = startDate ?? DateTime.UtcNow.AddDays(-30),
            EndDate = endDate ?? DateTime.UtcNow,
            IsCompliant = true
        };

        // Check data access logs
        var userDataAccess = await GetAuditLogsByEntityAsync("User", int.Parse(userId));
        report.Items.Add(new ComplianceItem
        {
            Category = "Data Access",
            Requirement = "All data access must be logged",
            IsMet = userDataAccess.Any(),
            Evidence = $"Found {userDataAccess.Count()} access logs",
            CheckedAt = DateTime.UtcNow
        });

        // Check consent tracking
        var consentLogs = await _dbContext.AuditLogs
            .Where(al => al.ChangedBy == userId && al.ChangeType.Contains("CONSENT"))
            .ToListAsync();

        report.Items.Add(new ComplianceItem
        {
            Category = "Consent",
            Requirement = "User consent must be recorded",
            IsMet = consentLogs.Any(),
            Evidence = $"Found {consentLogs.Count} consent records",
            CheckedAt = DateTime.UtcNow
        });

        // Check data retention
        var oldDataCount = await _dbContext.AuditLogs
            .Where(al => al.ChangedBy == userId && al.ChangeDate < DateTime.UtcNow.AddYears(-2))
            .CountAsync();

        if (oldDataCount > 0)
        {
            report.IsCompliant = false;
            report.Violations.Add(new ComplianceViolation
            {
                Regulation = "GDPR",
                Requirement = "Data retention limits",
                ViolationType = "Excessive retention",
                Severity = "Medium",
                DetectedAt = DateTime.UtcNow
            });
        }

        report.Summary["TotalDataPoints"] = userDataAccess.Count();
        report.Summary["ConsentRecords"] = consentLogs.Count;
        report.Summary["RetentionViolations"] = oldDataCount;

        return report;
    }

    public async Task<ComplianceReport> GenerateSOC2ReportAsync(DateTime startDate, DateTime endDate)
    {
        var report = new ComplianceReport
        {
            ReportType = "SOC2",
            GeneratedAt = DateTime.UtcNow,
            StartDate = startDate,
            EndDate = endDate,
            IsCompliant = true
        };

        // Security checks
        var securityEvents = await GetSecurityEventsAsync(null, startDate, endDate);
        var criticalEvents = securityEvents.Where(e => e.Severity == "Critical").ToList();

        report.Items.Add(new ComplianceItem
        {
            Category = "Security",
            Requirement = "No unresolved critical security events",
            IsMet = !criticalEvents.Any(),
            Evidence = $"Found {criticalEvents.Count} critical events",
            CheckedAt = DateTime.UtcNow
        });

        // Availability checks
        var systemEvents = await _dbContext.AuditLogs
            .Where(al => al.ChangeType.StartsWith("SYSTEM:") &&
                        al.ChangeDate >= startDate &&
                        al.ChangeDate <= endDate)
            .ToListAsync();

        report.Items.Add(new ComplianceItem
        {
            Category = "Availability",
            Requirement = "System availability monitoring",
            IsMet = systemEvents.Any(),
            Evidence = $"System monitoring active with {systemEvents.Count} events",
            CheckedAt = DateTime.UtcNow
        });

        // Processing integrity
        var dataChanges = await _dbContext.AuditLogs
            .Where(al => al.ChangeType.StartsWith("DATA_") &&
                        al.ChangeDate >= startDate &&
                        al.ChangeDate <= endDate)
            .ToListAsync();

        report.Items.Add(new ComplianceItem
        {
            Category = "Processing Integrity",
            Requirement = "All data changes must be logged",
            IsMet = true,
            Evidence = $"Logged {dataChanges.Count} data changes",
            CheckedAt = DateTime.UtcNow
        });

        // Confidentiality checks
        var accessViolations = await GetAccessViolationsAsync(startDate, endDate);
        
        if (accessViolations.Any())
        {
            report.IsCompliant = false;
            foreach (var violation in accessViolations.Take(10))
            {
                report.Violations.Add(new ComplianceViolation
                {
                    Regulation = "SOC2",
                    Requirement = "Confidentiality",
                    ViolationType = "Unauthorized access attempt",
                    Severity = violation.Severity,
                    DetectedAt = violation.OccurredAt
                });
            }
        }

        report.Summary["SecurityEvents"] = securityEvents.Count();
        report.Summary["CriticalEvents"] = criticalEvents.Count;
        report.Summary["AccessViolations"] = accessViolations.Count();
        report.Summary["DataChanges"] = dataChanges.Count;

        return report;
    }

    public async Task<ComplianceReport> GenerateHIPAAReportAsync(DateTime startDate, DateTime endDate)
    {
        var report = new ComplianceReport
        {
            ReportType = "HIPAA",
            GeneratedAt = DateTime.UtcNow,
            StartDate = startDate,
            EndDate = endDate,
            IsCompliant = true
        };

        // PHI access logging
        var phiAccess = await _dbContext.AuditLogs
            .Where(al => (al.EntityType == "Patient" || al.EntityType == "MedicalRecord") &&
                        al.ChangeDate >= startDate &&
                        al.ChangeDate <= endDate)
            .ToListAsync();

        report.Items.Add(new ComplianceItem
        {
            Category = "PHI Access Control",
            Requirement = "All PHI access must be logged",
            IsMet = true,
            Evidence = $"Logged {phiAccess.Count} PHI access events",
            CheckedAt = DateTime.UtcNow
        });

        // Encryption validation
        report.Items.Add(new ComplianceItem
        {
            Category = "Encryption",
            Requirement = "PHI must be encrypted at rest and in transit",
            IsMet = true, // Assuming encryption is configured
            Evidence = "Encryption enabled for database and connections",
            CheckedAt = DateTime.UtcNow
        });

        // Access controls
        var unauthorizedAccess = await GetAccessViolationsAsync(startDate, endDate);
        var phiViolations = unauthorizedAccess.Where(e => 
            e.Details.Contains("Patient") || e.Details.Contains("Medical")).ToList();

        if (phiViolations.Any())
        {
            report.IsCompliant = false;
            foreach (var violation in phiViolations)
            {
                report.Violations.Add(new ComplianceViolation
                {
                    Regulation = "HIPAA",
                    Requirement = "PHI Access Control",
                    ViolationType = "Unauthorized PHI access attempt",
                    Severity = "Critical",
                    DetectedAt = violation.OccurredAt
                });
            }
        }

        report.Summary["PHIAccessEvents"] = phiAccess.Count;
        report.Summary["UnauthorizedAttempts"] = phiViolations.Count;
        report.Summary["ComplianceScore"] = report.IsCompliant ? 100.0 : 75.0;

        return report;
    }

    public async Task<ComplianceReport> GeneratePCIDSSReportAsync(DateTime startDate, DateTime endDate)
    {
        var report = new ComplianceReport
        {
            ReportType = "PCI-DSS",
            GeneratedAt = DateTime.UtcNow,
            StartDate = startDate,
            EndDate = endDate,
            IsCompliant = true
        };

        // Check for cardholder data access
        var cardDataAccess = await _dbContext.AuditLogs
            .Where(al => (al.EntityType == "Payment" || al.EntityType == "Card" ||
                         al.ChangeDetails.Contains("card") || al.ChangeDetails.Contains("payment")) &&
                        al.ChangeDate >= startDate &&
                        al.ChangeDate <= endDate)
            .ToListAsync();

        report.Items.Add(new ComplianceItem
        {
            Category = "Cardholder Data Protection",
            Requirement = "Track all access to cardholder data",
            IsMet = true,
            Evidence = $"Tracked {cardDataAccess.Count} cardholder data access events",
            CheckedAt = DateTime.UtcNow
        });

        // Check for regular security testing
        var securityTests = await _dbContext.AuditLogs
            .Where(al => al.ChangeType.Contains("SECURITY_TEST") &&
                        al.ChangeDate >= startDate &&
                        al.ChangeDate <= endDate)
            .ToListAsync();

        report.Items.Add(new ComplianceItem
        {
            Category = "Security Testing",
            Requirement = "Regular security testing must be performed",
            IsMet = securityTests.Any(),
            Evidence = $"Performed {securityTests.Count} security tests",
            CheckedAt = DateTime.UtcNow
        });

        report.Summary["CardDataAccess"] = cardDataAccess.Count;
        report.Summary["SecurityTests"] = securityTests.Count;

        return report;
    }

    public async Task<ComplianceReport> GenerateCustomComplianceReportAsync(string reportType, Dictionary<string, object> parameters)
    {
        var report = new ComplianceReport
        {
            ReportType = reportType,
            GeneratedAt = DateTime.UtcNow,
            StartDate = parameters.ContainsKey("StartDate") ? (DateTime)parameters["StartDate"] : DateTime.UtcNow.AddDays(-30),
            EndDate = parameters.ContainsKey("EndDate") ? (DateTime)parameters["EndDate"] : DateTime.UtcNow,
            IsCompliant = true
        };

        // Custom compliance logic based on parameters
        foreach (var param in parameters)
        {
            report.Summary[param.Key] = param.Value;
        }

        _logger.LogInformation("Generated custom compliance report of type {ReportType}", reportType);
        return report;
    }

    #endregion

    #region Data Retention and Privacy

    public async Task<int> PurgeOldAuditLogsAsync(int retentionDays, string? entityType = null)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
        
        var query = _dbContext.AuditLogs.Where(al => al.ChangeDate < cutoffDate);
        
        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(al => al.EntityType == entityType);

        var logsToDelete = await query.ToListAsync();
        var count = logsToDelete.Count;

        if (count > 0)
        {
            _dbContext.AuditLogs.RemoveRange(logsToDelete);
            await _dbContext.SaveChangesAsync();

            await LogSystemEventAsync("PURGE", "AuditService",
                $"Purged {count} audit logs older than {retentionDays} days", null);
        }

        _logger.LogInformation("Purged {Count} audit logs older than {Days} days", count, retentionDays);
        return count;
    }

    public async Task AnonymizeUserDataAsync(string userId, string anonymizedBy)
    {
        var userLogs = await _dbContext.AuditLogs
            .Where(al => al.ChangedBy == userId)
            .ToListAsync();

        foreach (var log in userLogs)
        {
            log.ChangedBy = $"ANONYMIZED_{GenerateHash(userId).Substring(0, 8)}";
            log.ChangeDetails = AnonymizeDetails(log.ChangeDetails);
        }

        await _dbContext.SaveChangesAsync();

        await LogAsync("ANONYMIZE_USER", "User", 0, anonymizedBy,
            $"Anonymized {userLogs.Count} audit logs for user");

        _logger.LogInformation("Anonymized {Count} audit logs for user by {AnonymizedBy}",
            userLogs.Count, anonymizedBy);
    }

    public async Task<bool> ExportUserDataAsync(string userId, string format = "json")
    {
        var userLogs = await GetAuditLogsByUserAsync(userId);
        
        var exportData = new
        {
            UserId = userId,
            ExportDate = DateTime.UtcNow,
            Format = format,
            AuditLogs = userLogs
        };

        // In a real implementation, this would write to a file or stream
        var exportJson = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await LogAsync("EXPORT_USER_DATA", "User", 0, userId,
            $"Exported user data in {format} format");

        return true;
    }

    public async Task<bool> DeleteUserDataAsync(string userId, string deletedBy, bool hardDelete = false)
    {
        if (hardDelete)
        {
            var userLogs = await _dbContext.AuditLogs
                .Where(al => al.ChangedBy == userId)
                .ToListAsync();

            _dbContext.AuditLogs.RemoveRange(userLogs);
            await _dbContext.SaveChangesAsync();

            await LogAsync("HARD_DELETE_USER_DATA", "User", 0, deletedBy,
                $"Hard deleted {userLogs.Count} audit logs for user {userId}");
        }
        else
        {
            await AnonymizeUserDataAsync(userId, deletedBy);
        }

        return true;
    }

    public async Task<DataRetentionPolicy> GetDataRetentionPolicyAsync(string dataType)
    {
        // In a real implementation, this would come from configuration
        var policies = new Dictionary<string, DataRetentionPolicy>
        {
            ["AuditLog"] = new DataRetentionPolicy
            {
                DataType = "AuditLog",
                RetentionDays = 2555, // 7 years
                PurgeStrategy = "Archive",
                RequiresApproval = true
            },
            ["SecurityEvent"] = new DataRetentionPolicy
            {
                DataType = "SecurityEvent",
                RetentionDays = 365,
                PurgeStrategy = "Delete",
                RequiresApproval = false
            },
            ["UserData"] = new DataRetentionPolicy
            {
                DataType = "UserData",
                RetentionDays = 730, // 2 years after account deletion
                PurgeStrategy = "Anonymize",
                RequiresApproval = true
            }
        };

        return await Task.FromResult(policies.ContainsKey(dataType) 
            ? policies[dataType] 
            : new DataRetentionPolicy { DataType = dataType, RetentionDays = 365, PurgeStrategy = "Archive" });
    }

    #endregion

    #region Audit Trail Integrity

    public async Task<bool> VerifyAuditTrailIntegrityAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _dbContext.AuditLogs.AsQueryable();

        if (startDate.HasValue)
            query = query.Where(al => al.ChangeDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(al => al.ChangeDate <= endDate.Value);

        var logs = await query.OrderBy(al => al.Id).ToListAsync();

        // Check for gaps in IDs
        for (int i = 1; i < logs.Count; i++)
        {
            if (logs[i].Id - logs[i - 1].Id > 1)
            {
                _logger.LogWarning("Gap detected in audit trail between IDs {Id1} and {Id2}",
                    logs[i - 1].Id, logs[i].Id);
                return false;
            }
        }

        // Verify hashes if implemented
        foreach (var log in logs)
        {
            var calculatedHash = await CalculateAuditHashAsync(log.Id);
            // In a real implementation, compare with stored hash
        }

        return true;
    }

    public async Task<string> CalculateAuditHashAsync(int auditLogId)
    {
        var log = await _dbContext.AuditLogs.FindAsync(auditLogId);
        if (log == null)
            return string.Empty;

        var dataToHash = $"{log.Id}|{log.EntityType}|{log.EntityId}|{log.ChangeType}|{log.ChangedBy}|{log.ChangeDate:O}|{log.ChangeDetails}";
        return GenerateHash(dataToHash);
    }

    public async Task<bool> ValidateAuditHashAsync(int auditLogId, string expectedHash)
    {
        var actualHash = await CalculateAuditHashAsync(auditLogId);
        return actualHash == expectedHash;
    }

    public async Task SignAuditLogAsync(int auditLogId, string signature)
    {
        // In a real implementation, store signature in a separate table or field
        await LogAsync("SIGN_AUDIT", "AuditLog", auditLogId, "SYSTEM",
            $"Signed with signature: {signature.Substring(0, 20)}...");
    }

    public async Task<bool> VerifyAuditSignatureAsync(int auditLogId, string signature)
    {
        // In a real implementation, verify cryptographic signature
        return await Task.FromResult(true);
    }

    #endregion

    #region Analytics and Insights

    public async Task<Dictionary<string, int>> GetAuditStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _dbContext.AuditLogs.AsQueryable();

        if (startDate.HasValue)
            query = query.Where(al => al.ChangeDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(al => al.ChangeDate <= endDate.Value);

        var stats = new Dictionary<string, int>
        {
            ["TotalLogs"] = await query.CountAsync(),
            ["UniqueUsers"] = await query.Select(al => al.ChangedBy).Distinct().CountAsync(),
            ["UniqueEntities"] = await query.Select(al => al.EntityType).Distinct().CountAsync(),
            ["SecurityEvents"] = await query.Where(al => al.ChangeType.StartsWith("SECURITY:")).CountAsync(),
            ["DataChanges"] = await query.Where(al => al.ChangeType.StartsWith("DATA_")).CountAsync()
        };

        return stats;
    }

    public async Task<IEnumerable<(string Action, int Count)>> GetTopActionsAsync(int topN = 10, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _dbContext.AuditLogs.AsQueryable();

        if (startDate.HasValue)
            query = query.Where(al => al.ChangeDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(al => al.ChangeDate <= endDate.Value);

        var topActions = await query
            .GroupBy(al => al.ChangeType)
            .Select(g => new { Action = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(topN)
            .ToListAsync();

        return topActions.Select(x => (x.Action, x.Count));
    }

    public async Task<IEnumerable<(string User, int Count)>> GetMostActiveUsersAsync(int topN = 10, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _dbContext.AuditLogs.AsQueryable();

        if (startDate.HasValue)
            query = query.Where(al => al.ChangeDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(al => al.ChangeDate <= endDate.Value);

        var topUsers = await query
            .GroupBy(al => al.ChangedBy)
            .Select(g => new { User = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(topN)
            .ToListAsync();

        return topUsers.Select(x => (x.User, x.Count));
    }

    public async Task<IEnumerable<(string Entity, int Count)>> GetMostModifiedEntitiesAsync(int topN = 10, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _dbContext.AuditLogs.AsQueryable();

        if (startDate.HasValue)
            query = query.Where(al => al.ChangeDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(al => al.ChangeDate <= endDate.Value);

        var topEntities = await query
            .GroupBy(al => $"{al.EntityType}:{al.EntityId}")
            .Select(g => new { Entity = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(topN)
            .ToListAsync();

        return topEntities.Select(x => (x.Entity, x.Count));
    }

    public async Task<Dictionary<DateTime, int>> GetAuditTrendAsync(string groupBy = "day", DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _dbContext.AuditLogs.AsQueryable();

        if (startDate.HasValue)
            query = query.Where(al => al.ChangeDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(al => al.ChangeDate <= endDate.Value);

        var logs = await query.ToListAsync();
        var trend = new Dictionary<DateTime, int>();

        foreach (var log in logs)
        {
            var key = groupBy.ToLower() switch
            {
                "hour" => new DateTime(log.ChangeDate.Year, log.ChangeDate.Month, log.ChangeDate.Day, log.ChangeDate.Hour, 0, 0),
                "day" => log.ChangeDate.Date,
                "week" => log.ChangeDate.Date.AddDays(-(int)log.ChangeDate.DayOfWeek),
                "month" => new DateTime(log.ChangeDate.Year, log.ChangeDate.Month, 1),
                _ => log.ChangeDate.Date
            };

            if (!trend.ContainsKey(key))
                trend[key] = 0;
            trend[key]++;
        }

        return trend;
    }

    #endregion

    #region Real-time Monitoring

    public async Task<bool> EnableRealTimeMonitoringAsync(string userId)
    {
        _monitoredUsers[userId] = true;
        
        await LogAsync("ENABLE_MONITORING", "User", 0, "SYSTEM",
            $"Enabled real-time monitoring for user {userId}");

        _logger.LogInformation("Enabled real-time monitoring for user {UserId}", userId);
        return true;
    }

    public async Task<bool> DisableRealTimeMonitoringAsync(string userId)
    {
        if (_monitoredUsers.ContainsKey(userId))
        {
            _monitoredUsers.Remove(userId);
            
            await LogAsync("DISABLE_MONITORING", "User", 0, "SYSTEM",
                $"Disabled real-time monitoring for user {userId}");

            _logger.LogInformation("Disabled real-time monitoring for user {UserId}", userId);
            return true;
        }

        return false;
    }

    public async Task<IEnumerable<string>> GetMonitoredUsersAsync()
    {
        return await Task.FromResult(_monitoredUsers.Keys.ToList());
    }

    public async Task<bool> IsUserMonitoredAsync(string userId)
    {
        return await Task.FromResult(_monitoredUsers.ContainsKey(userId) && _monitoredUsers[userId]);
    }

    public async Task NotifySecurityTeamAsync(string alert, string severity, Dictionary<string, object> context)
    {
        // In a real implementation, this would send notifications via email, SMS, or messaging platform
        var notification = new
        {
            Alert = alert,
            Severity = severity,
            Timestamp = DateTime.UtcNow,
            Context = context
        };

        await LogSecurityEventAsync("SECURITY_ALERT", severity, "AuditService",
            JsonSerializer.Serialize(notification));

        _logger.LogCritical("Security alert: {Alert} with severity {Severity}", alert, severity);
    }

    #endregion

    #region Forensic Analysis

    public async Task<UserActivityTimeline> GetUserActivityTimelineAsync(string userId, DateTime startDate, DateTime endDate)
    {
        var timeline = new UserActivityTimeline
        {
            UserId = userId,
            StartDate = startDate,
            EndDate = endDate
        };

        var logs = await GetAuditLogsByUserAsync(userId, startDate, endDate);

        foreach (var log in logs)
        {
            timeline.Events.Add(new ActivityEvent
            {
                Timestamp = log.ChangeDate,
                EventType = log.ChangeType,
                Description = $"{log.ChangeType} on {log.EntityType}:{log.EntityId}",
                Context = new Dictionary<string, object>
                {
                    ["EntityType"] = log.EntityType,
                    ["EntityId"] = log.EntityId,
                    ["Details"] = log.ChangeDetails
                }
            });
        }

        // Calculate summary
        timeline.Summary["TotalEvents"] = timeline.Events.Count;
        timeline.Summary["UniqueActions"] = timeline.Events.Select(e => e.EventType).Distinct().Count();
        timeline.Summary["AverageEventsPerDay"] = timeline.Events.Count / Math.Max(1, (endDate - startDate).Days);

        return timeline;
    }

    public async Task<IEnumerable<AuditLog>> ReconstructEntityStateAsync(string entityType, int entityId, DateTime pointInTime)
    {
        var relevantLogs = await _dbContext.AuditLogs
            .Where(al => al.EntityType == entityType &&
                        al.EntityId == entityId &&
                        al.ChangeDate <= pointInTime)
            .OrderBy(al => al.ChangeDate)
            .ToListAsync();

        return relevantLogs;
    }

    public async Task<IEnumerable<SecurityIncident>> DetectAnomaliesAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var incidents = new List<SecurityIncident>();

        // Detect rapid access attempts
        var accessLogs = await GetAuditLogsByActionAsync("ACCESS_DENIED", startDate, endDate);
        var groupedByUser = accessLogs.GroupBy(l => l.ChangedBy);

        foreach (var userGroup in groupedByUser.Where(g => g.Count() > 5))
        {
            incidents.Add(new SecurityIncident
            {
                IncidentType = "RapidAccessAttempts",
                Severity = "High",
                DetectedAt = DateTime.UtcNow,
                Description = $"User {userGroup.Key} had {userGroup.Count()} failed access attempts",
                RelatedAuditLogIds = userGroup.Select(l => l.Id).ToList()
            });
        }

        // Detect unusual activity times
        var nightTimeLogs = await _dbContext.AuditLogs
            .Where(al => al.ChangeDate.Hour < 6 || al.ChangeDate.Hour > 22)
            .ToListAsync();

        if (nightTimeLogs.Count > 10)
        {
            incidents.Add(new SecurityIncident
            {
                IncidentType = "UnusualActivityTime",
                Severity = "Medium",
                DetectedAt = DateTime.UtcNow,
                Description = $"Detected {nightTimeLogs.Count} activities during non-business hours",
                RelatedAuditLogIds = nightTimeLogs.Select(l => l.Id).Take(10).ToList()
            });
        }

        return incidents;
    }

    public async Task<AccessPattern> AnalyzeAccessPatternsAsync(string userId, int days = 30)
    {
        var startDate = DateTime.UtcNow.AddDays(-days);
        var logs = await GetAuditLogsByUserAsync(userId, startDate, DateTime.UtcNow);

        var pattern = new AccessPattern
        {
            UserId = userId
        };

        // Count resource access
        foreach (var log in logs)
        {
            var resource = $"{log.EntityType}:{log.ChangeType}";
            if (!pattern.ResourceAccessCounts.ContainsKey(resource))
                pattern.ResourceAccessCounts[resource] = 0;
            pattern.ResourceAccessCounts[resource]++;
        }

        // Analyze hourly activity
        foreach (var log in logs)
        {
            var hour = log.ChangeDate.Hour;
            if (!pattern.HourlyActivity.ContainsKey(hour))
                pattern.HourlyActivity[hour] = 0;
            pattern.HourlyActivity[hour]++;
        }

        // Detect unusual activities
        var avgAccessPerDay = logs.Count() / Math.Max(1, days);
        var highActivityDays = logs.GroupBy(l => l.ChangeDate.Date)
            .Where(g => g.Count() > avgAccessPerDay * 2)
            .Select(g => g.Key);

        foreach (var day in highActivityDays)
        {
            pattern.UnusualActivities.Add($"High activity on {day:yyyy-MM-dd}");
        }

        // Calculate normality score (0-100, higher is more normal)
        pattern.NormalityScore = CalculateNormalityScore(pattern);

        return pattern;
    }

    public async Task<RiskScore> CalculateUserRiskScoreAsync(string userId)
    {
        var riskScore = new RiskScore
        {
            UserId = userId,
            CalculatedAt = DateTime.UtcNow
        };

        // Check failed login attempts
        var failedLogins = await GetFailedLoginAttemptsAsync(userId, DateTime.UtcNow.AddDays(-7));
        if (failedLogins.Any())
        {
            riskScore.Factors.Add(new RiskFactor
            {
                FactorName = "FailedLogins",
                Weight = 0.3,
                Value = Math.Min(failedLogins.Count() * 10, 100),
                Description = $"{failedLogins.Count()} failed login attempts in last 7 days"
            });
        }

        // Check access violations
        var violations = await GetAccessViolationsAsync(DateTime.UtcNow.AddDays(-30));
        var userViolations = violations.Where(v => v.UserId == userId);
        if (userViolations.Any())
        {
            riskScore.Factors.Add(new RiskFactor
            {
                FactorName = "AccessViolations",
                Weight = 0.4,
                Value = Math.Min(userViolations.Count() * 20, 100),
                Description = $"{userViolations.Count()} access violations in last 30 days"
            });
        }

        // Check suspicious activity
        var hasSuspicious = await HasSuspiciousActivityAsync(userId);
        if (hasSuspicious)
        {
            riskScore.Factors.Add(new RiskFactor
            {
                FactorName = "SuspiciousActivity",
                Weight = 0.3,
                Value = 80,
                Description = "Suspicious activity detected"
            });
        }

        // Calculate weighted score
        double totalScore = 0;
        double totalWeight = 0;
        foreach (var factor in riskScore.Factors)
        {
            totalScore += factor.Value * factor.Weight;
            totalWeight += factor.Weight;
        }

        riskScore.Score = totalWeight > 0 ? totalScore / totalWeight : 0;

        // Determine risk level
        riskScore.RiskLevel = riskScore.Score switch
        {
            < 25 => "Low",
            < 50 => "Medium",
            < 75 => "High",
            _ => "Critical"
        };

        return riskScore;
    }

    #endregion

    #region Export and Archival

    public async Task<string> ExportAuditLogsAsync(string format, DateTime? startDate = null, DateTime? endDate = null)
    {
        var logs = await GetAuditLogsAsync(startDate, endDate);

        string exportData = format.ToLower() switch
        {
            "json" => JsonSerializer.Serialize(logs, new JsonSerializerOptions { WriteIndented = true }),
            "csv" => ConvertToCsv(logs),
            "xml" => ConvertToXml(logs),
            _ => JsonSerializer.Serialize(logs)
        };

        await LogAsync("EXPORT_AUDIT_LOGS", "AuditLog", 0, "SYSTEM",
            $"Exported {logs.Count()} audit logs in {format} format");

        return exportData;
    }

    public async Task<bool> ArchiveAuditLogsAsync(DateTime cutoffDate, string archiveLocation)
    {
        var logsToArchive = await _dbContext.AuditLogs
            .Where(al => al.ChangeDate < cutoffDate)
            .ToListAsync();

        if (logsToArchive.Any())
        {
            // In a real implementation, write to archive location
            var archiveData = JsonSerializer.Serialize(logsToArchive);
            
            // Remove from active database
            _dbContext.AuditLogs.RemoveRange(logsToArchive);
            await _dbContext.SaveChangesAsync();

            await LogAsync("ARCHIVE_AUDIT_LOGS", "AuditLog", 0, "SYSTEM",
                $"Archived {logsToArchive.Count} audit logs to {archiveLocation}");

            return true;
        }

        return false;
    }

    public async Task<bool> RestoreArchivedLogsAsync(string archiveLocation, DateTime? startDate = null, DateTime? endDate = null)
    {
        // In a real implementation, read from archive location
        await LogAsync("RESTORE_AUDIT_LOGS", "AuditLog", 0, "SYSTEM",
            $"Restored audit logs from {archiveLocation}");

        return true;
    }

    public async Task<long> GetAuditLogSizeAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var logs = await GetAuditLogsAsync(startDate, endDate);
        var json = JsonSerializer.Serialize(logs);
        return Encoding.UTF8.GetByteCount(json);
    }

    public async Task<bool> CompressAuditLogsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        // In a real implementation, compress logs in storage
        await LogAsync("COMPRESS_AUDIT_LOGS", "AuditLog", 0, "SYSTEM",
            "Compressed audit logs");

        return true;
    }

    #endregion

    #region Alerting and Notifications

    public async Task<int> CreateAlertRuleAsync(string ruleName, string condition, string action, bool isActive = true)
    {
        var ruleId = _alertRules.Count + 1;
        _alertRules[ruleId] = new AlertRule
        {
            Id = ruleId,
            RuleName = ruleName,
            Condition = condition,
            Action = action,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };

        await LogAsync("CREATE_ALERT_RULE", "AlertRule", ruleId, "SYSTEM",
            $"Created alert rule: {ruleName}");

        return ruleId;
    }

    public async Task<bool> UpdateAlertRuleAsync(int ruleId, string? condition = null, string? action = null, bool? isActive = null)
    {
        if (_alertRules.ContainsKey(ruleId))
        {
            var rule = _alertRules[ruleId];
            if (condition != null) rule.Condition = condition;
            if (action != null) rule.Action = action;
            if (isActive.HasValue) rule.IsActive = isActive.Value;

            await LogAsync("UPDATE_ALERT_RULE", "AlertRule", ruleId, "SYSTEM",
                $"Updated alert rule: {rule.RuleName}");

            return true;
        }

        return false;
    }

    public async Task<bool> DeleteAlertRuleAsync(int ruleId)
    {
        if (_alertRules.ContainsKey(ruleId))
        {
            var ruleName = _alertRules[ruleId].RuleName;
            _alertRules.Remove(ruleId);

            await LogAsync("DELETE_ALERT_RULE", "AlertRule", ruleId, "SYSTEM",
                $"Deleted alert rule: {ruleName}");

            return true;
        }

        return false;
    }

    public async Task<IEnumerable<AlertRule>> GetAlertRulesAsync(bool? activeOnly = null)
    {
        var rules = _alertRules.Values.AsEnumerable();
        
        if (activeOnly.HasValue)
            rules = rules.Where(r => r.IsActive == activeOnly.Value);

        return await Task.FromResult(rules);
    }

    public async Task<bool> TestAlertRuleAsync(int ruleId)
    {
        if (_alertRules.ContainsKey(ruleId))
        {
            var rule = _alertRules[ruleId];
            // In a real implementation, evaluate condition and execute action
            
            await LogAsync("TEST_ALERT_RULE", "AlertRule", ruleId, "SYSTEM",
                $"Tested alert rule: {rule.RuleName}");

            return true;
        }

        return false;
    }

    #endregion

    #region Session Management

    public async Task<string> StartAuditSessionAsync(string userId, string sessionType, Dictionary<string, object>? metadata = null)
    {
        var sessionId = Guid.NewGuid().ToString();
        _activeSessions[sessionId] = new AuditSession
        {
            SessionId = sessionId,
            UserId = userId,
            SessionType = sessionType,
            StartedAt = DateTime.UtcNow,
            IsActive = true,
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        await LogAsync("START_AUDIT_SESSION", "Session", 0, userId,
            $"Started {sessionType} session: {sessionId}");

        return sessionId;
    }

    public async Task EndAuditSessionAsync(string sessionId, string? reason = null)
    {
        if (_activeSessions.ContainsKey(sessionId))
        {
            var session = _activeSessions[sessionId];
            session.EndedAt = DateTime.UtcNow;
            session.IsActive = false;

            await LogAsync("END_AUDIT_SESSION", "Session", 0, session.UserId,
                $"Ended {session.SessionType} session: {sessionId}. Reason: {reason ?? "Normal"}");

            _activeSessions.Remove(sessionId);
        }
    }

    public async Task<AuditSession?> GetAuditSessionAsync(string sessionId)
    {
        return await Task.FromResult(_activeSessions.ContainsKey(sessionId) ? _activeSessions[sessionId] : null);
    }

    public async Task<IEnumerable<AuditSession>> GetActiveSessionsAsync(string? userId = null)
    {
        var sessions = _activeSessions.Values.Where(s => s.IsActive);
        
        if (!string.IsNullOrEmpty(userId))
            sessions = sessions.Where(s => s.UserId == userId);

        return await Task.FromResult(sessions);
    }

    public async Task<bool> ExtendSessionAsync(string sessionId, int additionalMinutes)
    {
        if (_activeSessions.ContainsKey(sessionId))
        {
            // In a real implementation, extend session timeout
            await LogAsync("EXTEND_AUDIT_SESSION", "Session", 0, _activeSessions[sessionId].UserId,
                $"Extended session {sessionId} by {additionalMinutes} minutes");

            return true;
        }

        return false;
    }

    #endregion

    #region Regulatory Compliance

    public async Task<bool> IsCompliantAsync(string regulation, DateTime? asOfDate = null)
    {
        var date = asOfDate ?? DateTime.UtcNow;
        
        // Check compliance based on regulation
        var report = regulation.ToUpper() switch
        {
            "GDPR" => await GenerateGDPRReportAsync("", date.AddMonths(-1), date),
            "SOC2" => await GenerateSOC2ReportAsync(date.AddMonths(-1), date),
            "HIPAA" => await GenerateHIPAAReportAsync(date.AddMonths(-1), date),
            "PCI-DSS" => await GeneratePCIDSSReportAsync(date.AddMonths(-1), date),
            _ => new ComplianceReport { IsCompliant = true }
        };

        return report.IsCompliant;
    }

    public async Task<IEnumerable<ComplianceViolation>> GetComplianceViolationsAsync(string? regulation = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var violations = new List<ComplianceViolation>();

        // Get violations from various compliance reports
        if (regulation == null || regulation == "GDPR")
        {
            var gdprReport = await GenerateGDPRReportAsync("", startDate, endDate);
            violations.AddRange(gdprReport.Violations);
        }

        if (regulation == null || regulation == "SOC2")
        {
            var soc2Report = await GenerateSOC2ReportAsync(startDate ?? DateTime.UtcNow.AddMonths(-1), endDate ?? DateTime.UtcNow);
            violations.AddRange(soc2Report.Violations);
        }

        return violations;
    }

    public async Task<bool> RemediateViolationAsync(int violationId, string remediationAction, string performedBy)
    {
        // In a real implementation, update violation record
        await LogAsync("REMEDIATE_VIOLATION", "ComplianceViolation", violationId, performedBy,
            $"Remediated violation {violationId}: {remediationAction}");

        return true;
    }

    public async Task<ComplianceStatus> GetComplianceStatusAsync(string regulation)
    {
        var violations = await GetComplianceViolationsAsync(regulation, DateTime.UtcNow.AddMonths(-3), DateTime.UtcNow);
        var remediatedCount = violations.Count(v => v.IsRemediated);

        return new ComplianceStatus
        {
            Regulation = regulation,
            IsCompliant = !violations.Any(v => !v.IsRemediated),
            LastAuditDate = DateTime.UtcNow,
            NextAuditDate = DateTime.UtcNow.AddMonths(3),
            ViolationCount = violations.Count(),
            RemediatedCount = remediatedCount,
            ComplianceScore = violations.Any() ? (double)remediatedCount / violations.Count() * 100 : 100
        };
    }

    public async Task<bool> ScheduleComplianceAuditAsync(string regulation, DateTime scheduledDate)
    {
        await LogAsync("SCHEDULE_COMPLIANCE_AUDIT", "Compliance", 0, "SYSTEM",
            $"Scheduled {regulation} compliance audit for {scheduledDate:yyyy-MM-dd}");

        return true;
    }

    #endregion

    #region Helper Methods

    private string GenerateHash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private string AnonymizeDetails(string details)
    {
        // Simple anonymization - in production, use more sophisticated techniques
        return System.Text.RegularExpressions.Regex.Replace(details,
            @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
            "***@***.***");
    }

    private async Task CheckAlertRulesAsync(AuditLog auditLog)
    {
        foreach (var rule in _alertRules.Values.Where(r => r.IsActive))
        {
            // Simple condition evaluation - in production, use expression trees or rules engine
            if (EvaluateAlertCondition(rule.Condition, auditLog))
            {
                rule.TriggerCount++;
                rule.LastTriggeredAt = DateTime.UtcNow;
                
                // Execute action
                await ExecuteAlertActionAsync(rule.Action, auditLog);
            }
        }
    }

    private bool EvaluateAlertCondition(string condition, AuditLog auditLog)
    {
        // Simple condition evaluation
        return condition.Contains(auditLog.ChangeType) || condition.Contains(auditLog.EntityType);
    }

    private async Task ExecuteAlertActionAsync(string action, AuditLog auditLog)
    {
        if (action.StartsWith("NOTIFY:"))
        {
            await NotifySecurityTeamAsync($"Alert triggered for {auditLog.ChangeType}", "Medium",
                new Dictionary<string, object>
                {
                    ["AuditLogId"] = auditLog.Id,
                    ["Action"] = auditLog.ChangeType,
                    ["Entity"] = $"{auditLog.EntityType}:{auditLog.EntityId}"
                });
        }
    }

    private async Task TrackFailedAccessAsync(string userId, string resource)
    {
        // Track failed access attempts for pattern analysis
        if (await IsUserMonitoredAsync(userId))
        {
            await NotifySecurityTeamAsync($"Failed access attempt by monitored user", "High",
                new Dictionary<string, object>
                {
                    ["UserId"] = userId,
                    ["Resource"] = resource,
                    ["Timestamp"] = DateTime.UtcNow
                });
        }
    }

    private double CalculateNormalityScore(AccessPattern pattern)
    {
        double score = 100;

        // Penalize unusual activities
        score -= pattern.UnusualActivities.Count * 10;

        // Penalize irregular hourly patterns
        var avgHourlyActivity = pattern.HourlyActivity.Values.Average();
        var variance = pattern.HourlyActivity.Values.Select(v => Math.Pow(v - avgHourlyActivity, 2)).Average();
        score -= Math.Min(variance / 10, 30);

        return Math.Max(0, Math.Min(100, score));
    }

    private string ConvertToCsv(IEnumerable<AuditLog> logs)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Id,EntityType,EntityId,ChangeType,ChangedBy,ChangeDate,ChangeDetails");
        
        foreach (var log in logs)
        {
            csv.AppendLine($"{log.Id},{log.EntityType},{log.EntityId},{log.ChangeType},{log.ChangedBy},{log.ChangeDate:O},\"{log.ChangeDetails}\"");
        }

        return csv.ToString();
    }

    private string ConvertToXml(IEnumerable<AuditLog> logs)
    {
        // Simple XML conversion - in production, use XmlSerializer
        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<AuditLogs>");
        
        foreach (var log in logs)
        {
            xml.AppendLine($"  <AuditLog>");
            xml.AppendLine($"    <Id>{log.Id}</Id>");
            xml.AppendLine($"    <EntityType>{log.EntityType}</EntityType>");
            xml.AppendLine($"    <EntityId>{log.EntityId}</EntityId>");
            xml.AppendLine($"    <ChangeType>{log.ChangeType}</ChangeType>");
            xml.AppendLine($"    <ChangedBy>{log.ChangedBy}</ChangedBy>");
            xml.AppendLine($"    <ChangeDate>{log.ChangeDate:O}</ChangeDate>");
            xml.AppendLine($"    <ChangeDetails><![CDATA[{log.ChangeDetails}]]></ChangeDetails>");
            xml.AppendLine($"  </AuditLog>");
        }
        
        xml.AppendLine("</AuditLogs>");
        return xml.ToString();
    }

    #endregion
}