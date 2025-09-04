using ACS.VerticalHost.Services;
using ACS.VerticalHost.Commands;
using ACS.Infrastructure.RateLimiting;
using Microsoft.Extensions.Logging;
using static ACS.VerticalHost.Services.HandlerErrorHandling;
using static ACS.VerticalHost.Services.HandlerExtensions;
using InfrastructureRL = ACS.Infrastructure.RateLimiting;
using Commands = ACS.VerticalHost.Commands;
using CommandRateLimitStatus = ACS.VerticalHost.Commands.RateLimitStatus;
using CommandAggregatedRateLimitMetrics = ACS.VerticalHost.Commands.AggregatedRateLimitMetrics;

namespace ACS.VerticalHost.Handlers;

public class ResetRateLimitCommandHandler : ICommandHandler<ResetRateLimitCommand>
{
    private readonly IRateLimitingService _rateLimitingService;
    private readonly RateLimitingMetricsService _metricsService;
    private readonly ILogger<ResetRateLimitCommandHandler> _logger;

    public ResetRateLimitCommandHandler(
        IRateLimitingService rateLimitingService,
        RateLimitingMetricsService metricsService,
        ILogger<ResetRateLimitCommandHandler> logger)
    {
        _rateLimitingService = rateLimitingService;
        _metricsService = metricsService;
        _logger = logger;
    }

    public async Task HandleAsync(ResetRateLimitCommand command, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(ResetRateLimitCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, 
            new { TenantId = command.TenantId, Key = command.Key, Reason = command.Reason }, correlationId);

        try
        {
            await _rateLimitingService.ResetRateLimitAsync(command.TenantId, command.Key);
            
            _metricsService.RecordRateLimitReset(command.TenantId, command.Key, command.Reason);
            
            LogCommandSuccess(_logger, context, 
                new { TenantId = command.TenantId, Key = command.Key, Reason = command.Reason }, correlationId);
        }
        catch (Exception ex)
        {
            // Re-throw to maintain clean architecture - HandleCommandError always throws
#pragma warning disable CS4014 // Fire and forget is intentional - method always throws
            HandleCommandError<Task>(_logger, ex, context, correlationId);
#pragma warning restore CS4014
            throw; // This line never executes but satisfies compiler
        }
    }
}

public class TestRateLimitCommandHandler : ICommandHandler<TestRateLimitCommand, RateLimitTestResult>
{
    private readonly IRateLimitingService _rateLimitingService;
    private readonly ILogger<TestRateLimitCommandHandler> _logger;

    public TestRateLimitCommandHandler(
        IRateLimitingService rateLimitingService,
        ILogger<TestRateLimitCommandHandler> logger)
    {
        _rateLimitingService = rateLimitingService;
        _logger = logger;
    }

    public async Task<RateLimitTestResult> HandleAsync(TestRateLimitCommand command, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(TestRateLimitCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, 
            new { TenantId = command.TenantId, RequestLimit = command.RequestLimit, NumberOfRequests = command.NumberOfRequests }, correlationId);

        try
        {
            var key = $"test_{command.TenantId}_{Guid.NewGuid():N}";
            
            var policy = new InfrastructureRL.RateLimitPolicy
            {
                RequestLimit = command.RequestLimit,
                WindowSizeSeconds = command.WindowSizeSeconds,
                PolicyName = "test"
            };
            
            var results = new List<RateLimitTestRequestResult>();
            
            // Perform multiple test requests
            for (int i = 0; i < command.NumberOfRequests; i++)
            {
                var result = await _rateLimitingService.CheckRateLimitAsync(command.TenantId, key, policy);
                results.Add(new Commands.RateLimitTestRequestResult
                {
                    RequestNumber = i + 1,
                    IsAllowed = result.IsAllowed,
                    Remaining = result.RemainingRequests,
                    ResetTime = result.ResetTimeSeconds
                });
                
                if (command.DelayBetweenRequests > 0)
                {
                    await Task.Delay(command.DelayBetweenRequests, cancellationToken);
                }
            }
            
            // Clean up test data
            await _rateLimitingService.ResetRateLimitAsync(command.TenantId, key);
            
            var testResult = new Commands.RateLimitTestResult
            {
                TestConfiguration = new Commands.RateLimitTestConfiguration
                {
                    RequestLimit = command.RequestLimit,
                    WindowSizeSeconds = command.WindowSizeSeconds,
                    NumberOfRequests = command.NumberOfRequests
                },
                Results = results
            };

            LogCommandSuccess(_logger, context, 
                new { TenantId = command.TenantId, TestRequests = command.NumberOfRequests, CompletedRequests = results.Count }, correlationId);
            return testResult;
        }
        catch (Exception ex)
        {
            return HandleCommandError<RateLimitTestResult>(_logger, ex, context, correlationId);
        }
    }
}

public class GetRateLimitStatusQueryHandler : IQueryHandler<GetRateLimitStatusQuery, CommandRateLimitStatus>
{
    private readonly IRateLimitingService _rateLimitingService;
    private readonly ILogger<GetRateLimitStatusQueryHandler> _logger;

    public GetRateLimitStatusQueryHandler(
        IRateLimitingService rateLimitingService,
        ILogger<GetRateLimitStatusQueryHandler> logger)
    {
        _rateLimitingService = rateLimitingService;
        _logger = logger;
    }

    public async Task<CommandRateLimitStatus> HandleAsync(GetRateLimitStatusQuery query, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetRateLimitStatusQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { TenantId = query.TenantId, Key = query.Key }, correlationId);

        try
        {
            var policy = new InfrastructureRL.RateLimitPolicy
            {
                RequestLimit = query.Policy.RequestLimit,
                WindowSizeSeconds = query.Policy.WindowSizeSeconds,
                PolicyName = query.Policy.PolicyName
            };
            
            var status = await _rateLimitingService.GetRateLimitStatusAsync(query.TenantId, query.Key, policy);
            
            var result = new Commands.RateLimitStatus
            {
                TenantId = status.TenantId,
                RequestCount = status.RequestCount,
                RequestLimit = status.RequestLimit,
                RemainingRequests = status.RemainingRequests,
                WindowStartTime = status.WindowStartTime,
                WindowEndTime = status.WindowEndTime,
                Algorithm = (Commands.RateLimitAlgorithm)status.Algorithm,
                PolicyName = status.PolicyName,
                IsNearLimit = status.IsNearLimit
            };

            LogQuerySuccess(_logger, context, 
                new { TenantId = query.TenantId, RequestCount = result.RequestCount, IsNearLimit = result.IsNearLimit }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<CommandRateLimitStatus>(_logger, ex, context, correlationId);
        }
    }
}

public class GetActiveLimitsQueryHandler : IQueryHandler<GetActiveLimitsQuery, List<ActiveRateLimit>>
{
    private readonly IRateLimitingService _rateLimitingService;
    private readonly ILogger<GetActiveLimitsQueryHandler> _logger;

    public GetActiveLimitsQueryHandler(
        IRateLimitingService rateLimitingService,
        ILogger<GetActiveLimitsQueryHandler> logger)
    {
        _rateLimitingService = rateLimitingService;
        _logger = logger;
    }

    public async Task<List<ActiveRateLimit>> HandleAsync(GetActiveLimitsQuery query, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetActiveLimitsQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { TenantId = query.TenantId }, correlationId);

        try
        {
            var activeLimits = await _rateLimitingService.GetActiveLimitsAsync(query.TenantId);
            
            var result = activeLimits.Select(limit => new Commands.ActiveRateLimit
            {
                Key = limit.Key,
                RequestCount = limit.RequestCount,
                RequestLimit = limit.RequestLimit,
                CreatedAt = limit.CreatedAt,
                ExpiresAt = limit.ExpiresAt,
                PolicyName = limit.PolicyName,
                Algorithm = (Commands.RateLimitAlgorithm)limit.Algorithm,
                ClientInfo = limit.ClientInfo
            }).ToList();

            LogQuerySuccess(_logger, context, 
                new { TenantId = query.TenantId, ActiveLimitCount = result.Count }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<List<ActiveRateLimit>>(_logger, ex, context, correlationId);
        }
    }
}

public class GetRateLimitMetricsQueryHandler : IQueryHandler<GetRateLimitMetricsQuery, RateLimitMetrics>
{
    private readonly RateLimitingMetricsService _metricsService;
    private readonly ILogger<GetRateLimitMetricsQueryHandler> _logger;

    public GetRateLimitMetricsQueryHandler(
        RateLimitingMetricsService metricsService,
        ILogger<GetRateLimitMetricsQueryHandler> logger)
    {
        _metricsService = metricsService;
        _logger = logger;
    }

    public async Task<RateLimitMetrics> HandleAsync(GetRateLimitMetricsQuery query, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // For async signature
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetRateLimitMetricsQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { TenantId = query.TenantId }, correlationId);

        try
        {
            var tenantMetrics = _metricsService.GetTenantMetrics(query.TenantId);
            
            var result = new Commands.RateLimitMetrics
            {
                TenantId = tenantMetrics.TenantId,
                TotalRequests = tenantMetrics.TotalRequests,
                RequestsAllowed = tenantMetrics.RequestsAllowed,
                RequestsBlocked = tenantMetrics.RequestsBlocked,
                BlockRate = tenantMetrics.BlockRate,
                AverageRemainingRequests = tenantMetrics.AverageRemainingRequests,
                LastActivity = tenantMetrics.LastActivity
            };

            LogQuerySuccess(_logger, context, 
                new { TenantId = query.TenantId, TotalRequests = result.TotalRequests, BlockRate = result.BlockRate }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<RateLimitMetrics>(_logger, ex, context, correlationId);
        }
    }
}

public class GetAggregatedRateLimitMetricsQueryHandler : IQueryHandler<GetAggregatedRateLimitMetricsQuery, CommandAggregatedRateLimitMetrics>
{
    private readonly RateLimitingMetricsService _metricsService;
    private readonly ILogger<GetAggregatedRateLimitMetricsQueryHandler> _logger;

    public GetAggregatedRateLimitMetricsQueryHandler(
        RateLimitingMetricsService metricsService,
        ILogger<GetAggregatedRateLimitMetricsQueryHandler> logger)
    {
        _metricsService = metricsService;
        _logger = logger;
    }

    public async Task<CommandAggregatedRateLimitMetrics> HandleAsync(GetAggregatedRateLimitMetricsQuery query, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // For async signature
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetAggregatedRateLimitMetricsQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { }, correlationId);

        try
        {
            var metrics = _metricsService.GetAggregatedMetrics();
            
            var result = new Commands.AggregatedRateLimitMetrics
            {
                TotalTenants = metrics.ActiveTenants,
                TotalRequests = metrics.TotalRequests,
                TotalBlocked = metrics.TotalBlocked,
                OverallBlockRate = metrics.BlockRate,
                RequestsByTenant = new Dictionary<string, long>(),
                BlockRatesByTenant = new Dictionary<string, double>()
            };

            LogQuerySuccess(_logger, context, 
                new { TotalTenants = result.TotalTenants, TotalRequests = result.TotalRequests, BlockRate = result.OverallBlockRate }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<CommandAggregatedRateLimitMetrics>(_logger, ex, context, correlationId);
        }
    }
}

public class GetRateLimitHealthStatusQueryHandler : IQueryHandler<GetRateLimitHealthStatusQuery, RateLimitHealthStatus>
{
    private readonly RateLimitingMonitoringService _monitoringService;
    private readonly ILogger<GetRateLimitHealthStatusQueryHandler> _logger;

    public GetRateLimitHealthStatusQueryHandler(
        RateLimitingMonitoringService monitoringService,
        ILogger<GetRateLimitHealthStatusQueryHandler> logger)
    {
        _monitoringService = monitoringService;
        _logger = logger;
    }

    public async Task<RateLimitHealthStatus> HandleAsync(GetRateLimitHealthStatusQuery query, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetRateLimitHealthStatusQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { }, correlationId);

        try
        {
            var health = await _monitoringService.GetHealthStatusAsync();
            
            var result = new Commands.RateLimitHealthStatus
            {
                IsHealthy = health.IsHealthy,
                LastCheck = health.LastCheck,
                StorageResponseTime = health.StorageResponseTime,
                TotalActiveEntries = health.TotalActiveEntries,
                ExpiredEntries = health.ExpiredEntries,
                BlockRate = health.BlockRate,
                ActiveTenants = health.ActiveTenants,
                Issues = health.Issues.ToList()
            };

            LogQuerySuccess(_logger, context, 
                new { IsHealthy = result.IsHealthy, ActiveTenants = result.ActiveTenants, IssueCount = result.Issues.Count }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<RateLimitHealthStatus>(_logger, ex, context, correlationId);
        }
    }
}