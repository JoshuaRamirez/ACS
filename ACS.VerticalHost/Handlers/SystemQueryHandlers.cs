using ACS.VerticalHost.Services;
using ACS.VerticalHost.Commands;
using ACS.Service.Data;
using ACS.Infrastructure.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using ServiceMigrationValidationResult = ACS.Service.Services.MigrationValidationResult;
using ServiceMigrationInfo = ACS.Service.Services.MigrationInfo;
using ServiceIMigrationValidationService = ACS.Service.Services.IMigrationValidationService;

namespace ACS.VerticalHost.Handlers;

public class GetSystemOverviewQueryHandler : IQueryHandler<GetSystemOverviewQuery, SystemOverview>
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<GetSystemOverviewQueryHandler> _logger;

    public GetSystemOverviewQueryHandler(
        ApplicationDbContext context,
        ILogger<GetSystemOverviewQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SystemOverview> HandleAsync(GetSystemOverviewQuery query, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting system overview for tenant {TenantId}", query.TenantId);

            var overview = new SystemOverview
            {
                Timestamp = DateTime.UtcNow,
                Status = "Healthy",
                UsersCount = await _context.Users.CountAsync(cancellationToken),
                GroupsCount = await _context.Groups.CountAsync(cancellationToken),
                RolesCount = await _context.Roles.CountAsync(cancellationToken),
                Uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()
            };

            return overview;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system overview");
            return new SystemOverview
            {
                Timestamp = DateTime.UtcNow,
                Status = "Error",
                Uptime = TimeSpan.Zero
            };
        }
    }
}

public class GetHealthStatusQueryHandler : IQueryHandler<GetHealthStatusQuery, HealthStatus>
{
    private readonly ILogger<GetHealthStatusQueryHandler> _logger;

    public GetHealthStatusQueryHandler(ILogger<GetHealthStatusQueryHandler> logger)
    {
        _logger = logger;
    }

    public async Task<HealthStatus> HandleAsync(GetHealthStatusQuery query, CancellationToken cancellationToken)
    {
        try
        {
            await Task.CompletedTask; // For async signature

            _logger.LogDebug("Getting health status for tenant {TenantId}", query.TenantId);

            var health = new HealthStatus
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime(),
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
            };

            return health;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting health status");
            return new HealthStatus
            {
                Status = "Unhealthy",
                Timestamp = DateTime.UtcNow,
                Environment = "Error"
            };
        }
    }
}

public class GetMigrationHistoryQueryHandler : IQueryHandler<GetMigrationHistoryQuery, List<MigrationInfo>>
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<GetMigrationHistoryQueryHandler> _logger;

    public GetMigrationHistoryQueryHandler(
        ApplicationDbContext context,
        ILogger<GetMigrationHistoryQueryHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<MigrationInfo>> HandleAsync(GetMigrationHistoryQuery query, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting migration history for tenant {TenantId}", query.TenantId);

            // Get applied migrations from database
            var appliedMigrations = await _context.Database.GetAppliedMigrationsAsync();
            
            var migrationInfos = appliedMigrations.Select(migration => new Commands.MigrationInfo
            {
                Id = migration,
                Name = migration.Split('_').Skip(1).FirstOrDefault() ?? migration,
                AppliedDate = DateTime.UtcNow, // EF Core doesn't track application date by default
                Status = "Applied"
            }).ToList();

            return migrationInfos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting migration history");
            return new List<Commands.MigrationInfo>();
        }
    }
}

public class ValidateMigrationsCommandHandler : ICommandHandler<ValidateMigrationsCommand, MigrationValidationResult>
{
    private readonly ServiceIMigrationValidationService _migrationService;
    private readonly ILogger<ValidateMigrationsCommandHandler> _logger;

    public ValidateMigrationsCommandHandler(
        ServiceIMigrationValidationService migrationService,
        ILogger<ValidateMigrationsCommandHandler> logger)
    {
        _migrationService = migrationService;
        _logger = logger;
    }

    public async Task<MigrationValidationResult> HandleAsync(ValidateMigrationsCommand command, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Validating migrations for tenant {TenantId}", command.TenantId);

            var healthCheck = await _migrationService.CheckMigrationHealthAsync();

            return new MigrationValidationResult
            {
                IsValid = healthCheck.IsHealthy,
                Issues = healthCheck.Errors?.ToList() ?? new List<string>(),
                ValidatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating migrations");
            return new MigrationValidationResult
            {
                IsValid = false,
                Issues = new List<string> { $"Validation error: {ex.Message}" },
                ValidatedAt = DateTime.UtcNow
            };
        }
    }
}

public class GetSystemInfoQueryHandler : IQueryHandler<GetSystemInfoQuery, SystemDiagnosticInfo>
{
    private readonly IDiagnosticService _diagnosticService;
    private readonly ILogger<GetSystemInfoQueryHandler> _logger;

    public GetSystemInfoQueryHandler(
        IDiagnosticService diagnosticService,
        ILogger<GetSystemInfoQueryHandler> logger)
    {
        _diagnosticService = diagnosticService;
        _logger = logger;
    }

    public async Task<SystemDiagnosticInfo> HandleAsync(GetSystemInfoQuery query, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting system diagnostic info for tenant {TenantId}", query.TenantId);

            var process = Process.GetCurrentProcess();
            var diagnostics = await _diagnosticService.GetSystemInfoAsync();

            return new SystemDiagnosticInfo
            {
                MachineName = Environment.MachineName,
                ProcessId = process.Id.ToString(),
                WorkingSetMemory = process.WorkingSet64,
                ProcessorTime = process.TotalProcessorTime,
                StartTime = process.StartTime.ToUniversalTime(),
                Version = diagnostics?.RuntimeVersion ?? "Unknown"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system diagnostic info");
            return new SystemDiagnosticInfo
            {
                MachineName = Environment.MachineName,
                ProcessId = Environment.ProcessId.ToString(),
                StartTime = DateTime.UtcNow,
                Version = "Error"
            };
        }
    }
}