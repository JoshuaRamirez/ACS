using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ACS.Service.Data;
using ACS.Service.Domain;

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
            if (!int.TryParse(userId, out int userIdInt))
            {
                return new DataPortabilityResult
                {
                    Success = false,
                    Message = "Invalid user ID format"
                };
            }

            var user = await _context.Users
                .Include(u => u.Entity)
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Include(u => u.UserGroups).ThenInclude(ug => ug.Group)
                .FirstOrDefaultAsync(u => u.Id == userIdInt, cancellationToken);

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
                Id = user.Id.ToString(),
                Name = user.Name,
                Email = user.Email,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                Roles = user.Roles.Select(r => r.Name).ToList(),
                Groups = user.Groups.Select(g => g.Name).ToList()
            };

            // Get user permissions through roles and groups - simplified for now
            var permissions = new List<PermissionData>();
            
            // Add role-based permissions (simplified)
            foreach (var userRole in user.UserRoles)
            {
                permissions.Add(new PermissionData
                {
                    ResourceId = userRole.RoleId.ToString(),
                    Permission = $"Role: {userRole.Role?.Name ?? "Unknown"}",
                    GrantedAt = DateTime.UtcNow
                });
            }
            
            // Add group-based permissions (simplified)
            foreach (var userGroup in user.UserGroups)
            {
                permissions.Add(new PermissionData
                {
                    ResourceId = userGroup.GroupId.ToString(),
                    Permission = $"Group: {userGroup.Group?.Name ?? "Unknown"}",
                    GrantedAt = DateTime.UtcNow
                });
            }

            userData.Permissions = permissions;

            // Get audit logs
            var auditLogs = await _context.AuditLogs
                .Where(al => al.ChangedBy == userIdInt.ToString() || al.EntityId == userIdInt)
                .OrderBy(al => al.ChangeDate)
                .Select(al => new AuditLogData
                {
                    Action = al.ChangeType,
                    Resource = $"{al.EntityType}:{al.EntityId}",
                    Timestamp = al.ChangeDate,
                    IpAddress = "Anonymized", // Not stored in current AuditLog model
                    UserAgent = al.ChangeDetails.Length > 50 ? al.ChangeDetails.Substring(0, 50) : al.ChangeDetails
                })
                .ToListAsync(cancellationToken);

            userData.AuditLogs = auditLogs;

            // Get consent records
            var consents = await _context.ConsentRecords
                .Where(cr => cr.UserId == int.Parse(userId))
                .Select(cr => new ConsentData
                {
                    PurposeId = cr.Id.ToString(), // Use Id as PurposeId
                    Purpose = cr.Purpose ?? "Unknown",
                    ConsentedAt = cr.ConsentDate,
                    ExpiresAt = null, // Not available in current model
                    Withdrawn = cr.WithdrawnDate.HasValue
                })
                .ToListAsync(cancellationToken);

            userData.Consents = consents;

            // Export in requested format
            byte[] exportData;
            string fileName;

            switch (format)
            {
                case ExportFormat.JSON:
                    exportData = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(userData, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
                    fileName = $"user_data_{userId}_{DateTime.UtcNow:yyyyMMdd}.json";
                    break;

                case ExportFormat.CSV:
                    exportData = ExportToCsv(userData);
                    fileName = $"user_data_{userId}_{DateTime.UtcNow:yyyyMMdd}.csv";
                    break;

                case ExportFormat.XML:
                    exportData = ExportToXml(userData);
                    fileName = $"user_data_{userId}_{DateTime.UtcNow:yyyyMMdd}.xml";
                    break;

                default:
                    throw new NotSupportedException($"Export format {format} not supported");
            }

            // Audit the export
            await _auditService.LogGdprEventAsync(new GdprAuditEvent
            {
                GdprEventType = GdprEventType.DataExport,
                DataSubjectId = userId,
                Description = $"User data exported in {format} format",
                AdditionalData = new Dictionary<string, object>
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
            // Note: LegalHolds table not implemented yet - skip this check for now

            if (hardDelete)
            {
                // Hard delete - permanently remove all data
                
                // Delete audit logs
                var auditLogs = await _context.AuditLogs
                    .Where(al => al.ChangedBy == userId)
                    .ExecuteDeleteAsync(cancellationToken);
                result.RecordsDeleted["AuditLogs"] = auditLogs;

                // Delete user roles 
                var userRoles = await _context.UserRoles
                    .Where(ur => ur.UserId == int.Parse(userId))
                    .ExecuteDeleteAsync(cancellationToken);
                result.RecordsDeleted["UserRoles"] = userRoles;

                // Delete consent records
                var consents = await _context.ConsentRecords
                    .Where(cr => cr.UserId == int.Parse(userId))
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
                user.Name = "Deleted User";
                // Note: Email, IsDeleted, DeletedAt, and Metadata properties don't exist in User model

                // Anonymize audit logs
                var auditLogs = await _context.AuditLogs
                    .Where(al => al.ChangedBy == userId)
                    .ToListAsync(cancellationToken);

                foreach (var log in auditLogs)
                {
                    log.ChangedBy = $"anonymous_{GenerateHash(userId)}";
                    log.ChangeDetails = "Anonymized for GDPR compliance";
                }

                result.RecordsAnonymized["AuditLogs"] = auditLogs.Count;

                await _context.SaveChangesAsync(cancellationToken);
                result.RecordsAnonymized["User"] = 1;
            }

            await transaction.CommitAsync(cancellationToken);

            // Audit the erasure
            await _auditService.LogGdprEventAsync(new GdprAuditEvent
            {
                GdprEventType = GdprEventType.RightToErasure,
                DataSubjectId = hardDelete ? "ERASED" : userId,
                Description = $"User data {(hardDelete ? "permanently deleted" : "anonymized")}",
                AdditionalData = new Dictionary<string, object>
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
                    originalValues[correction.Key] = property.GetValue(user) ?? string.Empty;
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
                await _auditService.LogGdprEventAsync(new GdprAuditEvent
                {
                    GdprEventType = GdprEventType.RightToRectification,
                    DataSubjectId = userId,
                    Description = "User data rectified",
                    AdditionalData = new Dictionary<string, object>
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

    public Task<DataProcessingInfo> GetDataProcessingInfoAsync(string userId, CancellationToken cancellationToken = default)
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

        return Task.FromResult(info);
    }

    #endregion

    #region Consent Management

    public async Task<ConsentRecord> RecordConsentAsync(ConsentRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Recording consent for {UserId} for purpose {PurposeId}", request.UserId, request.PurposeId);

        var consent = new ConsentRecord
        {
            UserId = int.Parse(request.UserId),
            ConsentType = "DataProcessing",
            Purpose = request.Purpose,
            IsConsented = true,
            ConsentDate = DateTime.UtcNow,
            ConsentVersion = "1.0",
            LegalBasis = "Consent"
        };

        _context.ConsentRecords.Add(consent);
        await _context.SaveChangesAsync(cancellationToken);

        // Audit consent
        await _auditService.LogGdprEventAsync(new GdprAuditEvent
        {
            GdprEventType = GdprEventType.ConsentGiven,
            DataSubjectId = request.UserId,
            Description = $"Consent granted for {request.Purpose}",
            AdditionalData = new Dictionary<string, object>
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
            .FirstOrDefaultAsync(cr => cr.UserId == int.Parse(userId) && cr.Purpose == purposeId && cr.IsConsented && !cr.WithdrawnDate.HasValue, cancellationToken);

        if (consent == null)
        {
            return false;
        }

        consent.IsConsented = false;
        consent.WithdrawnDate = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        // Audit withdrawal
        await _auditService.LogGdprEventAsync(new GdprAuditEvent
        {
            GdprEventType = GdprEventType.ConsentWithdrawn,
            DataSubjectId = userId,
            Description = $"Consent withdrawn for {consent.Purpose}",
            AdditionalData = new Dictionary<string, object>
            {
                ["PurposeId"] = purposeId,
                ["ConsentedAt"] = consent.ConsentDate.ToString("O"),
                ["WithdrawnAt"] = consent.WithdrawnDate.Value.ToString("O")
            }
        });

        return true;
    }

    public async Task<List<ConsentRecord>> GetUserConsentsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.ConsentRecords
            .Where(cr => cr.UserId == int.Parse(userId))
            .OrderByDescending(cr => cr.ConsentDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasValidConsentAsync(string userId, string purposeId, CancellationToken cancellationToken = default)
    {
        return await _context.ConsentRecords
            .AnyAsync(cr => 
                cr.UserId == int.Parse(userId) && 
                cr.Purpose == purposeId && 
                cr.IsConsented && !cr.WithdrawnDate.HasValue &&
                true, // ExpiresAt not available in ConsentRecord 
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
                .Where(al => al.ChangedBy == userId && al.ChangeDate < cutoffDate)
                .ExecuteDeleteAsync(cancellationToken);
            
            result.RecordsRemoved["AuditLogs"] = oldAuditLogs;

            // Remove expired consents
            var expiredConsents = await _context.ConsentRecords
                .Where(cr => cr.UserId == int.Parse(userId) && cr.WithdrawnDate.HasValue)
                .ExecuteDeleteAsync(cancellationToken);
            
            result.RecordsRemoved["ExpiredConsents"] = expiredConsents;

            // Clear unnecessary metadata
            var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
            if (user != null && user.Metadata != null && user.Metadata.Any())
            {
                var minimizedMetadata = new Dictionary<string, object>();
                
                // Keep only essential metadata
                var essentialKeys = new[] { "locale", "timezone", "preferences" };
                foreach (var key in essentialKeys)
                {
                    if (user.Metadata.ContainsKey(key))
                    {
                        minimizedMetadata[key] = user.Metadata[key];
                    }
                }

                user.Metadata = minimizedMetadata;
                await _context.SaveChangesAsync(cancellationToken);
                
                result.FieldsMinimized.Add("Metadata");
            }

            result.Success = true;
            result.Message = "Data minimization completed";

            // Audit minimization
            await _auditService.LogGdprEventAsync(new GdprAuditEvent
            {
                GdprEventType = GdprEventType.DataModification,
                DataSubjectId = userId,
                Description = "User data minimized",
                AdditionalData = new Dictionary<string, object>
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
            .CountAsync(al => al.EntityId == int.Parse(userId) && al.ChangeDate < DateTime.UtcNow.AddDays(-90), cancellationToken);
        
        if (oldAuditLogsCount > 0)
        {
            excessiveData.Add($"{oldAuditLogsCount} audit logs older than 90 days");
        }

        // Check for expired consents
        var expiredConsentsCount = await _context.ConsentRecords
            .CountAsync(cr => cr.UserId == int.Parse(userId) && cr.WithdrawnDate.HasValue, cancellationToken);
        
        if (expiredConsentsCount > 0)
        {
            excessiveData.Add($"{expiredConsentsCount} expired consent records");
        }

        // Check for unnecessary metadata
        var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user != null && user.Metadata != null && user.Metadata.Any())
        {
            var unnecessaryKeys = user.Metadata.Keys
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
            var numericPseudonym = GenerateNumericPseudonym(userId);
            
            // Update user record
            var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
            if (user == null) return false;

            user.PseudonymId = pseudonym;
            user.IsPseudonymized = true;

            // Pseudonymize audit logs
            var auditLogs = await _context.AuditLogs
                .Where(al => al.EntityId == int.Parse(userId))
                .ToListAsync(cancellationToken);

            foreach (var log in auditLogs)
            {
                log.EntityId = numericPseudonym;
            }

            await _context.SaveChangesAsync(cancellationToken);

            // Audit pseudonymization
            await _auditService.LogGdprEventAsync(new GdprAuditEvent
            {
                GdprEventType = GdprEventType.DataModification,
                DataSubjectId = pseudonym,
                Description = "User data pseudonymized",
                AdditionalData = new Dictionary<string, object>
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
            var numericAnonymousId = GenerateNumericPseudonym($"anon_{userId}");

            // Anonymize user data
            user.Id = numericAnonymousId;
            user.Email = $"{anonymousId}@anonymous.local";
            user.Name = "Anonymous User";
            user.Metadata = new Dictionary<string, object>();
            user.IsAnonymized = true;
            user.AnonymizedAt = DateTime.UtcNow;

            // Anonymize related records
            var auditLogs = await _context.AuditLogs
                .Where(al => al.EntityId == int.Parse(userId))
                .ToListAsync(cancellationToken);

            foreach (var log in auditLogs)
            {
                log.EntityId = numericAnonymousId;
                log.IpAddress = "0.0.0.0";
                log.UserAgent = "Anonymized";
            }

            await _context.SaveChangesAsync(cancellationToken);

            // Audit anonymization
            await _auditService.LogGdprEventAsync(new GdprAuditEvent
            {
                GdprEventType = GdprEventType.DataModification,
                DataSubjectId = anonymousId,
                Description = "User data anonymized",
                AdditionalData = new Dictionary<string, object>
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
            Id = 0, // Let EF generate the ID
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

        _context.DataBreachRecords.Add(record);
        await _context.SaveChangesAsync(cancellationToken);

        // Check if notification is required (within 72 hours for GDPR)
        var hoursSinceDiscovery = (DateTime.UtcNow - breach.DiscoveryDate).TotalHours;
        if (hoursSinceDiscovery < 72)
        {
            record.NotificationDeadline = breach.DiscoveryDate.AddHours(72);
        }

        // Audit breach report
        await _auditService.LogGdprEventAsync(new GdprAuditEvent
        {
            GdprEventType = GdprEventType.DataBreach,
            DataSubjectId = "SYSTEM",
            Description = "Data breach reported",
            AdditionalData = new Dictionary<string, object>
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
        var query = _context.DataBreachRecords.AsQueryable();

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
    
    private int GenerateNumericPseudonym(string userId)
    {
        var salt = "ACS_GDPR_SALT"; // In production, use a secure random salt
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(userId + salt));
        // Convert first 4 bytes to int (ensuring positive)
        return Math.Abs(BitConverter.ToInt32(bytes, 0));
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
    public string Message { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string ExportId { get; set; } = string.Empty;
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public ExportFormat Format { get; set; }
    public DateTime ExportDate { get; set; }
}

public class DataErasureResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime ErasureDate { get; set; }
    public bool HardDelete { get; set; }
    public Dictionary<string, int> RecordsDeleted { get; set; } = new();
    public Dictionary<string, int> RecordsAnonymized { get; set; } = new();
}

public class DataRectificationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime RectificationDate { get; set; }
    public List<string> FieldsUpdated { get; set; } = new();
    public List<string> FieldsSkipped { get; set; } = new();
}

public class DataMinimizationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime MinimizationDate { get; set; }
    public Dictionary<string, int> RecordsRemoved { get; set; } = new();
    public List<string> FieldsMinimized { get; set; } = new();
}

public class UserDataExport
{
    public string ExportId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime ExportDate { get; set; }
    public ExportFormat Format { get; set; }
    public UserProfile Profile { get; set; } = new();
    public List<PermissionData> Permissions { get; set; } = new();
    public List<AuditLogData> AuditLogs { get; set; } = new();
    public List<ConsentData> Consents { get; set; } = new();
}

public class UserProfile
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<string> Roles { get; set; } = new();
    public List<string> Groups { get; set; } = new();
}

public class PermissionData
{
    public string ResourceId { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
    public DateTime GrantedAt { get; set; }
}

public class AuditLogData
{
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
}

public class ConsentData
{
    public string PurposeId { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public DateTime ConsentedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool Withdrawn { get; set; }
}

public class ConsentRequest
{
    public string UserId { get; set; } = string.Empty;
    public string PurposeId { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string ConsentMethod { get; set; } = string.Empty;
}

public class DataProcessingInfo
{
    public string UserId { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public List<DataLocation> DataLocations { get; set; } = new();
    public List<ProcessingPurpose> ProcessingPurposes { get; set; } = new();
    public List<DataSharingInfo> DataSharing { get; set; } = new();
    public List<string> UserRights { get; set; } = new();
}

public class DataLocation
{
    public string Table { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string RetentionPeriod { get; set; } = string.Empty;
}

public class ProcessingPurpose
{
    public string Purpose { get; set; } = string.Empty;
    public string LegalBasis { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class DataSharingInfo
{
    public string Recipient { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string DataShared { get; set; } = string.Empty;
}

public class DataBreachInfo
{
    public DateTime IncidentDate { get; set; }
    public DateTime DiscoveryDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string> DataTypes { get; set; } = new();
    public List<string> AffectedUsers { get; set; } = new();
    public string Severity { get; set; } = string.Empty;
    public List<string> MitigationSteps { get; set; } = new();
}

#endregion