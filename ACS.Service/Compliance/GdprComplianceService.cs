using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ACS.Service.Data;

namespace ACS.Service.Compliance;

/// <summary>
/// Service for handling GDPR compliance requirements
/// </summary>
public interface IGdprComplianceService
{
    // Data Subject Rights
    Task<DataPortabilityResult> ExportUserDataAsync(string userId, ExportFormat format, CancellationToken cancellationToken = default);
    Task<DataErasureResult> EraseUserDataAsync(string userId, bool hardDelete = false, CancellationToken cancellationToken = default);
    Task<DataRectificationResult> RectifyUserDataAsync(string userId, Dictionary<string, object> corrections, CancellationToken cancellationToken = default);
    Task<DataProcessingInfo> GetDataProcessingInfoAsync(string userId, CancellationToken cancellationToken = default);
    
    // Consent Management
    Task<ConsentRecord> RecordConsentAsync(ConsentRequest request, CancellationToken cancellationToken = default);
    Task<bool> WithdrawConsentAsync(string userId, string purposeId, CancellationToken cancellationToken = default);
    Task<List<ConsentRecord>> GetUserConsentsAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> HasValidConsentAsync(string userId, string purposeId, CancellationToken cancellationToken = default);
    
    // Data Minimization
    Task<DataMinimizationResult> MinimizeUserDataAsync(string userId, CancellationToken cancellationToken = default);
    Task<List<string>> IdentifyExcessiveDataAsync(string userId, CancellationToken cancellationToken = default);
    
    // Privacy by Design
    Task<bool> PseudonymizeUserDataAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> AnonymizeUserDataAsync(string userId, CancellationToken cancellationToken = default);
    
    // Breach Notification
    Task<DataBreachRecord> ReportDataBreachAsync(DataBreachInfo breach, CancellationToken cancellationToken = default);
    Task<List<DataBreachRecord>> GetDataBreachesAsync(DateTime? since = null, CancellationToken cancellationToken = default);
}

public class GdprComplianceService : IGdprComplianceService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<GdprComplianceService> _logger;
    private readonly IComplianceAuditService _auditService;
    private readonly IEncryptionService _encryptionService;

    public GdprComplianceService(
        ApplicationDbContext context,
        ILogger<GdprComplianceService> logger,
        IComplianceAuditService auditService,
        IEncryptionService encryptionService)
    {
        _context = context;
        _logger = logger;
        _auditService = auditService;
        _encryptionService = encryptionService;
    }

    #region Data Subject Rights

    public async Task<DataPortabilityResult> ExportUserDataAsync(string userId, ExportFormat format, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Exporting user data for {UserId} in {Format} format", userId, format);

        try
        {
            // Collect all user data from various tables
            var userData = new UserDataExport
            {
                ExportId = Guid.NewGuid().ToString(),
                UserId = userId,
                ExportDate = DateTime.UtcNow,
                Format = format
            };

            // Get user profile
            var user = await _context.Users
                .Include(u => u.Roles)
                .Include(u => u.Groups)
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (user == null)
            {
                return new DataPortabilityResult
                {
                    Success = false,
                    Message = "User not found"
                };
            }

            userData.Profile = new UserProfile
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                Roles = user.Roles.Select(r => r.Name).ToList(),
                Groups = user.Groups.Select(g => g.Name).ToList()
            };

            // Get user permissions
            var permissions = await _context.UserPermissions
                .Where(up => up.UserId == userId)
                .Select(up => new PermissionData
                {
                    ResourceId = up.ResourceId,
                    Permission = up.Permission,
                    GrantedAt = up.GrantedAt
                })
                .ToListAsync(cancellationToken);

            userData.Permissions = permissions;

            // Get audit logs
            var auditLogs = await _context.AuditLogs
                .Where(al => al.UserId == userId)
                .OrderBy(al => al.Timestamp)
                .Select(al => new AuditLogData
                {
                    Action = al.Action,
                    Resource = al.Resource,
                    Timestamp = al.Timestamp,
                    IpAddress = AnonymizeIpAddress(al.IpAddress),
                    UserAgent = al.UserAgent
                })
                .ToListAsync(cancellationToken);

            userData.AuditLogs = auditLogs;

            // Get consent records
            var consents = await _context.ConsentRecords
                .Where(cr => cr.UserId == userId)
                .Select(cr => new ConsentData
                {
                    PurposeId = cr.PurposeId,
                    Purpose = cr.Purpose,
                    ConsentedAt = cr.ConsentedAt,
                    ExpiresAt = cr.ExpiresAt,
                    Withdrawn = cr.Withdrawn
                })
                .ToListAsync(cancellationToken);

            userData.Consents = consents;

            // Export in requested format
            byte[] exportData;
            string fileName;

            switch (format)
            {
                case ExportFormat.Json:
                    exportData = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(userData, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
                    fileName = $"user_data_{userId}_{DateTime.UtcNow:yyyyMMdd}.json";
                    break;

                case ExportFormat.Csv:
                    exportData = ExportToCsv(userData);
                    fileName = $"user_data_{userId}_{DateTime.UtcNow:yyyyMMdd}.csv";
                    break;

                case ExportFormat.Xml:
                    exportData = ExportToXml(userData);
                    fileName = $"user_data_{userId}_{DateTime.UtcNow:yyyyMMdd}.xml";
                    break;

                default:
                    throw new NotSupportedException($"Export format {format} not supported");
            }

            // Audit the export
            await _auditService.LogComplianceEventAsync(new ComplianceAuditEvent
            {
                EventType = ComplianceEventType.DataExport,
                UserId = userId,
                Description = $"User data exported in {format} format",
                Metadata = new Dictionary<string, string>
                {
                    ["ExportId"] = userData.ExportId,
                    ["Format"] = format.ToString(),
                    ["RecordCount"] = (userData.AuditLogs.Count + userData.Permissions.Count + userData.Consents.Count).ToString()
                }
            });

            return new DataPortabilityResult
            {
                Success = true,
                ExportId = userData.ExportId,
                Data = exportData,
                FileName = fileName,
                Format = format,
                ExportDate = userData.ExportDate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting user data for {UserId}", userId);
            return new DataPortabilityResult
            {
                Success = false,
                Message = "Error exporting user data",
                Error = ex.Message
            };
        }
    }

    public async Task<DataErasureResult> EraseUserDataAsync(string userId, bool hardDelete = false, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Erasing user data for {UserId} (HardDelete: {HardDelete})", userId, hardDelete);

        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var result = new DataErasureResult
            {
                UserId = userId,
                ErasureDate = DateTime.UtcNow,
                HardDelete = hardDelete
            };

            // Check if user exists
            var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
            if (user == null)
            {
                result.Success = false;
                result.Message = "User not found";
                return result;
            }

            // Check for legal holds or retention requirements
            var hasLegalHold = await _context.LegalHolds
                .AnyAsync(lh => lh.UserId == userId && lh.Active, cancellationToken);

            if (hasLegalHold)
            {
                result.Success = false;
                result.Message = "Cannot erase user data due to legal hold";
                return result;
            }

            if (hardDelete)
            {
                // Hard delete - permanently remove all data
                
                // Delete audit logs
                var auditLogs = await _context.AuditLogs
                    .Where(al => al.UserId == userId)
                    .ExecuteDeleteAsync(cancellationToken);
                result.RecordsDeleted["AuditLogs"] = auditLogs;

                // Delete permissions
                var permissions = await _context.UserPermissions
                    .Where(up => up.UserId == userId)
                    .ExecuteDeleteAsync(cancellationToken);
                result.RecordsDeleted["Permissions"] = permissions;

                // Delete consent records
                var consents = await _context.ConsentRecords
                    .Where(cr => cr.UserId == userId)
                    .ExecuteDeleteAsync(cancellationToken);
                result.RecordsDeleted["Consents"] = consents;

                // Delete user
                _context.Users.Remove(user);
                await _context.SaveChangesAsync(cancellationToken);
                result.RecordsDeleted["User"] = 1;
            }
            else
            {
                // Soft delete - anonymize/pseudonymize data
                
                // Anonymize user data
                user.Email = $"deleted_{Guid.NewGuid():N}@anonymous.local";
                user.Name = "Deleted User";
                user.IsDeleted = true;
                user.DeletedAt = DateTime.UtcNow;
                
                // Clear any PII from custom fields
                if (!string.IsNullOrEmpty(user.Metadata))
                {
                    user.Metadata = "{}";
                }

                // Anonymize audit logs
                var auditLogs = await _context.AuditLogs
                    .Where(al => al.UserId == userId)
                    .ToListAsync(cancellationToken);

                foreach (var log in auditLogs)
                {
                    log.UserId = $"anonymous_{GenerateHash(userId)}";
                    log.IpAddress = "0.0.0.0";
                    log.UserAgent = "Anonymized";
                }

                result.RecordsAnonymized["AuditLogs"] = auditLogs.Count;

                await _context.SaveChangesAsync(cancellationToken);
                result.RecordsAnonymized["User"] = 1;
            }

            await transaction.CommitAsync(cancellationToken);

            // Audit the erasure
            await _auditService.LogComplianceEventAsync(new ComplianceAuditEvent
            {
                EventType = ComplianceEventType.DataErasure,
                UserId = hardDelete ? "ERASED" : userId,
                Description = $"User data {(hardDelete ? "permanently deleted" : "anonymized")}",
                Metadata = new Dictionary<string, string>
                {
                    ["OriginalUserId"] = userId,
                    ["HardDelete"] = hardDelete.ToString(),
                    ["RecordsAffected"] = JsonSerializer.Serialize(hardDelete ? result.RecordsDeleted : result.RecordsAnonymized)
                }
            });

            result.Success = true;
            result.Message = hardDelete ? "User data permanently deleted" : "User data anonymized";

            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error erasing user data for {UserId}", userId);

            return new DataErasureResult
            {
                Success = false,
                Message = "Error erasing user data",
                Error = ex.Message
            };
        }
    }

    public async Task<DataRectificationResult> RectifyUserDataAsync(string userId, Dictionary<string, object> corrections, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Rectifying user data for {UserId}", userId);

        try
        {
            var result = new DataRectificationResult
            {
                UserId = userId,
                RectificationDate = DateTime.UtcNow
            };

            var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
            if (user == null)
            {
                result.Success = false;
                result.Message = "User not found";
                return result;
            }

            // Track original values for audit
            var originalValues = new Dictionary<string, object>();

            // Apply corrections
            foreach (var correction in corrections)
            {
                var property = user.GetType().GetProperty(correction.Key);
                if (property != null && property.CanWrite)
                {
                    originalValues[correction.Key] = property.GetValue(user);
                    property.SetValue(user, correction.Value);
                    result.FieldsUpdated.Add(correction.Key);
                }
                else
                {
                    result.FieldsSkipped.Add(correction.Key);
                }
            }

            if (result.FieldsUpdated.Any())
            {
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);

                // Audit the rectification
                await _auditService.LogComplianceEventAsync(new ComplianceAuditEvent
                {
                    EventType = ComplianceEventType.DataRectification,
                    UserId = userId,
                    Description = "User data rectified",
                    Metadata = new Dictionary<string, string>
                    {
                        ["FieldsUpdated"] = string.Join(", ", result.FieldsUpdated),
                        ["OriginalValues"] = JsonSerializer.Serialize(originalValues),
                        ["NewValues"] = JsonSerializer.Serialize(corrections)
                    }
                });

                result.Success = true;
                result.Message = $"Successfully updated {result.FieldsUpdated.Count} fields";
            }
            else
            {
                result.Success = false;
                result.Message = "No valid fields to update";
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rectifying user data for {UserId}", userId);
            return new DataRectificationResult
            {
                Success = false,
                Message = "Error rectifying user data",
                Error = ex.Message
            };
        }
    }

    public async Task<DataProcessingInfo> GetDataProcessingInfoAsync(string userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting data processing info for {UserId}", userId);

        var info = new DataProcessingInfo
        {
            UserId = userId,
            GeneratedAt = DateTime.UtcNow
        };

        // Get all tables that contain user data
        info.DataLocations = new List<DataLocation>
        {
            new() { Table = "Users", Purpose = "User profile and authentication", RetentionPeriod = "Until account deletion" },
            new() { Table = "AuditLogs", Purpose = "Security and compliance auditing", RetentionPeriod = "7 years" },
            new() { Table = "UserPermissions", Purpose = "Access control", RetentionPeriod = "Until revoked" },
            new() { Table = "ConsentRecords", Purpose = "Consent tracking", RetentionPeriod = "Until withdrawn + 3 years" }
        };

        // Get processing purposes
        info.ProcessingPurposes = new List<ProcessingPurpose>
        {
            new() { Purpose = "Authentication", LegalBasis = "Contract", Description = "User authentication and session management" },
            new() { Purpose = "Authorization", LegalBasis = "Contract", Description = "Access control and permission management" },
            new() { Purpose = "Audit Logging", LegalBasis = "Legal Obligation", Description = "Security and compliance auditing" },
            new() { Purpose = "Analytics", LegalBasis = "Legitimate Interest", Description = "System usage analytics and optimization" }
        };

        // Get data sharing info
        info.DataSharing = new List<DataSharingInfo>
        {
            new() { Recipient = "Internal Systems", Purpose = "Service Delivery", DataShared = "User ID, Permissions" },
            new() { Recipient = "Backup Systems", Purpose = "Data Recovery", DataShared = "All user data" }
        };

        // Get user rights
        info.UserRights = new List<string>
        {
            "Right to Access - Request a copy of your personal data",
            "Right to Rectification - Correct inaccurate personal data",
            "Right to Erasure - Request deletion of your personal data",
            "Right to Portability - Receive your data in a portable format",
            "Right to Restriction - Limit processing of your personal data",
            "Right to Object - Object to certain types of processing",
            "Right to Withdraw Consent - Withdraw previously given consent"
        };

        return info;
    }

    #endregion

    #region Consent Management

    public async Task<ConsentRecord> RecordConsentAsync(ConsentRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Recording consent for {UserId} for purpose {PurposeId}", request.UserId, request.PurposeId);

        var consent = new ConsentRecord
        {
            Id = Guid.NewGuid().ToString(),
            UserId = request.UserId,
            PurposeId = request.PurposeId,
            Purpose = request.Purpose,
            ConsentedAt = DateTime.UtcNow,
            ExpiresAt = request.ExpiresAt,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent,
            ConsentMethod = request.ConsentMethod,
            Withdrawn = false
        };

        _context.ConsentRecords.Add(consent);
        await _context.SaveChangesAsync(cancellationToken);

        // Audit consent
        await _auditService.LogComplianceEventAsync(new ComplianceAuditEvent
        {
            EventType = ComplianceEventType.ConsentGranted,
            UserId = request.UserId,
            Description = $"Consent granted for {request.Purpose}",
            Metadata = new Dictionary<string, string>
            {
                ["PurposeId"] = request.PurposeId,
                ["ConsentMethod"] = request.ConsentMethod,
                ["ExpiresAt"] = request.ExpiresAt?.ToString("O") ?? "Never"
            }
        });

        return consent;
    }

    public async Task<bool> WithdrawConsentAsync(string userId, string purposeId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Withdrawing consent for {UserId} for purpose {PurposeId}", userId, purposeId);

        var consent = await _context.ConsentRecords
            .FirstOrDefaultAsync(cr => cr.UserId == userId && cr.PurposeId == purposeId && !cr.Withdrawn, cancellationToken);

        if (consent == null)
        {
            return false;
        }

        consent.Withdrawn = true;
        consent.WithdrawnAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        // Audit withdrawal
        await _auditService.LogComplianceEventAsync(new ComplianceAuditEvent
        {
            EventType = ComplianceEventType.ConsentWithdrawn,
            UserId = userId,
            Description = $"Consent withdrawn for {consent.Purpose}",
            Metadata = new Dictionary<string, string>
            {
                ["PurposeId"] = purposeId,
                ["ConsentedAt"] = consent.ConsentedAt.ToString("O"),
                ["WithdrawnAt"] = consent.WithdrawnAt.Value.ToString("O")
            }
        });

        return true;
    }

    public async Task<List<ConsentRecord>> GetUserConsentsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.ConsentRecords
            .Where(cr => cr.UserId == userId)
            .OrderByDescending(cr => cr.ConsentedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasValidConsentAsync(string userId, string purposeId, CancellationToken cancellationToken = default)
    {
        return await _context.ConsentRecords
            .AnyAsync(cr => 
                cr.UserId == userId && 
                cr.PurposeId == purposeId && 
                !cr.Withdrawn &&
                (cr.ExpiresAt == null || cr.ExpiresAt > DateTime.UtcNow), 
                cancellationToken);
    }

    #endregion

    #region Data Minimization

    public async Task<DataMinimizationResult> MinimizeUserDataAsync(string userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Minimizing user data for {UserId}", userId);

        var result = new DataMinimizationResult
        {
            UserId = userId,
            MinimizationDate = DateTime.UtcNow
        };

        try
        {
            // Remove old audit logs (keep only last 90 days for active processing)
            var cutoffDate = DateTime.UtcNow.AddDays(-90);
            var oldAuditLogs = await _context.AuditLogs
                .Where(al => al.UserId == userId && al.Timestamp < cutoffDate)
                .ExecuteDeleteAsync(cancellationToken);
            
            result.RecordsRemoved["AuditLogs"] = oldAuditLogs;

            // Remove expired consents
            var expiredConsents = await _context.ConsentRecords
                .Where(cr => cr.UserId == userId && cr.ExpiresAt < DateTime.UtcNow)
                .ExecuteDeleteAsync(cancellationToken);
            
            result.RecordsRemoved["ExpiredConsents"] = expiredConsents;

            // Clear unnecessary metadata
            var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
            if (user != null && !string.IsNullOrEmpty(user.Metadata))
            {
                var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(user.Metadata);
                var minimizedMetadata = new Dictionary<string, object>();
                
                // Keep only essential metadata
                var essentialKeys = new[] { "locale", "timezone", "preferences" };
                foreach (var key in essentialKeys)
                {
                    if (metadata.ContainsKey(key))
                    {
                        minimizedMetadata[key] = metadata[key];
                    }
                }

                user.Metadata = JsonSerializer.Serialize(minimizedMetadata);
                await _context.SaveChangesAsync(cancellationToken);
                
                result.FieldsMinimized.Add("Metadata");
            }

            result.Success = true;
            result.Message = "Data minimization completed";

            // Audit minimization
            await _auditService.LogComplianceEventAsync(new ComplianceAuditEvent
            {
                EventType = ComplianceEventType.DataMinimization,
                UserId = userId,
                Description = "User data minimized",
                Metadata = new Dictionary<string, string>
                {
                    ["RecordsRemoved"] = JsonSerializer.Serialize(result.RecordsRemoved),
                    ["FieldsMinimized"] = string.Join(", ", result.FieldsMinimized)
                }
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error minimizing user data for {UserId}", userId);
            result.Success = false;
            result.Message = "Error minimizing user data";
            result.Error = ex.Message;
            return result;
        }
    }

    public async Task<List<string>> IdentifyExcessiveDataAsync(string userId, CancellationToken cancellationToken = default)
    {
        var excessiveData = new List<string>();

        // Check for old audit logs
        var oldAuditLogsCount = await _context.AuditLogs
            .CountAsync(al => al.UserId == userId && al.Timestamp < DateTime.UtcNow.AddDays(-90), cancellationToken);
        
        if (oldAuditLogsCount > 0)
        {
            excessiveData.Add($"{oldAuditLogsCount} audit logs older than 90 days");
        }

        // Check for expired consents
        var expiredConsentsCount = await _context.ConsentRecords
            .CountAsync(cr => cr.UserId == userId && cr.ExpiresAt < DateTime.UtcNow, cancellationToken);
        
        if (expiredConsentsCount > 0)
        {
            excessiveData.Add($"{expiredConsentsCount} expired consent records");
        }

        // Check for unnecessary metadata
        var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user != null && !string.IsNullOrEmpty(user.Metadata))
        {
            var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(user.Metadata);
            var unnecessaryKeys = metadata.Keys
                .Except(new[] { "locale", "timezone", "preferences" })
                .ToList();
            
            if (unnecessaryKeys.Any())
            {
                excessiveData.Add($"Unnecessary metadata fields: {string.Join(", ", unnecessaryKeys)}");
            }
        }

        return excessiveData;
    }

    #endregion

    #region Privacy by Design

    public async Task<bool> PseudonymizeUserDataAsync(string userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Pseudonymizing user data for {UserId}", userId);

        try
        {
            var pseudonym = GeneratePseudonym(userId);
            
            // Update user record
            var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
            if (user == null) return false;

            user.PseudonymId = pseudonym;
            user.IsPseudonymized = true;

            // Pseudonymize audit logs
            var auditLogs = await _context.AuditLogs
                .Where(al => al.UserId == userId)
                .ToListAsync(cancellationToken);

            foreach (var log in auditLogs)
            {
                log.UserId = pseudonym;
            }

            await _context.SaveChangesAsync(cancellationToken);

            // Audit pseudonymization
            await _auditService.LogComplianceEventAsync(new ComplianceAuditEvent
            {
                EventType = ComplianceEventType.DataPseudonymization,
                UserId = pseudonym,
                Description = "User data pseudonymized",
                Metadata = new Dictionary<string, string>
                {
                    ["OriginalUserId"] = userId,
                    ["Pseudonym"] = pseudonym
                }
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pseudonymizing user data for {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> AnonymizeUserDataAsync(string userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Anonymizing user data for {UserId}", userId);

        try
        {
            // This is similar to soft delete but more thorough
            var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
            if (user == null) return false;

            // Generate anonymous identifier
            var anonymousId = $"anon_{Guid.NewGuid():N}";

            // Anonymize user data
            user.Id = anonymousId;
            user.Email = $"{anonymousId}@anonymous.local";
            user.Name = "Anonymous User";
            user.Metadata = "{}";
            user.IsAnonymized = true;
            user.AnonymizedAt = DateTime.UtcNow;

            // Anonymize related records
            var auditLogs = await _context.AuditLogs
                .Where(al => al.UserId == userId)
                .ToListAsync(cancellationToken);

            foreach (var log in auditLogs)
            {
                log.UserId = anonymousId;
                log.IpAddress = "0.0.0.0";
                log.UserAgent = "Anonymized";
            }

            await _context.SaveChangesAsync(cancellationToken);

            // Audit anonymization
            await _auditService.LogComplianceEventAsync(new ComplianceAuditEvent
            {
                EventType = ComplianceEventType.DataAnonymization,
                UserId = anonymousId,
                Description = "User data anonymized",
                Metadata = new Dictionary<string, string>
                {
                    ["AnonymousId"] = anonymousId
                }
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error anonymizing user data for {UserId}", userId);
            return false;
        }
    }

    #endregion

    #region Breach Notification

    public async Task<DataBreachRecord> ReportDataBreachAsync(DataBreachInfo breach, CancellationToken cancellationToken = default)
    {
        _logger.LogCritical("Data breach reported: {Description}", breach.Description);

        var record = new DataBreachRecord
        {
            Id = Guid.NewGuid().ToString(),
            IncidentDate = breach.IncidentDate,
            DiscoveryDate = breach.DiscoveryDate,
            ReportedDate = DateTime.UtcNow,
            Description = breach.Description,
            DataTypes = breach.DataTypes,
            AffectedUsers = breach.AffectedUsers,
            Severity = breach.Severity,
            MitigationSteps = breach.MitigationSteps,
            NotificationSent = false
        };

        _context.DataBreaches.Add(record);
        await _context.SaveChangesAsync(cancellationToken);

        // Check if notification is required (within 72 hours for GDPR)
        var hoursSinceDiscovery = (DateTime.UtcNow - breach.DiscoveryDate).TotalHours;
        if (hoursSinceDiscovery < 72)
        {
            record.NotificationDeadline = breach.DiscoveryDate.AddHours(72);
        }

        // Audit breach report
        await _auditService.LogComplianceEventAsync(new ComplianceAuditEvent
        {
            EventType = ComplianceEventType.DataBreach,
            UserId = "SYSTEM",
            Description = "Data breach reported",
            Metadata = new Dictionary<string, string>
            {
                ["BreachId"] = record.Id,
                ["Severity"] = breach.Severity,
                ["AffectedUsers"] = breach.AffectedUsers.Count.ToString(),
                ["DataTypes"] = string.Join(", ", breach.DataTypes)
            }
        });

        return record;
    }

    public async Task<List<DataBreachRecord>> GetDataBreachesAsync(DateTime? since = null, CancellationToken cancellationToken = default)
    {
        var query = _context.DataBreaches.AsQueryable();

        if (since.HasValue)
        {
            query = query.Where(db => db.ReportedDate >= since.Value);
        }

        return await query
            .OrderByDescending(db => db.ReportedDate)
            .ToListAsync(cancellationToken);
    }

    #endregion

    #region Helper Methods

    private string AnonymizeIpAddress(string ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress))
            return "0.0.0.0";

        // For IPv4, zero out last octet
        if (ipAddress.Contains('.'))
        {
            var parts = ipAddress.Split('.');
            if (parts.Length == 4)
            {
                parts[3] = "0";
                return string.Join('.', parts);
            }
        }
        // For IPv6, zero out last 64 bits
        else if (ipAddress.Contains(':'))
        {
            var parts = ipAddress.Split(':');
            if (parts.Length >= 4)
            {
                for (int i = 4; i < parts.Length; i++)
                {
                    parts[i] = "0";
                }
                return string.Join(':', parts);
            }
        }

        return "0.0.0.0";
    }

    private string GenerateHash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }

    private string GeneratePseudonym(string userId)
    {
        var salt = "ACS_GDPR_SALT"; // In production, use a secure random salt
        return $"pseudo_{GenerateHash(userId + salt).Substring(0, 16)}";
    }

    private byte[] ExportToCsv(UserDataExport data)
    {
        var csv = new StringBuilder();
        
        // Profile
        csv.AppendLine("Profile");
        csv.AppendLine("Field,Value");
        csv.AppendLine($"Id,{data.Profile.Id}");
        csv.AppendLine($"Name,{data.Profile.Name}");
        csv.AppendLine($"Email,{data.Profile.Email}");
        csv.AppendLine($"CreatedAt,{data.Profile.CreatedAt}");
        csv.AppendLine($"Roles,\"{string.Join(", ", data.Profile.Roles)}\"");
        csv.AppendLine($"Groups,\"{string.Join(", ", data.Profile.Groups)}\"");
        csv.AppendLine();

        // Permissions
        csv.AppendLine("Permissions");
        csv.AppendLine("Resource,Permission,GrantedAt");
        foreach (var perm in data.Permissions)
        {
            csv.AppendLine($"{perm.ResourceId},{perm.Permission},{perm.GrantedAt}");
        }
        csv.AppendLine();

        // Audit Logs
        csv.AppendLine("Audit Logs");
        csv.AppendLine("Action,Resource,Timestamp");
        foreach (var log in data.AuditLogs)
        {
            csv.AppendLine($"{log.Action},{log.Resource},{log.Timestamp}");
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    private byte[] ExportToXml(UserDataExport data)
    {
        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<UserDataExport>");
        xml.AppendLine($"  <ExportId>{data.ExportId}</ExportId>");
        xml.AppendLine($"  <UserId>{data.UserId}</UserId>");
        xml.AppendLine($"  <ExportDate>{data.ExportDate:O}</ExportDate>");
        
        // Profile
        xml.AppendLine("  <Profile>");
        xml.AppendLine($"    <Id>{data.Profile.Id}</Id>");
        xml.AppendLine($"    <Name>{data.Profile.Name}</Name>");
        xml.AppendLine($"    <Email>{data.Profile.Email}</Email>");
        xml.AppendLine($"    <CreatedAt>{data.Profile.CreatedAt:O}</CreatedAt>");
        xml.AppendLine("    <Roles>");
        foreach (var role in data.Profile.Roles)
        {
            xml.AppendLine($"      <Role>{role}</Role>");
        }
        xml.AppendLine("    </Roles>");
        xml.AppendLine("  </Profile>");

        // Add other sections...
        xml.AppendLine("</UserDataExport>");

        return Encoding.UTF8.GetBytes(xml.ToString());
    }

    #endregion
}

#region Models

public class DataPortabilityResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string Error { get; set; }
    public string ExportId { get; set; }
    public byte[] Data { get; set; }
    public string FileName { get; set; }
    public ExportFormat Format { get; set; }
    public DateTime ExportDate { get; set; }
}

public class DataErasureResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string Error { get; set; }
    public string UserId { get; set; }
    public DateTime ErasureDate { get; set; }
    public bool HardDelete { get; set; }
    public Dictionary<string, int> RecordsDeleted { get; set; } = new();
    public Dictionary<string, int> RecordsAnonymized { get; set; } = new();
}

public class DataRectificationResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string Error { get; set; }
    public string UserId { get; set; }
    public DateTime RectificationDate { get; set; }
    public List<string> FieldsUpdated { get; set; } = new();
    public List<string> FieldsSkipped { get; set; } = new();
}

public class DataMinimizationResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string Error { get; set; }
    public string UserId { get; set; }
    public DateTime MinimizationDate { get; set; }
    public Dictionary<string, int> RecordsRemoved { get; set; } = new();
    public List<string> FieldsMinimized { get; set; } = new();
}

public class UserDataExport
{
    public string ExportId { get; set; }
    public string UserId { get; set; }
    public DateTime ExportDate { get; set; }
    public ExportFormat Format { get; set; }
    public UserProfile Profile { get; set; }
    public List<PermissionData> Permissions { get; set; }
    public List<AuditLogData> AuditLogs { get; set; }
    public List<ConsentData> Consents { get; set; }
}

public class UserProfile
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<string> Roles { get; set; }
    public List<string> Groups { get; set; }
}

public class PermissionData
{
    public string ResourceId { get; set; }
    public string Permission { get; set; }
    public DateTime GrantedAt { get; set; }
}

public class AuditLogData
{
    public string Action { get; set; }
    public string Resource { get; set; }
    public DateTime Timestamp { get; set; }
    public string IpAddress { get; set; }
    public string UserAgent { get; set; }
}

public class ConsentData
{
    public string PurposeId { get; set; }
    public string Purpose { get; set; }
    public DateTime ConsentedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool Withdrawn { get; set; }
}

public class ConsentRequest
{
    public string UserId { get; set; }
    public string PurposeId { get; set; }
    public string Purpose { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string IpAddress { get; set; }
    public string UserAgent { get; set; }
    public string ConsentMethod { get; set; }
}

public class DataProcessingInfo
{
    public string UserId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<DataLocation> DataLocations { get; set; }
    public List<ProcessingPurpose> ProcessingPurposes { get; set; }
    public List<DataSharingInfo> DataSharing { get; set; }
    public List<string> UserRights { get; set; }
}

public class DataLocation
{
    public string Table { get; set; }
    public string Purpose { get; set; }
    public string RetentionPeriod { get; set; }
}

public class ProcessingPurpose
{
    public string Purpose { get; set; }
    public string LegalBasis { get; set; }
    public string Description { get; set; }
}

public class DataSharingInfo
{
    public string Recipient { get; set; }
    public string Purpose { get; set; }
    public string DataShared { get; set; }
}

public class DataBreachInfo
{
    public DateTime IncidentDate { get; set; }
    public DateTime DiscoveryDate { get; set; }
    public string Description { get; set; }
    public List<string> DataTypes { get; set; }
    public List<string> AffectedUsers { get; set; }
    public string Severity { get; set; }
    public List<string> MitigationSteps { get; set; }
}

#endregion