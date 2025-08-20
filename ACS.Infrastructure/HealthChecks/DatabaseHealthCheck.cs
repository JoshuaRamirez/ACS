using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ACS.Infrastructure.HealthChecks;

/// <summary>
/// Health check for database connectivity and performance
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseHealthCheck> _logger;
    private readonly TimeSpan _timeout;
    private readonly TimeSpan _degradedThreshold;

    public DatabaseHealthCheck(
        IConfiguration configuration,
        ILogger<DatabaseHealthCheck> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _timeout = TimeSpan.FromSeconds(5);
        _degradedThreshold = TimeSpan.FromSeconds(2);
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                return HealthCheckResult.Unhealthy("Database connection string not configured");
            }

            using var connection = new SqlConnection(connectionString);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);

            await connection.OpenAsync(cts.Token);

            // Test basic query execution
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandTimeout = (int)_timeout.TotalSeconds;
            
            var result = await command.ExecuteScalarAsync(cts.Token);
            
            // Get additional database metrics
            var metrics = await GetDatabaseMetricsAsync(connection, cts.Token);
            
            stopwatch.Stop();
            
            var data = new Dictionary<string, object>
            {
                ["ResponseTime"] = stopwatch.ElapsedMilliseconds,
                ["Database"] = connection.Database,
                ["DataSource"] = connection.DataSource,
                ["ServerVersion"] = connection.ServerVersion,
                ["ActiveConnections"] = metrics.ActiveConnections,
                ["DatabaseSize"] = metrics.DatabaseSizeMB,
                ["CpuUsage"] = metrics.CpuUsage,
                ["MemoryUsage"] = metrics.MemoryUsageMB
            };

            // Check connection pool health
            var poolStats = GetConnectionPoolStatistics(connectionString);
            data["PoolActiveConnections"] = poolStats.ActiveConnections;
            data["PoolIdleConnections"] = poolStats.IdleConnections;

            if (stopwatch.Elapsed > _degradedThreshold)
            {
                return HealthCheckResult.Degraded(
                    $"Database response time is slow: {stopwatch.ElapsedMilliseconds}ms",
                    null,
                    data);
            }

            return HealthCheckResult.Healthy(
                $"Database is responsive ({stopwatch.ElapsedMilliseconds}ms)",
                data);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Database health check timed out after {Timeout}ms", _timeout.TotalMilliseconds);
            return HealthCheckResult.Unhealthy(
                $"Database health check timed out after {_timeout.TotalSeconds} seconds");
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy(
                $"Database connection failed: {ex.Message}",
                ex,
                new Dictionary<string, object>
                {
                    ["ErrorNumber"] = ex.Number,
                    ["ErrorSeverity"] = ex.Class,
                    ["ErrorState"] = ex.State
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during database health check");
            return HealthCheckResult.Unhealthy(
                $"Database health check failed: {ex.Message}",
                ex);
        }
    }

    private async Task<DatabaseMetrics> GetDatabaseMetricsAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        var metrics = new DatabaseMetrics();

        try
        {
            // Get database size
            using var sizeCommand = connection.CreateCommand();
            sizeCommand.CommandText = @"
                SELECT 
                    SUM(size * 8.0 / 1024) as SizeMB
                FROM sys.master_files
                WHERE database_id = DB_ID()";
            
            var size = await sizeCommand.ExecuteScalarAsync(cancellationToken);
            if (size != DBNull.Value)
            {
                metrics.DatabaseSizeMB = Convert.ToDouble(size);
            }

            // Get active connections
            using var connCommand = connection.CreateCommand();
            connCommand.CommandText = @"
                SELECT COUNT(*) 
                FROM sys.dm_exec_connections 
                WHERE database_id = DB_ID()";
            
            var connections = await connCommand.ExecuteScalarAsync(cancellationToken);
            if (connections != DBNull.Value)
            {
                metrics.ActiveConnections = Convert.ToInt32(connections);
            }

            // Get CPU and memory usage (requires appropriate permissions)
            using var perfCommand = connection.CreateCommand();
            perfCommand.CommandText = @"
                SELECT TOP 1
                    cpu_percent,
                    physical_memory_kb / 1024.0 as memory_mb
                FROM sys.dm_os_ring_buffers
                CROSS APPLY sys.dm_os_sys_memory
                WHERE ring_buffer_type = N'RING_BUFFER_RESOURCE_MONITOR'
                ORDER BY record_id DESC";
            
            using var reader = await perfCommand.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0))
                    metrics.CpuUsage = reader.GetDouble(0);
                if (!reader.IsDBNull(1))
                    metrics.MemoryUsageMB = reader.GetDouble(1);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get database metrics");
        }

        return metrics;
    }

    private ConnectionPoolStats GetConnectionPoolStatistics(string connectionString)
    {
        // This is a simplified version - in production you'd use performance counters
        var stats = new ConnectionPoolStats();
        
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            stats.MaxPoolSize = builder.MaxPoolSize;
            stats.MinPoolSize = builder.MinPoolSize;
            
            // These would come from actual monitoring in production
            stats.ActiveConnections = 0;
            stats.IdleConnections = 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get connection pool statistics");
        }

        return stats;
    }

    private class DatabaseMetrics
    {
        public double DatabaseSizeMB { get; set; }
        public int ActiveConnections { get; set; }
        public double CpuUsage { get; set; }
        public double MemoryUsageMB { get; set; }
    }

    private class ConnectionPoolStats
    {
        public int MaxPoolSize { get; set; }
        public int MinPoolSize { get; set; }
        public int ActiveConnections { get; set; }
        public int IdleConnections { get; set; }
    }
}