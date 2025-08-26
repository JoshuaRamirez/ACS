using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Diagnostics;

namespace ACS.Infrastructure.HealthChecks;

/// <summary>
/// Health check for Redis connectivity and performance
/// </summary>
public class RedisHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RedisHealthCheck> _logger;
    private readonly TimeSpan _timeout;

    public RedisHealthCheck(
        IConfiguration configuration,
        ILogger<RedisHealthCheck> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _timeout = TimeSpan.FromSeconds(3);
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var connectionString = _configuration.GetConnectionString("Redis");
            if (string.IsNullOrEmpty(connectionString))
            {
                return HealthCheckResult.Healthy("Redis not configured (optional dependency)");
            }

            var options = ConfigurationOptions.Parse(connectionString);
            options.ConnectTimeout = (int)_timeout.TotalMilliseconds;
            options.AsyncTimeout = (int)_timeout.TotalMilliseconds;
            options.SyncTimeout = (int)_timeout.TotalMilliseconds;
            options.AbortOnConnectFail = false;

            using var connection = await ConnectionMultiplexer.ConnectAsync(options);
            
            if (!connection.IsConnected)
            {
                return HealthCheckResult.Unhealthy("Redis connection failed");
            }

            var database = connection.GetDatabase();
            
            // Test basic operations
            var testKey = $"health_check_{Guid.NewGuid():N}";
            var testValue = DateTime.UtcNow.ToString("O");
            
            await database.StringSetAsync(testKey, testValue, TimeSpan.FromSeconds(10));
            var retrievedValue = await database.StringGetAsync(testKey);
            await database.KeyDeleteAsync(testKey);

            if (retrievedValue != testValue)
            {
                return HealthCheckResult.Unhealthy("Redis read/write test failed");
            }

            // Get server metrics
            var endpoints = connection.GetEndPoints();
            var server = connection.GetServer(endpoints[0]);
            var info = await server.InfoAsync();
            
            stopwatch.Stop();

            var data = new Dictionary<string, object>
            {
                ["ResponseTime"] = stopwatch.ElapsedMilliseconds,
                ["IsConnected"] = connection.IsConnected,
                ["EndPoint"] = endpoints[0].ToString() ?? "unknown",
                ["ClientName"] = connection.ClientName ?? "unknown",
                ["OperationCount"] = connection.OperationCount
            };

            // Parse Redis INFO response
            if (info != null)
            {
                foreach (var group in info)
                {
                    if (group.Key == "Memory")
                    {
                        var memoryInfo = ParseRedisInfo(new KeyValuePair<string, KeyValuePair<string, string>[]>(group.Key, group.ToArray()));
                        if (memoryInfo.TryGetValue("used_memory_human", out var usedMemory))
                            data["UsedMemory"] = usedMemory;
                        if (memoryInfo.TryGetValue("used_memory_peak_human", out var peakMemory))
                            data["PeakMemory"] = peakMemory;
                    }
                    else if (group.Key == "Stats")
                    {
                        var statsInfo = ParseRedisInfo(new KeyValuePair<string, KeyValuePair<string, string>[]>(group.Key, group.ToArray()));
                        if (statsInfo.TryGetValue("total_connections_received", out var connections))
                            data["TotalConnections"] = connections;
                        if (statsInfo.TryGetValue("total_commands_processed", out var commands))
                            data["TotalCommands"] = commands;
                    }
                    else if (group.Key == "Clients")
                    {
                        var clientInfo = ParseRedisInfo(new KeyValuePair<string, KeyValuePair<string, string>[]>(group.Key, group.ToArray()));
                        if (clientInfo.TryGetValue("connected_clients", out var clients))
                            data["ConnectedClients"] = clients;
                    }
                }
            }

            if (stopwatch.Elapsed > TimeSpan.FromSeconds(1))
            {
                return HealthCheckResult.Degraded(
                    $"Redis response time is slow: {stopwatch.ElapsedMilliseconds}ms",
                    null,
                    data);
            }

            return HealthCheckResult.Healthy(
                $"Redis is responsive ({stopwatch.ElapsedMilliseconds}ms)",
                data);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Redis connection failed");
            return HealthCheckResult.Unhealthy($"Redis connection failed: {ex.Message}", ex);
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogWarning(ex, "Redis operation timed out");
            return HealthCheckResult.Unhealthy($"Redis operation timed out: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Redis health check");
            return HealthCheckResult.Unhealthy($"Redis health check failed: {ex.Message}", ex);
        }
    }

    private Dictionary<string, string> ParseRedisInfo(KeyValuePair<string, KeyValuePair<string, string>[]> group)
    {
        var result = new Dictionary<string, string>();
        
        foreach (var item in group.Value)
        {
            result[item.Key] = item.Value;
        }
        
        return result;
    }
}