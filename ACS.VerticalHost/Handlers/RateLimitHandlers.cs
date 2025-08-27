using ACS.VerticalHost.Services;
using ACS.VerticalHost.Commands;
using ACS.Infrastructure.RateLimiting;
using Microsoft.Extensions.Logging;
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

    public async Task<object?> HandleAsync(ResetRateLimitCommand command, CancellationToken cancellationToken)
    {
        try
        {
            await _rateLimitingService.ResetRateLimitAsync(command.TenantId, command.Key);
            
            _metricsService.RecordRateLimitReset(command.TenantId, command.Key, command.Reason);
            
            _logger.LogInformation("Rate limit reset for tenant {TenantId}, key {Key}, reason: {Reason}",
                command.TenantId, command.Key, command.Reason);
            
            return null; // ICommand returns object?
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting rate limit for tenant {TenantId}, key {Key}",
                command.TenantId, command.Key);
            throw;
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
            
            _logger.LogInformation("Rate limit test completed for tenant {TenantId}", command.TenantId);
            
            return new Commands.RateLimitTestResult
            {
                TestConfiguration = new Commands.RateLimitTestConfiguration
                {
                    RequestLimit = command.RequestLimit,
                    WindowSizeSeconds = command.WindowSizeSeconds,
                    NumberOfRequests = command.NumberOfRequests
                },
                Results = results
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing rate limit for tenant {TenantId}", command.TenantId);
            throw;
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
        try
        {
            var policy = new InfrastructureRL.RateLimitPolicy
            {
                RequestLimit = query.Policy.RequestLimit,
                WindowSizeSeconds = query.Policy.WindowSizeSeconds,
                PolicyName = query.Policy.PolicyName
            };
            
            var status = await _rateLimitingService.GetRateLimitStatusAsync(query.TenantId, query.Key, policy);
            
            return new Commands.RateLimitStatus
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rate limit status for tenant {TenantId}, key {Key}",
                query.TenantId, query.Key);
            throw;
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
        try
        {
            var activeLimits = await _rateLimitingService.GetActiveLimitsAsync(query.TenantId);
            
            return activeLimits.Select(limit => new Commands.ActiveRateLimit
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active rate limits for tenant {TenantId}", query.TenantId);
            throw;
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

        try
        {
            var tenantMetrics = _metricsService.GetTenantMetrics(query.TenantId);
            
            return new Commands.RateLimitMetrics
            {
                TenantId = tenantMetrics.TenantId,
                TotalRequests = tenantMetrics.TotalRequests,
                RequestsAllowed = tenantMetrics.RequestsAllowed,
                RequestsBlocked = tenantMetrics.RequestsBlocked,
                BlockRate = tenantMetrics.BlockRate,
                AverageRemainingRequests = tenantMetrics.AverageRemainingRequests,
                LastActivity = tenantMetrics.LastActivity
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rate limit metrics for tenant {TenantId}", query.TenantId);
            throw;
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

        try
        {
            var metrics = _metricsService.GetAggregatedMetrics();
            
            return new Commands.AggregatedRateLimitMetrics
            {
                TotalTenants = metrics.ActiveTenants,
                TotalRequests = metrics.TotalRequests,
                TotalBlocked = metrics.TotalBlocked,
                OverallBlockRate = metrics.BlockRate,
                RequestsByTenant = new Dictionary<string, long>(),
                BlockRatesByTenant = new Dictionary<string, double>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting aggregated rate limit metrics");
            throw;
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
        try
        {
            var health = await _monitoringService.GetHealthStatusAsync();
            
            return new Commands.RateLimitHealthStatus
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rate limiting health status");
            throw;
        }
    }
}