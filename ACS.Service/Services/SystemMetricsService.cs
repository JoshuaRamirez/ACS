using ACS.Service.Data;
using ACS.Service.Infrastructure;
using ACS.Service.Requests;
using ACS.Service.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ACS.Service.Services;

/// <summary>
/// Service for system metrics and diagnostic operations
/// Provides system overview, health status, and diagnostic information
/// Uses proper service layer abstraction without direct DbContext exposure to handlers
/// </summary>
public class SystemMetricsService : ISystemMetricsService
{
    private readonly InMemoryEntityGraph _entityGraph;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<SystemMetricsService> _logger;

    public SystemMetricsService(
        InMemoryEntityGraph entityGraph,
        ApplicationDbContext dbContext,
        ILogger<SystemMetricsService> logger)
    {
        _entityGraph = entityGraph;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<SystemOverviewResponse> GetSystemOverviewAsync(SystemOverviewRequest request)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Getting system overview for tenant {TenantId}", request.TenantId);

            // Get counts from database (proper service layer responsibility)
            var usersCount = await _dbContext.Users.CountAsync();
            var groupsCount = await _dbContext.Groups.CountAsync();
            var rolesCount = await _dbContext.Roles.CountAsync();

            var data = new SystemOverviewData
            {
                Timestamp = DateTime.UtcNow,
                Status = "Healthy",
                UsersCount = usersCount,
                GroupsCount = groupsCount,
                RolesCount = rolesCount,
                Uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()
            };

            _logger.LogDebug("System overview retrieved in {Duration}ms - Users: {Users}, Groups: {Groups}, Roles: {Roles}", 
                stopwatch.ElapsedMilliseconds, usersCount, groupsCount, rolesCount);

            return new SystemOverviewResponse
            {
                Data = data,
                Success = true,
                Message = "System overview retrieved successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system overview for tenant {TenantId}", request.TenantId);
            
            return new SystemOverviewResponse
            {
                Success = false,
                Message = "Failed to retrieve system overview",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<MigrationHistoryResponse> GetMigrationHistoryAsync(MigrationHistoryRequest request)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Getting migration history for tenant {TenantId}", request.TenantId);

            // Get applied migrations from database
            var appliedMigrations = await _dbContext.Database.GetAppliedMigrationsAsync();
            
            var migrations = appliedMigrations.Select(migration => new MigrationData
            {
                Id = migration,
                Name = migration.Split('_').Skip(1).FirstOrDefault() ?? migration,
                AppliedDate = DateTime.UtcNow, // EF Core doesn't track application date by default
                Status = "Applied"
            }).ToList();

            _logger.LogDebug("Migration history retrieved in {Duration}ms - {Count} migrations found", 
                stopwatch.ElapsedMilliseconds, migrations.Count);

            return new MigrationHistoryResponse
            {
                Migrations = migrations,
                Success = true,
                Message = $"Retrieved {migrations.Count} applied migrations"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting migration history for tenant {TenantId}", request.TenantId);
            
            return new MigrationHistoryResponse
            {
                Success = false,
                Message = "Failed to retrieve migration history",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public Task<SystemDiagnosticsResponse> GetSystemDiagnosticsAsync(SystemDiagnosticsRequest request)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Getting system diagnostics for tenant {TenantId}", request.TenantId);

            var process = Process.GetCurrentProcess();

            var data = new SystemDiagnosticsData
            {
                MachineName = Environment.MachineName,
                ProcessId = process.Id.ToString(),
                WorkingSetMemory = process.WorkingSet64,
                ProcessorTime = process.TotalProcessorTime,
                StartTime = process.StartTime.ToUniversalTime(),
                Version = Environment.Version.ToString()
            };

            _logger.LogDebug("System diagnostics retrieved in {Duration}ms", stopwatch.ElapsedMilliseconds);

            return Task.FromResult(new SystemDiagnosticsResponse
            {
                Data = data,
                Success = true,
                Message = "System diagnostics retrieved successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system diagnostics for tenant {TenantId}", request.TenantId);
            
            return Task.FromResult(new SystemDiagnosticsResponse
            {
                Success = false,
                Message = "Failed to retrieve system diagnostics",
                Errors = new List<string> { ex.Message }
            });
        }
    }
}