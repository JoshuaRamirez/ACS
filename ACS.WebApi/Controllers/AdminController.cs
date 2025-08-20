using ACS.Service.Domain;
using ACS.Service.Domain.Events;
using ACS.Service.Domain.Specifications;
using ACS.Service.Services;
using ACS.WebApi.Models;
using ACS.WebApi.Models.Requests;
using ACS.WebApi.Models.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Reflection;
using System.Text.Json;

namespace ACS.WebApi.Controllers;

/// <summary>
/// Controller for administrative operations including system configuration, maintenance, and tenant management
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "Administrator,SystemAdmin")]
[Produces("application/json")]
public class AdminController : ControllerBase
{
    private readonly ISystemConfigurationService _systemConfigService;
    private readonly ITenantService _tenantService;
    private readonly IMaintenanceService _maintenanceService;
    private readonly IHealthCheckService _healthCheckService;
    private readonly ICacheService _cacheService;
    private readonly IAuditService _auditService;
    private readonly IBackupService _backupService;
    private readonly IMonitoringService _monitoringService;
    private readonly IDomainEventPublisher _eventPublisher;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        ISystemConfigurationService systemConfigService,
        ITenantService tenantService,
        IMaintenanceService maintenanceService,
        IHealthCheckService healthCheckService,
        ICacheService cacheService,
        IAuditService auditService,
        IBackupService backupService,
        IMonitoringService monitoringService,
        IDomainEventPublisher eventPublisher,
        ILogger<AdminController> logger)
    {
        _systemConfigService = systemConfigService;
        _tenantService = tenantService;
        _maintenanceService = maintenanceService;
        _healthCheckService = healthCheckService;
        _cacheService = cacheService;
        _auditService = auditService;
        _backupService = backupService;
        _monitoringService = monitoringService;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    #region System Configuration

    /// <summary>
    /// Gets system configuration settings
    /// </summary>
    /// <param name="section">Configuration section to retrieve (optional)</param>
    /// <returns>System configuration</returns>
    [HttpGet("configuration")]
    [ProducesResponseType(typeof(SystemConfigurationResponse), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<SystemConfigurationResponse>> GetSystemConfigurationAsync(
        [FromQuery] string? section = null)
    {
        try
        {
            _logger.LogInformation("Retrieving system configuration for section: {Section}", section ?? "all");

            var config = await _systemConfigService.GetConfigurationAsync(section);
            
            var response = new SystemConfigurationResponse
            {
                Section = section ?? "all",
                Configuration = config,
                LastUpdated = await _systemConfigService.GetLastUpdateTimeAsync(section),
                Version = await _systemConfigService.GetConfigurationVersionAsync(),
                IsReadOnly = await _systemConfigService.IsReadOnlyModeAsync()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system configuration for section: {Section}", section);
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while retrieving system configuration");
        }
    }

    /// <summary>
    /// Updates system configuration settings
    /// </summary>
    /// <param name="request">Configuration update request</param>
    /// <returns>Updated configuration</returns>
    [HttpPut("configuration")]
    [ProducesResponseType(typeof(SystemConfigurationResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<SystemConfigurationResponse>> UpdateSystemConfigurationAsync(
        [FromBody] UpdateSystemConfigurationRequest request)
    {
        try
        {
            _logger.LogInformation("Updating system configuration for section: {Section}", request.Section);

            // Validate configuration changes
            var validationResult = await _systemConfigService.ValidateConfigurationAsync(request.Configuration);
            if (!validationResult.IsValid)
            {
                return BadRequest(CreateValidationProblemDetails(validationResult));
            }

            var updatedConfig = await _systemConfigService.UpdateConfigurationAsync(
                request.Section,
                request.Configuration,
                request.Reason);

            // Publish configuration change event
            await _eventPublisher.PublishAsync(new SystemConfigurationChangedEvent(
                request.Section,
                request.Configuration,
                request.Reason ?? "Configuration updated via admin API"));

            var response = new SystemConfigurationResponse
            {
                Section = request.Section,
                Configuration = updatedConfig,
                LastUpdated = DateTime.UtcNow,
                Version = await _systemConfigService.GetConfigurationVersionAsync(),
                IsReadOnly = await _systemConfigService.IsReadOnlyModeAsync()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating system configuration for section: {Section}", request.Section);
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while updating system configuration");
        }
    }

    /// <summary>
    /// Resets configuration section to default values
    /// </summary>
    /// <param name="section">Configuration section to reset</param>
    /// <param name="reason">Reason for reset</param>
    /// <returns>Reset configuration</returns>
    [HttpPost("configuration/{section}/reset")]
    [ProducesResponseType(typeof(SystemConfigurationResponse), (int)HttpStatusCode.OK)]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<SystemConfigurationResponse>> ResetConfigurationSectionAsync(
        string section,
        [FromBody] string reason)
    {
        try
        {
            _logger.LogInformation("Resetting configuration section: {Section}", section);

            var resetConfig = await _systemConfigService.ResetConfigurationSectionAsync(section, reason);

            await _eventPublisher.PublishAsync(new SystemConfigurationResetEvent(section, reason));

            var response = new SystemConfigurationResponse
            {
                Section = section,
                Configuration = resetConfig,
                LastUpdated = DateTime.UtcNow,
                Version = await _systemConfigService.GetConfigurationVersionAsync(),
                IsReadOnly = await _systemConfigService.IsReadOnlyModeAsync()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting configuration section: {Section}", section);
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while resetting configuration");
        }
    }

    #endregion

    #region Tenant Management

    /// <summary>
    /// Gets all tenants with their configuration
    /// </summary>
    /// <param name="includeInactive">Whether to include inactive tenants</param>
    /// <returns>List of tenants</returns>
    [HttpGet("tenants")]
    [ProducesResponseType(typeof(IEnumerable<TenantResponse>), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<IEnumerable<TenantResponse>>> GetTenantsAsync(
        [FromQuery] bool includeInactive = false)
    {
        try
        {
            _logger.LogInformation("Retrieving tenants, includeInactive: {IncludeInactive}", includeInactive);

            var tenants = await _tenantService.GetAllTenantsAsync(includeInactive);
            var response = tenants.Select(MapToTenantResponse).ToList();

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tenants");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while retrieving tenants");
        }
    }

    /// <summary>
    /// Creates a new tenant
    /// </summary>
    /// <param name="request">Tenant creation request</param>
    /// <returns>Created tenant</returns>
    [HttpPost("tenants")]
    [ProducesResponseType(typeof(TenantResponse), (int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<TenantResponse>> CreateTenantAsync([FromBody] CreateTenantRequest request)
    {
        try
        {
            _logger.LogInformation("Creating new tenant: {TenantName}", request.Name);

            var tenant = await _tenantService.CreateTenantAsync(
                request.Name,
                request.DatabaseConnectionString,
                request.Configuration,
                request.IsActive);

            await _eventPublisher.PublishAsync(new TenantCreatedEvent(tenant, "Tenant created via admin API"));

            var response = MapToTenantResponse(tenant);
            return CreatedAtAction(nameof(GetTenantAsync), new { id = tenant.Id }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tenant: {TenantName}", request.Name);
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while creating the tenant");
        }
    }

    /// <summary>
    /// Gets a specific tenant by ID
    /// </summary>
    /// <param name="id">Tenant ID</param>
    /// <returns>Tenant details</returns>
    [HttpGet("tenants/{id}")]
    [ProducesResponseType(typeof(TenantResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<TenantResponse>> GetTenantAsync(int id)
    {
        try
        {
            var tenant = await _tenantService.GetTenantByIdAsync(id);
            if (tenant == null)
            {
                return NotFound($"Tenant with ID {id} not found");
            }

            var response = MapToTenantResponse(tenant);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tenant {TenantId}", id);
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while retrieving the tenant");
        }
    }

    /// <summary>
    /// Updates tenant configuration
    /// </summary>
    /// <param name="id">Tenant ID</param>
    /// <param name="request">Tenant update request</param>
    /// <returns>Updated tenant</returns>
    [HttpPut("tenants/{id}")]
    [ProducesResponseType(typeof(TenantResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<TenantResponse>> UpdateTenantAsync(
        int id,
        [FromBody] UpdateTenantRequest request)
    {
        try
        {
            _logger.LogInformation("Updating tenant {TenantId}", id);

            var updatedTenant = await _tenantService.UpdateTenantAsync(id, request.Configuration, request.IsActive);
            if (updatedTenant == null)
            {
                return NotFound($"Tenant with ID {id} not found");
            }

            await _eventPublisher.PublishAsync(new TenantUpdatedEvent(updatedTenant, "Tenant updated via admin API"));

            var response = MapToTenantResponse(updatedTenant);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tenant {TenantId}", id);
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while updating the tenant");
        }
    }

    /// <summary>
    /// Deletes a tenant
    /// </summary>
    /// <param name="id">Tenant ID</param>
    /// <param name="purgeData">Whether to purge all tenant data</param>
    /// <returns>No content if successful</returns>
    [HttpDelete("tenants/{id}")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> DeleteTenantAsync(int id, [FromQuery] bool purgeData = false)
    {
        try
        {
            _logger.LogInformation("Deleting tenant {TenantId}, purgeData: {PurgeData}", id, purgeData);

            var deleted = await _tenantService.DeleteTenantAsync(id, purgeData);
            if (!deleted)
            {
                return NotFound($"Tenant with ID {id} not found");
            }

            await _eventPublisher.PublishAsync(new TenantDeletedEvent(id, purgeData, "Tenant deleted via admin API"));

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tenant {TenantId}", id);
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while deleting the tenant");
        }
    }

    #endregion

    #region System Health and Monitoring

    /// <summary>
    /// Gets comprehensive system health status
    /// </summary>
    /// <returns>System health information</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(SystemHealthResponse), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<SystemHealthResponse>> GetSystemHealthAsync()
    {
        try
        {
            _logger.LogDebug("Retrieving system health status");

            var healthStatus = await _healthCheckService.GetSystemHealthAsync();
            var systemInfo = await GetSystemInformationAsync();
            var performanceMetrics = await _monitoringService.GetPerformanceMetricsAsync();

            var response = new SystemHealthResponse
            {
                OverallStatus = healthStatus.OverallStatus,
                CheckedAt = DateTime.UtcNow,
                HealthChecks = healthStatus.HealthChecks.Select(MapToHealthCheckResponse).ToList(),
                SystemInformation = systemInfo,
                PerformanceMetrics = performanceMetrics,
                Uptime = GetSystemUptime(),
                Version = GetApplicationVersion()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system health");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while retrieving system health");
        }
    }

    /// <summary>
    /// Gets detailed performance metrics
    /// </summary>
    /// <param name="timeRange">Time range for metrics (1h, 24h, 7d, 30d)</param>
    /// <returns>Performance metrics</returns>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(PerformanceMetricsResponse), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<PerformanceMetricsResponse>> GetPerformanceMetricsAsync(
        [FromQuery] string timeRange = "1h")
    {
        try
        {
            _logger.LogInformation("Retrieving performance metrics for time range: {TimeRange}", timeRange);

            var metrics = await _monitoringService.GetDetailedMetricsAsync(timeRange);
            
            var response = new PerformanceMetricsResponse
            {
                TimeRange = timeRange,
                CollectedAt = DateTime.UtcNow,
                CpuUsage = metrics.CpuUsage,
                MemoryUsage = metrics.MemoryUsage,
                DiskUsage = metrics.DiskUsage,
                NetworkMetrics = metrics.NetworkMetrics,
                DatabaseMetrics = metrics.DatabaseMetrics,
                CacheMetrics = metrics.CacheMetrics,
                RequestMetrics = metrics.RequestMetrics,
                ErrorRates = metrics.ErrorRates,
                ResponseTimes = metrics.ResponseTimes
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance metrics");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while retrieving performance metrics");
        }
    }

    #endregion

    #region Cache Management

    /// <summary>
    /// Gets cache statistics
    /// </summary>
    /// <returns>Cache statistics</returns>
    [HttpGet("cache/stats")]
    [ProducesResponseType(typeof(CacheStatisticsResponse), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<CacheStatisticsResponse>> GetCacheStatisticsAsync()
    {
        try
        {
            _logger.LogDebug("Retrieving cache statistics");

            var cacheStats = await _cacheService.GetStatisticsAsync();
            
            var response = new CacheStatisticsResponse
            {
                TotalKeys = cacheStats.TotalKeys,
                MemoryUsage = cacheStats.MemoryUsage,
                HitRate = cacheStats.HitRate,
                MissRate = cacheStats.MissRate,
                EvictionCount = cacheStats.EvictionCount,
                ExpiredKeys = cacheStats.ExpiredKeys,
                CacheRegions = cacheStats.CacheRegions.Select(MapToCacheRegionResponse).ToList(),
                LastRefreshed = DateTime.UtcNow
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cache statistics");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while retrieving cache statistics");
        }
    }

    /// <summary>
    /// Clears cache by pattern or region
    /// </summary>
    /// <param name="request">Cache clear request</param>
    /// <returns>Cache clear results</returns>
    [HttpPost("cache/clear")]
    [ProducesResponseType(typeof(CacheClearResponse), (int)HttpStatusCode.OK)]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<CacheClearResponse>> ClearCacheAsync([FromBody] ClearCacheRequest request)
    {
        try
        {
            _logger.LogInformation("Clearing cache with pattern: {Pattern}, region: {Region}", request.Pattern, request.Region);

            var clearedCount = await _cacheService.ClearCacheAsync(request.Pattern, request.Region, request.Force);

            await _eventPublisher.PublishAsync(new CacheClearedEvent(request.Pattern, request.Region, clearedCount));

            var response = new CacheClearResponse
            {
                Pattern = request.Pattern,
                Region = request.Region,
                ClearedCount = clearedCount,
                ClearedAt = DateTime.UtcNow,
                Force = request.Force
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while clearing cache");
        }
    }

    /// <summary>
    /// Preloads cache with frequently accessed data
    /// </summary>
    /// <param name="request">Cache preload request</param>
    /// <returns>Cache preload results</returns>
    [HttpPost("cache/preload")]
    [ProducesResponseType(typeof(CachePreloadResponse), (int)HttpStatusCode.OK)]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<CachePreloadResponse>> PreloadCacheAsync([FromBody] PreloadCacheRequest request)
    {
        try
        {
            _logger.LogInformation("Preloading cache for regions: {Regions}", string.Join(",", request.Regions));

            var preloadResults = await _cacheService.PreloadCacheAsync(request.Regions, request.Priority);

            await _eventPublisher.PublishAsync(new CachePreloadedEvent(request.Regions, preloadResults.TotalPreloaded));

            var response = new CachePreloadResponse
            {
                Regions = request.Regions,
                TotalPreloaded = preloadResults.TotalPreloaded,
                PreloadDuration = preloadResults.Duration,
                PreloadedAt = DateTime.UtcNow,
                Results = preloadResults.RegionResults.Select(MapToPreloadResult).ToList()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preloading cache");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while preloading cache");
        }
    }

    #endregion

    #region Maintenance Operations

    /// <summary>
    /// Performs system maintenance tasks
    /// </summary>
    /// <param name="request">Maintenance request</param>
    /// <returns>Maintenance results</returns>
    [HttpPost("maintenance")]
    [ProducesResponseType(typeof(MaintenanceResponse), (int)HttpStatusCode.OK)]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<MaintenanceResponse>> PerformMaintenanceAsync([FromBody] MaintenanceRequest request)
    {
        try
        {
            _logger.LogInformation("Starting maintenance tasks: {Tasks}", string.Join(",", request.Tasks));

            var maintenanceResults = await _maintenanceService.PerformMaintenanceAsync(
                request.Tasks,
                request.ScheduledFor,
                request.MaxDuration);

            await _eventPublisher.PublishAsync(new MaintenanceCompletedEvent(
                request.Tasks,
                maintenanceResults.Duration,
                maintenanceResults.Success));

            var response = new MaintenanceResponse
            {
                Tasks = request.Tasks,
                StartedAt = maintenanceResults.StartedAt,
                CompletedAt = maintenanceResults.CompletedAt,
                Duration = maintenanceResults.Duration,
                Success = maintenanceResults.Success,
                Results = maintenanceResults.TaskResults.Select(MapToMaintenanceTaskResult).ToList(),
                Warnings = maintenanceResults.Warnings,
                NextScheduledMaintenance = await _maintenanceService.GetNextScheduledMaintenanceAsync()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during maintenance");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred during maintenance");
        }
    }

    /// <summary>
    /// Gets maintenance schedule
    /// </summary>
    /// <returns>Scheduled maintenance tasks</returns>
    [HttpGet("maintenance/schedule")]
    [ProducesResponseType(typeof(MaintenanceScheduleResponse), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<MaintenanceScheduleResponse>> GetMaintenanceScheduleAsync()
    {
        try
        {
            var schedule = await _maintenanceService.GetMaintenanceScheduleAsync();
            
            var response = new MaintenanceScheduleResponse
            {
                ScheduledTasks = schedule.ScheduledTasks.Select(MapToScheduledTask).ToList(),
                NextMaintenance = schedule.NextMaintenance,
                MaintenanceWindow = schedule.MaintenanceWindow,
                LastMaintenance = schedule.LastMaintenance,
                RecurringTasks = schedule.RecurringTasks.Select(MapToRecurringTask).ToList()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance schedule");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while retrieving maintenance schedule");
        }
    }

    #endregion

    #region Backup and Recovery

    /// <summary>
    /// Creates a system backup
    /// </summary>
    /// <param name="request">Backup request</param>
    /// <returns>Backup information</returns>
    [HttpPost("backup")]
    [ProducesResponseType(typeof(BackupResponse), (int)HttpStatusCode.OK)]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<BackupResponse>> CreateBackupAsync([FromBody] CreateBackupRequest request)
    {
        try
        {
            _logger.LogInformation("Creating backup: {BackupType}", request.BackupType);

            var backup = await _backupService.CreateBackupAsync(
                request.BackupType,
                request.IncludeAuditLogs,
                request.Compress,
                request.Encrypt);

            await _eventPublisher.PublishAsync(new BackupCreatedEvent(backup.BackupId, backup.BackupType, backup.Size));

            var response = new BackupResponse
            {
                BackupId = backup.BackupId,
                BackupType = backup.BackupType,
                CreatedAt = backup.CreatedAt,
                Size = backup.Size,
                Location = backup.Location,
                Compressed = backup.Compressed,
                Encrypted = backup.Encrypted,
                Checksum = backup.Checksum,
                ExpiresAt = backup.ExpiresAt,
                Status = backup.Status
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating backup");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while creating backup");
        }
    }

    /// <summary>
    /// Lists available backups
    /// </summary>
    /// <param name="backupType">Filter by backup type</param>
    /// <param name="limit">Maximum number of backups to return</param>
    /// <returns>List of backups</returns>
    [HttpGet("backups")]
    [ProducesResponseType(typeof(IEnumerable<BackupSummaryResponse>), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<IEnumerable<BackupSummaryResponse>>> GetBackupsAsync(
        [FromQuery] string? backupType = null,
        [FromQuery] int limit = 50)
    {
        try
        {
            var backups = await _backupService.GetBackupsAsync(backupType, limit);
            
            var response = backups.Select(b => new BackupSummaryResponse
            {
                BackupId = b.BackupId,
                BackupType = b.BackupType,
                CreatedAt = b.CreatedAt,
                Size = b.Size,
                Status = b.Status,
                ExpiresAt = b.ExpiresAt
            });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving backups");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while retrieving backups");
        }
    }

    #endregion

    #region System Information

    /// <summary>
    /// Gets comprehensive system information
    /// </summary>
    /// <returns>System information</returns>
    [HttpGet("system-info")]
    [ProducesResponseType(typeof(SystemInformationResponse), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<SystemInformationResponse>> GetSystemInformationAsync()
    {
        try
        {
            var systemInfo = await GetSystemInformationAsync();
            return Ok(systemInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system information");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while retrieving system information");
        }
    }

    #endregion

    #region Private Helper Methods

    private TenantResponse MapToTenantResponse(Tenant tenant)
    {
        return new TenantResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            IsActive = tenant.IsActive,
            CreatedAt = tenant.CreatedAt,
            UpdatedAt = tenant.UpdatedAt,
            Configuration = tenant.Configuration,
            UserCount = tenant.UserCount,
            LastActivity = tenant.LastActivity
        };
    }

    private HealthCheckResponse MapToHealthCheckResponse(HealthCheckResult healthCheck)
    {
        return new HealthCheckResponse
        {
            Name = healthCheck.Name,
            Status = healthCheck.Status.ToString(),
            Duration = healthCheck.Duration,
            Description = healthCheck.Description,
            Data = healthCheck.Data,
            Exception = healthCheck.Exception?.Message
        };
    }

    private CacheRegionResponse MapToCacheRegionResponse(CacheRegionStatistics region)
    {
        return new CacheRegionResponse
        {
            Name = region.Name,
            KeyCount = region.KeyCount,
            MemoryUsage = region.MemoryUsage,
            HitRate = region.HitRate,
            LastAccessed = region.LastAccessed
        };
    }

    private CachePreloadResult MapToPreloadResult(RegionPreloadResult result)
    {
        return new CachePreloadResult
        {
            Region = result.Region,
            PreloadedCount = result.PreloadedCount,
            Duration = result.Duration,
            Success = result.Success,
            ErrorMessage = result.ErrorMessage
        };
    }

    private MaintenanceTaskResult MapToMaintenanceTaskResult(TaskResult result)
    {
        return new MaintenanceTaskResult
        {
            TaskName = result.TaskName,
            Success = result.Success,
            Duration = result.Duration,
            Message = result.Message,
            Details = result.Details
        };
    }

    private ScheduledMaintenanceTask MapToScheduledTask(ScheduledTask task)
    {
        return new ScheduledMaintenanceTask
        {
            TaskName = task.TaskName,
            ScheduledFor = task.ScheduledFor,
            EstimatedDuration = task.EstimatedDuration,
            Priority = task.Priority,
            Description = task.Description
        };
    }

    private RecurringMaintenanceTask MapToRecurringTask(RecurringTask task)
    {
        return new RecurringMaintenanceTask
        {
            TaskName = task.TaskName,
            Schedule = task.Schedule,
            LastRun = task.LastRun,
            NextRun = task.NextRun,
            Enabled = task.Enabled
        };
    }

    private async Task<SystemInformationResponse> GetSystemInformationAsync()
    {
        return new SystemInformationResponse
        {
            ApplicationName = "Access Control System",
            Version = GetApplicationVersion(),
            BuildDate = GetBuildDate(),
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
            MachineName = Environment.MachineName,
            ProcessorCount = Environment.ProcessorCount,
            OSVersion = Environment.OSVersion.ToString(),
            WorkingSet = Environment.WorkingSet,
            ManagedMemory = GC.GetTotalMemory(false),
            Uptime = GetSystemUptime(),
            StartTime = GetApplicationStartTime(),
            ThreadCount = System.Diagnostics.Process.GetCurrentProcess().Threads.Count,
            HandleCount = System.Diagnostics.Process.GetCurrentProcess().HandleCount
        };
    }

    private string GetApplicationVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "Unknown";
    }

    private DateTime GetBuildDate()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var attribute = assembly.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>();
        if (DateTime.TryParse(attribute?.InformationalVersion, out var buildDate))
        {
            return buildDate;
        }
        return File.GetLastWriteTime(assembly.Location);
    }

    private TimeSpan GetSystemUptime()
    {
        return DateTime.UtcNow - GetApplicationStartTime();
    }

    private DateTime GetApplicationStartTime()
    {
        return System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
    }

    private ValidationProblemDetails CreateValidationProblemDetails(ValidationResult validationResult)
    {
        var problemDetails = new ValidationProblemDetails();
        
        foreach (var error in validationResult.Errors)
        {
            var memberNames = new[] { error.PropertyName ?? "General" };
            foreach (var memberName in memberNames)
            {
                if (!problemDetails.Errors.ContainsKey(memberName))
                {
                    problemDetails.Errors[memberName] = new List<string>();
                }
                ((List<string>)problemDetails.Errors[memberName]).Add(error.ErrorMessage ?? "Validation error");
            }
        }

        return problemDetails;
    }

    #endregion
}