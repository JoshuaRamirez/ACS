using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;

namespace ACS.Infrastructure.Performance;

/// <summary>
/// Implementation of connection pool monitoring
/// </summary>
public class ConnectionPoolMonitor : IConnectionPoolMonitor
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConnectionPoolMonitor> _logger;
    private readonly object _statsLock = new();
    private ConnectionPoolStatistics _currentStats;
    private DateTime _lastStatsUpdate;

    public ConnectionPoolMonitor(
        IConfiguration configuration,
        ILogger<ConnectionPoolMonitor> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _currentStats = new ConnectionPoolStatistics
        {
            LastReset = DateTime.UtcNow
        };
        _lastStatsUpdate = DateTime.UtcNow;
    }

    public async Task<ConnectionPoolStatistics> GetStatisticsAsync()
    {
        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                return _currentStats;
            }

            await Task.Run(() =>
            {
                lock (_statsLock)
                {
                    // Get SQL Server connection pool statistics
                    using var connection = new SqlConnection(connectionString);
                    
                    // Parse pool settings from connection string
                    var builder = new SqlConnectionStringBuilder(connectionString);
                    _currentStats.MaxPoolSize = builder.MaxPoolSize;
                    _currentStats.MinPoolSize = builder.MinPoolSize;
                    
                    // Get runtime statistics (these would come from performance counters in production)
                    _currentStats.TotalConnections = GetActiveConnectionCount(connectionString);
                    _currentStats.ActiveConnections = GetActiveConnectionCount(connectionString);
                    _currentStats.IdleConnections = _currentStats.TotalConnections - _currentStats.ActiveConnections;
                    
                    _lastStatsUpdate = DateTime.UtcNow;
                }
            });

            return _currentStats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting connection pool statistics");
            return _currentStats;
        }
    }

    public async Task ResetPoolAsync()
    {
        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
                return;

            await Task.Run(() =>
            {
                SqlConnection.ClearPool(new SqlConnection(connectionString));
                
                lock (_statsLock)
                {
                    _currentStats.LastReset = DateTime.UtcNow;
                    _currentStats.TotalRequests = 0;
                    _currentStats.FailedRequests = 0;
                }
            });

            _logger.LogInformation("Connection pool reset successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting connection pool");
            throw;
        }
    }

    public async Task<ConnectionPoolHealth> GetHealthAsync()
    {
        var health = new ConnectionPoolHealth
        {
            CheckTime = DateTime.UtcNow,
            IsHealthy = true
        };

        try
        {
            var stats = await GetStatisticsAsync();
            
            // Calculate utilization
            health.UtilizationPercentage = stats.MaxPoolSize > 0 
                ? (double)stats.ActiveConnections / stats.MaxPoolSize * 100 
                : 0;

            // Check for issues
            if (health.UtilizationPercentage > 90)
            {
                health.Issues.Add($"High connection pool utilization: {health.UtilizationPercentage:F1}%");
                health.IsHealthy = false;
            }

            if (stats.WaitingRequests > 10)
            {
                health.Issues.Add($"High number of waiting requests: {stats.WaitingRequests}");
                health.IsHealthy = false;
            }

            if (stats.FailedRequests > stats.TotalRequests * 0.05 && stats.TotalRequests > 100)
            {
                health.Issues.Add($"High failure rate: {(double)stats.FailedRequests / stats.TotalRequests * 100:F1}%");
                health.IsHealthy = false;
            }

            // Test connection
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrEmpty(connectionString))
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                command.CommandTimeout = 5;
                await command.ExecuteScalarAsync();
            }
        }
        catch (Exception ex)
        {
            health.IsHealthy = false;
            health.Issues.Add($"Connection test failed: {ex.Message}");
            _logger.LogError(ex, "Health check failed");
        }

        return health;
    }

    public async Task ClearIdleConnectionsAsync(TimeSpan idleTime)
    {
        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
                return;

            await Task.Run(() =>
            {
                // SQL Server will handle this automatically based on Connection Lifetime setting
                // This is a placeholder for custom logic if needed
                _logger.LogDebug("Idle connections cleared (handled by SQL Server connection pooling)");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing idle connections");
        }
    }

    private int GetActiveConnectionCount(string connectionString)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT COUNT(*) 
                FROM sys.dm_exec_connections 
                WHERE session_id = @@SPID";
            
            var result = command.ExecuteScalar();
            return Convert.ToInt32(result);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Updates statistics based on connection events
    /// </summary>
    public void RecordConnectionRequest(bool success)
    {
        lock (_statsLock)
        {
            _currentStats.TotalRequests++;
            if (!success)
            {
                _currentStats.FailedRequests++;
            }
        }
    }

    /// <summary>
    /// Records wait time for connection acquisition
    /// </summary>
    public void RecordWaitTime(TimeSpan waitTime)
    {
        lock (_statsLock)
        {
            // Simple moving average calculation
            var currentAvg = _currentStats.AverageWaitTime;
            var totalRequests = Math.Max(1, _currentStats.TotalRequests);
            
            _currentStats.AverageWaitTime = TimeSpan.FromMilliseconds(
                (currentAvg.TotalMilliseconds * (totalRequests - 1) + waitTime.TotalMilliseconds) / totalRequests);
        }
    }
}