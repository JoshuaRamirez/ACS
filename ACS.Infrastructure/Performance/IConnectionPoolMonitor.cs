namespace ACS.Infrastructure.Performance;

/// <summary>
/// Interface for monitoring database connection pool
/// </summary>
public interface IConnectionPoolMonitor
{
    /// <summary>
    /// Gets current pool statistics
    /// </summary>
    Task<ConnectionPoolStatistics> GetStatisticsAsync();
    
    /// <summary>
    /// Resets the connection pool
    /// </summary>
    Task ResetPoolAsync();
    
    /// <summary>
    /// Gets pool health status
    /// </summary>
    Task<ConnectionPoolHealth> GetHealthAsync();
    
    /// <summary>
    /// Clears idle connections
    /// </summary>
    Task ClearIdleConnectionsAsync(TimeSpan idleTime);
}

/// <summary>
/// Connection pool statistics
/// </summary>
public class ConnectionPoolStatistics
{
    public int ActiveConnections { get; set; }
    public int IdleConnections { get; set; }
    public int TotalConnections { get; set; }
    public int MaxPoolSize { get; set; }
    public int MinPoolSize { get; set; }
    public int WaitingRequests { get; set; }
    public TimeSpan AverageWaitTime { get; set; }
    public long TotalRequests { get; set; }
    public long FailedRequests { get; set; }
    public DateTime LastReset { get; set; }
}

/// <summary>
/// Connection pool health status
/// </summary>
public class ConnectionPoolHealth
{
    public bool IsHealthy { get; set; }
    public double UtilizationPercentage { get; set; }
    public List<string> Issues { get; set; } = new();
    public DateTime CheckTime { get; set; }
}