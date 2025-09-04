using ACS.VerticalHost.Services;
using ACS.VerticalHost.Commands;
using MigrationValidationResult = ACS.VerticalHost.Commands.MigrationValidationResult;
using ACS.Service.Services;
using ACS.Service.Requests;
using ACS.Infrastructure.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using static ACS.VerticalHost.Services.HandlerErrorHandling;
using static ACS.VerticalHost.Services.HandlerExtensions;
using ServiceMigrationValidationResult = ACS.Service.Services.MigrationValidationResult;
using ServiceMigrationInfo = ACS.Service.Services.MigrationInfo;
using ServiceIMigrationValidationService = ACS.Service.Services.IMigrationValidationService;

namespace ACS.VerticalHost.Handlers;

public class GetSystemOverviewQueryHandler : IQueryHandler<GetSystemOverviewQuery, SystemOverview>
{
    private readonly ISystemMetricsService _systemMetricsService;
    private readonly ILogger<GetSystemOverviewQueryHandler> _logger;

    public GetSystemOverviewQueryHandler(
        ISystemMetricsService systemMetricsService,
        ILogger<GetSystemOverviewQueryHandler> logger)
    {
        _systemMetricsService = systemMetricsService;
        _logger = logger;
    }

    public async Task<SystemOverview> HandleAsync(GetSystemOverviewQuery query, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetSystemOverviewQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { TenantId = query.TenantId }, correlationId);

        try
        {
            var request = new SystemOverviewRequest
            {
                TenantId = query.TenantId,
                RequestedBy = "system"
            };

            var response = await _systemMetricsService.GetSystemOverviewAsync(request);

            if (response.Success && response.Data != null)
            {
                var result = new SystemOverview
                {
                    Timestamp = response.Data.Timestamp,
                    Status = response.Data.Status,
                    UsersCount = response.Data.UsersCount,
                    GroupsCount = response.Data.GroupsCount,
                    RolesCount = response.Data.RolesCount,
                    Uptime = response.Data.Uptime
                };
                
                LogQuerySuccess(_logger, context, new { TenantId = query.TenantId, Status = response.Data.Status }, correlationId);
                return result;
            }
            else
            {
                var error = $"System metrics service returned error: {response.Message}";
                _logger.LogError("{Error}. CorrelationId: {CorrelationId}", error, correlationId);
                throw new InvalidOperationException(error);
            }
        }
        catch (Exception ex)
        {
            return HandleQueryError<SystemOverview>(_logger, ex, context, correlationId);
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
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetHealthStatusQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { TenantId = query.TenantId }, correlationId);

        try
        {
            await Task.CompletedTask; // For async signature

            var health = new HealthStatus
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime(),
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
            };

            LogQuerySuccess(_logger, context, new { TenantId = query.TenantId, Status = health.Status }, correlationId);
            return health;
        }
        catch (Exception ex)
        {
            return HandleQueryError<HealthStatus>(_logger, ex, context, correlationId);
        }
    }
}

public class GetMigrationHistoryQueryHandler : IQueryHandler<GetMigrationHistoryQuery, List<Commands.MigrationInfo>>
{
    private readonly ISystemMetricsService _systemMetricsService;
    private readonly ILogger<GetMigrationHistoryQueryHandler> _logger;

    public GetMigrationHistoryQueryHandler(
        ISystemMetricsService systemMetricsService,
        ILogger<GetMigrationHistoryQueryHandler> logger)
    {
        _systemMetricsService = systemMetricsService;
        _logger = logger;
    }

    public async Task<List<Commands.MigrationInfo>> HandleAsync(GetMigrationHistoryQuery query, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetMigrationHistoryQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { TenantId = query.TenantId }, correlationId);

        try
        {
            var request = new MigrationHistoryRequest
            {
                TenantId = query.TenantId,
                RequestedBy = "system"
            };

            var response = await _systemMetricsService.GetMigrationHistoryAsync(request);

            if (response.Success)
            {
                var result = response.Migrations.Select(m => new Commands.MigrationInfo
                {
                    Id = m.Id,
                    Name = m.Name,
                    AppliedDate = m.AppliedDate,
                    Status = m.Status
                }).ToList();
                
                LogQuerySuccess(_logger, context, new { TenantId = query.TenantId, Count = result.Count }, correlationId);
                return result;
            }
            else
            {
                var error = $"System metrics service returned error: {response.Message}";
                _logger.LogError("{Error}. CorrelationId: {CorrelationId}", error, correlationId);
                throw new InvalidOperationException(error);
            }
        }
        catch (Exception ex)
        {
            return HandleQueryError<List<Commands.MigrationInfo>>(_logger, ex, context, correlationId);
        }
    }
}

public class ValidateMigrationsCommandHandler : ICommandHandler<ValidateMigrationsCommand, Commands.MigrationValidationResult>
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

    public async Task<Commands.MigrationValidationResult> HandleAsync(ValidateMigrationsCommand command, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(ValidateMigrationsCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { TenantId = command.TenantId }, correlationId);

        try
        {
            var healthCheck = await _migrationService.CheckMigrationHealthAsync();

            var result = new MigrationValidationResult
            {
                IsValid = healthCheck.IsHealthy,
                Issues = healthCheck.Errors?.ToList() ?? new List<string>(),
                ValidatedAt = DateTime.UtcNow
            };
            
            LogCommandSuccess(_logger, context, 
                new { TenantId = command.TenantId, IsValid = result.IsValid, IssueCount = result.Issues.Count }, 
                correlationId);
                
            return result;
        }
        catch (Exception ex)
        {
            // For migration validation, return error result instead of throwing
            // to provide operational details to the caller
            _logger.LogError(ex, "Error validating migrations for tenant {TenantId}. CorrelationId: {CorrelationId}", 
                command.TenantId, correlationId);
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
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetSystemInfoQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { TenantId = query.TenantId }, correlationId);

        try
        {
            var process = Process.GetCurrentProcess();
            var diagnostics = await _diagnosticService.GetSystemInfoAsync();

            var result = new SystemDiagnosticInfo
            {
                MachineName = Environment.MachineName,
                ProcessId = process.Id.ToString(),
                WorkingSetMemory = process.WorkingSet64,
                ProcessorTime = process.TotalProcessorTime,
                StartTime = process.StartTime.ToUniversalTime(),
                Version = diagnostics?.RuntimeVersion ?? "Unknown"
            };
            
            LogQuerySuccess(_logger, context, 
                new { TenantId = query.TenantId, MachineName = result.MachineName, ProcessId = result.ProcessId }, 
                correlationId);
                
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<SystemDiagnosticInfo>(_logger, ex, context, correlationId);
        }
    }
}