using System.ComponentModel.DataAnnotations;

namespace ACS.WebApi.Models.Requests;

#region System Configuration

/// <summary>
/// Request model for updating system configuration
/// </summary>
public class UpdateSystemConfigurationRequest
{
    /// <summary>
    /// Configuration section to update
    /// </summary>
    [Required(ErrorMessage = "Section is required")]
    [StringLength(100, ErrorMessage = "Section name cannot exceed 100 characters")]
    public string Section { get; set; } = string.Empty;

    /// <summary>
    /// Configuration settings as key-value pairs
    /// </summary>
    [Required(ErrorMessage = "Configuration is required")]
    public Dictionary<string, object> Configuration { get; set; } = new();

    /// <summary>
    /// Reason for the configuration change
    /// </summary>
    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
    public string? Reason { get; set; }

    /// <summary>
    /// Whether to validate configuration before applying
    /// </summary>
    public bool ValidateBeforeApply { get; set; } = true;

    /// <summary>
    /// Whether to backup current configuration before changes
    /// </summary>
    public bool BackupCurrent { get; set; } = true;

    /// <summary>
    /// Effective date for configuration changes
    /// </summary>
    public DateTime? EffectiveDate { get; set; }

    /// <summary>
    /// Whether to restart affected services after configuration change
    /// </summary>
    public bool RestartServices { get; set; } = false;

    /// <summary>
    /// Notification recipients for configuration changes
    /// </summary>
    public List<string> NotificationRecipients { get; set; } = new();
}

#endregion

#region Tenant Management

/// <summary>
/// Request model for creating a new tenant
/// </summary>
public class CreateTenantRequest
{
    /// <summary>
    /// Tenant name
    /// </summary>
    [Required(ErrorMessage = "Tenant name is required")]
    [StringLength(255, MinimumLength = 2, ErrorMessage = "Tenant name must be between 2 and 255 characters")]
    [RegularExpression(@"^[a-zA-Z0-9\s\-_.]+$", ErrorMessage = "Tenant name contains invalid characters")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Database connection string for the tenant
    /// </summary>
    [Required(ErrorMessage = "Database connection string is required")]
    [StringLength(2000, ErrorMessage = "Connection string cannot exceed 2000 characters")]
    public string DatabaseConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Tenant-specific configuration settings
    /// </summary>
    public Dictionary<string, object> Configuration { get; set; } = new();

    /// <summary>
    /// Whether the tenant is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Initial administrator email for the tenant
    /// </summary>
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
    public string? AdminEmail { get; set; }

    /// <summary>
    /// Tenant domain or subdomain
    /// </summary>
    [StringLength(255, ErrorMessage = "Domain cannot exceed 255 characters")]
    [RegularExpression(@"^[a-zA-Z0-9\-_.]+$", ErrorMessage = "Domain contains invalid characters")]
    public string? Domain { get; set; }

    /// <summary>
    /// Maximum number of users allowed for this tenant
    /// </summary>
    [Range(1, 100000, ErrorMessage = "User limit must be between 1 and 100,000")]
    public int? UserLimit { get; set; }

    /// <summary>
    /// Tenant tier or subscription level
    /// </summary>
    [StringLength(50, ErrorMessage = "Tier cannot exceed 50 characters")]
    public string? Tier { get; set; }

    /// <summary>
    /// Additional metadata for the tenant
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Request model for updating tenant configuration
/// </summary>
public class UpdateTenantRequest
{
    /// <summary>
    /// Updated tenant configuration
    /// </summary>
    public Dictionary<string, object> Configuration { get; set; } = new();

    /// <summary>
    /// Whether the tenant is active
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>
    /// Updated user limit for the tenant
    /// </summary>
    [Range(1, 100000, ErrorMessage = "User limit must be between 1 and 100,000")]
    public int? UserLimit { get; set; }

    /// <summary>
    /// Updated tenant tier
    /// </summary>
    [StringLength(50, ErrorMessage = "Tier cannot exceed 50 characters")]
    public string? Tier { get; set; }

    /// <summary>
    /// Updated domain for the tenant
    /// </summary>
    [StringLength(255, ErrorMessage = "Domain cannot exceed 255 characters")]
    [RegularExpression(@"^[a-zA-Z0-9\-_.]+$", ErrorMessage = "Domain contains invalid characters")]
    public string? Domain { get; set; }

    /// <summary>
    /// Reason for the update
    /// </summary>
    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
    public string? Reason { get; set; }

    /// <summary>
    /// Whether to notify tenant administrators of changes
    /// </summary>
    public bool NotifyAdmins { get; set; } = true;

    /// <summary>
    /// Metadata updates
    /// </summary>
    public Dictionary<string, object> MetadataUpdates { get; set; } = new();
}

#endregion

#region Cache Management

/// <summary>
/// Request model for clearing cache
/// </summary>
public class ClearCacheRequest
{
    /// <summary>
    /// Cache key pattern to clear (supports wildcards)
    /// </summary>
    [StringLength(500, ErrorMessage = "Pattern cannot exceed 500 characters")]
    public string? Pattern { get; set; }

    /// <summary>
    /// Cache region to clear
    /// </summary>
    [StringLength(100, ErrorMessage = "Region cannot exceed 100 characters")]
    public string? Region { get; set; }

    /// <summary>
    /// Whether to force clear even if cache is currently being accessed
    /// </summary>
    public bool Force { get; set; } = false;

    /// <summary>
    /// Reason for clearing cache
    /// </summary>
    [Required(ErrorMessage = "Reason is required")]
    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Whether to warm up cache after clearing
    /// </summary>
    public bool WarmUpAfterClear { get; set; } = false;

    /// <summary>
    /// Cache types to clear (Memory, Distributed, All)
    /// </summary>
    public List<string> CacheTypes { get; set; } = new() { "All" };
}

/// <summary>
/// Request model for preloading cache
/// </summary>
public class PreloadCacheRequest
{
    /// <summary>
    /// Cache regions to preload
    /// </summary>
    [Required(ErrorMessage = "Regions are required")]
    [MinLength(1, ErrorMessage = "At least one region is required")]
    public List<string> Regions { get; set; } = new();

    /// <summary>
    /// Preload priority (Low, Normal, High)
    /// </summary>
    [RegularExpression("^(Low|Normal|High)$", ErrorMessage = "Priority must be Low, Normal, or High")]
    public string Priority { get; set; } = "Normal";

    /// <summary>
    /// Maximum time to spend on preloading
    /// </summary>
    public TimeSpan? MaxDuration { get; set; }

    /// <summary>
    /// Whether to preload in background
    /// </summary>
    public bool Background { get; set; } = true;

    /// <summary>
    /// Data sources to preload from
    /// </summary>
    public List<string> DataSources { get; set; } = new();

    /// <summary>
    /// Preload strategy (Eager, Lazy, Smart)
    /// </summary>
    [RegularExpression("^(Eager|Lazy|Smart)$", ErrorMessage = "Strategy must be Eager, Lazy, or Smart")]
    public string Strategy { get; set; } = "Smart";
}

#endregion

#region Maintenance Operations

/// <summary>
/// Request model for maintenance operations
/// </summary>
public class MaintenanceRequest
{
    /// <summary>
    /// Maintenance tasks to perform
    /// </summary>
    [Required(ErrorMessage = "Tasks are required")]
    [MinLength(1, ErrorMessage = "At least one task is required")]
    public List<string> Tasks { get; set; } = new();

    /// <summary>
    /// When to perform the maintenance (null = immediately)
    /// </summary>
    public DateTime? ScheduledFor { get; set; }

    /// <summary>
    /// Maximum duration allowed for maintenance
    /// </summary>
    public TimeSpan? MaxDuration { get; set; }

    /// <summary>
    /// Whether to put system in maintenance mode
    /// </summary>
    public bool MaintenanceMode { get; set; } = false;

    /// <summary>
    /// Notification settings for maintenance
    /// </summary>
    public MaintenanceNotificationSettings NotificationSettings { get; set; } = new();

    /// <summary>
    /// Reason for the maintenance
    /// </summary>
    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
    public string? Reason { get; set; }

    /// <summary>
    /// Whether to continue with remaining tasks if one fails
    /// </summary>
    public bool ContinueOnError { get; set; } = false;

    /// <summary>
    /// Backup system before maintenance
    /// </summary>
    public bool BackupBeforeMaintenance { get; set; } = true;
}

/// <summary>
/// Notification settings for maintenance operations
/// </summary>
public class MaintenanceNotificationSettings
{
    /// <summary>
    /// Whether to notify users of maintenance
    /// </summary>
    public bool NotifyUsers { get; set; } = true;

    /// <summary>
    /// Whether to notify administrators
    /// </summary>
    public bool NotifyAdmins { get; set; } = true;

    /// <summary>
    /// Advance notice period for notifications
    /// </summary>
    public TimeSpan AdvanceNotice { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Custom notification message
    /// </summary>
    [StringLength(1000, ErrorMessage = "Message cannot exceed 1000 characters")]
    public string? CustomMessage { get; set; }

    /// <summary>
    /// Notification channels (Email, SMS, Push, InApp)
    /// </summary>
    public List<string> Channels { get; set; } = new() { "Email", "InApp" };
}

#endregion

#region Backup and Recovery

/// <summary>
/// Request model for creating backups
/// </summary>
public class CreateBackupRequest
{
    /// <summary>
    /// Type of backup (Full, Incremental, Differential, ConfigOnly)
    /// </summary>
    [Required(ErrorMessage = "Backup type is required")]
    [RegularExpression("^(Full|Incremental|Differential|ConfigOnly)$", 
        ErrorMessage = "Backup type must be Full, Incremental, Differential, or ConfigOnly")]
    public string BackupType { get; set; } = "Full";

    /// <summary>
    /// Whether to include audit logs in the backup
    /// </summary>
    public bool IncludeAuditLogs { get; set; } = true;

    /// <summary>
    /// Whether to compress the backup
    /// </summary>
    public bool Compress { get; set; } = true;

    /// <summary>
    /// Whether to encrypt the backup
    /// </summary>
    public bool Encrypt { get; set; } = true;

    /// <summary>
    /// Custom backup location (optional)
    /// </summary>
    [StringLength(500, ErrorMessage = "Location cannot exceed 500 characters")]
    public string? CustomLocation { get; set; }

    /// <summary>
    /// Retention period for the backup
    /// </summary>
    public TimeSpan? RetentionPeriod { get; set; }

    /// <summary>
    /// Description for the backup
    /// </summary>
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }

    /// <summary>
    /// Tags for categorizing the backup
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Whether to verify backup integrity after creation
    /// </summary>
    public bool VerifyIntegrity { get; set; } = true;

    /// <summary>
    /// Priority of the backup operation (Low, Normal, High)
    /// </summary>
    [RegularExpression("^(Low|Normal|High)$", ErrorMessage = "Priority must be Low, Normal, or High")]
    public string Priority { get; set; } = "Normal";
}

/// <summary>
/// Request model for restoring from backup
/// </summary>
public class RestoreBackupRequest
{
    /// <summary>
    /// Backup ID to restore from
    /// </summary>
    [Required(ErrorMessage = "Backup ID is required")]
    public string BackupId { get; set; } = string.Empty;

    /// <summary>
    /// Restore point in time (for incremental backups)
    /// </summary>
    public DateTime? RestorePointInTime { get; set; }

    /// <summary>
    /// Whether to restore configuration settings
    /// </summary>
    public bool RestoreConfiguration { get; set; } = true;

    /// <summary>
    /// Whether to restore user data
    /// </summary>
    public bool RestoreUserData { get; set; } = true;

    /// <summary>
    /// Whether to restore audit logs
    /// </summary>
    public bool RestoreAuditLogs { get; set; } = true;

    /// <summary>
    /// Confirmation token (required for production restores)
    /// </summary>
    [StringLength(100, ErrorMessage = "Confirmation token cannot exceed 100 characters")]
    public string? ConfirmationToken { get; set; }

    /// <summary>
    /// Reason for the restore operation
    /// </summary>
    [Required(ErrorMessage = "Reason is required")]
    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Whether to create a backup before restore
    /// </summary>
    public bool BackupBeforeRestore { get; set; } = true;

    /// <summary>
    /// Notification recipients for restore operation
    /// </summary>
    public List<string> NotificationRecipients { get; set; } = new();
}

#endregion

#region System Operations

/// <summary>
/// Request model for system restart
/// </summary>
public class SystemRestartRequest
{
    /// <summary>
    /// Reason for restart
    /// </summary>
    [Required(ErrorMessage = "Reason is required")]
    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Delay before restart
    /// </summary>
    public TimeSpan? DelayBeforeRestart { get; set; }

    /// <summary>
    /// Whether to gracefully shut down services
    /// </summary>
    public bool GracefulShutdown { get; set; } = true;

    /// <summary>
    /// Maximum time to wait for graceful shutdown
    /// </summary>
    public TimeSpan GracefulShutdownTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to notify users of restart
    /// </summary>
    public bool NotifyUsers { get; set; } = true;

    /// <summary>
    /// Custom notification message
    /// </summary>
    [StringLength(500, ErrorMessage = "Message cannot exceed 500 characters")]
    public string? NotificationMessage { get; set; }

    /// <summary>
    /// Services to restart (empty = all services)
    /// </summary>
    public List<string> ServicesToRestart { get; set; } = new();

    /// <summary>
    /// Force restart even if system is busy
    /// </summary>
    public bool ForceRestart { get; set; } = false;
}

/// <summary>
/// Request model for enabling/disabling maintenance mode
/// </summary>
public class MaintenanceModeRequest
{
    /// <summary>
    /// Whether to enable maintenance mode
    /// </summary>
    [Required(ErrorMessage = "Enable flag is required")]
    public bool Enable { get; set; }

    /// <summary>
    /// Reason for maintenance mode change
    /// </summary>
    [Required(ErrorMessage = "Reason is required")]
    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Custom maintenance message to display
    /// </summary>
    [StringLength(1000, ErrorMessage = "Message cannot exceed 1000 characters")]
    public string? MaintenanceMessage { get; set; }

    /// <summary>
    /// Estimated duration of maintenance
    /// </summary>
    public TimeSpan? EstimatedDuration { get; set; }

    /// <summary>
    /// Whether to allow administrator access during maintenance
    /// </summary>
    public bool AllowAdminAccess { get; set; } = true;

    /// <summary>
    /// IP addresses allowed during maintenance mode
    /// </summary>
    public List<string> AllowedIpAddresses { get; set; } = new();

    /// <summary>
    /// Whether to schedule automatic disable of maintenance mode
    /// </summary>
    public DateTime? AutoDisableAt { get; set; }

    /// <summary>
    /// Notification recipients for maintenance mode changes
    /// </summary>
    public List<string> NotificationRecipients { get; set; } = new();
}

/// <summary>
/// Request model for system diagnostics
/// </summary>
public class SystemDiagnosticsRequest
{
    /// <summary>
    /// Diagnostic tests to run
    /// </summary>
    [Required(ErrorMessage = "Tests are required")]
    [MinLength(1, ErrorMessage = "At least one test is required")]
    public List<string> Tests { get; set; } = new();

    /// <summary>
    /// Include detailed output in diagnostics
    /// </summary>
    public bool IncludeDetailedOutput { get; set; } = true;

    /// <summary>
    /// Include performance metrics
    /// </summary>
    public bool IncludePerformanceMetrics { get; set; } = true;

    /// <summary>
    /// Include security checks
    /// </summary>
    public bool IncludeSecurityChecks { get; set; } = false;

    /// <summary>
    /// Maximum time to spend on diagnostics
    /// </summary>
    public TimeSpan? MaxDuration { get; set; }

    /// <summary>
    /// Export format for diagnostics report (JSON, XML, HTML)
    /// </summary>
    [RegularExpression("^(JSON|XML|HTML)$", ErrorMessage = "Export format must be JSON, XML, or HTML")]
    public string ExportFormat { get; set; } = "JSON";

    /// <summary>
    /// Whether to include system logs in report
    /// </summary>
    public bool IncludeSystemLogs { get; set; } = false;

    /// <summary>
    /// Whether to run diagnostics in background
    /// </summary>
    public bool RunInBackground { get; set; } = false;
}

#endregion

#region Feature Management

/// <summary>
/// Request model for toggling feature flags
/// </summary>
public class ToggleFeatureRequest
{
    /// <summary>
    /// Feature flag name
    /// </summary>
    [Required(ErrorMessage = "Feature name is required")]
    [StringLength(100, ErrorMessage = "Feature name cannot exceed 100 characters")]
    public string FeatureName { get; set; } = string.Empty;

    /// <summary>
    /// Whether to enable the feature
    /// </summary>
    [Required(ErrorMessage = "Enabled flag is required")]
    public bool Enabled { get; set; }

    /// <summary>
    /// Reason for toggling the feature
    /// </summary>
    [Required(ErrorMessage = "Reason is required")]
    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Effective date for the feature toggle
    /// </summary>
    public DateTime? EffectiveDate { get; set; }

    /// <summary>
    /// Expiration date for the feature toggle
    /// </summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Percentage rollout for gradual feature deployment
    /// </summary>
    [Range(0, 100, ErrorMessage = "Rollout percentage must be between 0 and 100")]
    public int? RolloutPercentage { get; set; }

    /// <summary>
    /// Target user groups for the feature
    /// </summary>
    public List<string> TargetGroups { get; set; } = new();

    /// <summary>
    /// Whether to notify users of feature changes
    /// </summary>
    public bool NotifyUsers { get; set; } = false;
}

#endregion