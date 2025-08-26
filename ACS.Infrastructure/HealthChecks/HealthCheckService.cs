using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ACS.Infrastructure.HealthChecks;

/// <summary>
/// Comprehensive health check service implementation
/// </summary>
public class HealthCheckService : IHealthCheckService, IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HealthCheckService> _logger;
    private readonly HealthCheckServiceOptions _options;
    private readonly ConcurrentDictionary<string, IHealthCheck> _healthChecks;
    private readonly ConcurrentQueue<HealthCheckHistory> _history;
    private readonly SemaphoreSlim _checkSemaphore;
    private Timer? _periodicCheckTimer;
    private readonly Stopwatch _uptime;

    public HealthCheckService(
        IServiceProvider serviceProvider,
        ILogger<HealthCheckService> logger,
        HealthCheckServiceOptions? options = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options ?? new HealthCheckServiceOptions();
        _healthChecks = new ConcurrentDictionary<string, IHealthCheck>();
        _history = new ConcurrentQueue<HealthCheckHistory>();
        _checkSemaphore = new SemaphoreSlim(1, 1);
        _uptime = Stopwatch.StartNew();
        
        RegisterDefaultHealthChecks();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Health check service starting");
        
        // Start periodic health checks
        if (_options.EnablePeriodicChecks)
        {
            _periodicCheckTimer = new Timer(
                async _ => await PerformPeriodicHealthCheck(),
                null,
                _options.InitialDelay,
                _options.CheckInterval);
        }
        
        // Perform initial health check
        await CheckHealthAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Health check service stopping");
        _periodicCheckTimer?.Dispose();
        return Task.CompletedTask;
    }

    public async Task<HealthReport> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        await _checkSemaphore.WaitAsync(cancellationToken);
        
        try
        {
            var entries = new Dictionary<string, HealthReportEntry>();
            var tasks = new List<Task<(string Name, HealthCheckResult Result, TimeSpan Duration)>>();

            foreach (var check in _healthChecks)
            {
                tasks.Add(ExecuteHealthCheckAsync(check.Key, check.Value, cancellationToken));
            }

            var results = await Task.WhenAll(tasks);
            
            foreach (var (name, result, duration) in results)
            {
                entries[name] = new HealthReportEntry(
                    result.Status,
                    result.Description,
                    duration,
                    result.Exception,
                    result.Data);

                // Record history
                RecordHistory(name, result, duration);
            }

            var totalDuration = TimeSpan.FromMilliseconds(results.Sum(r => r.Duration.TotalMilliseconds));
            var overallStatus = CalculateOverallStatus(entries.Values);

            return new HealthReport(entries, overallStatus, totalDuration);
        }
        finally
        {
            _checkSemaphore.Release();
        }
    }

    public async Task<HealthCheckResult> CheckComponentAsync(string componentName, CancellationToken cancellationToken = default)
    {
        if (!_healthChecks.TryGetValue(componentName, out var healthCheck))
        {
            return new HealthCheckResult(
                HealthStatus.Unhealthy,
                $"Health check for component '{componentName}' not found");
        }

        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(componentName, healthCheck, null, null)
        };

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await healthCheck.CheckHealthAsync(context, cancellationToken);
            stopwatch.Stop();
            
            RecordHistory(componentName, result, stopwatch.Elapsed);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed for component {Component}", componentName);
            return new HealthCheckResult(HealthStatus.Unhealthy, $"Health check failed: {ex.Message}", ex);
        }
    }

    public Task<IEnumerable<HealthCheckHistory>> GetHistoryAsync(string? componentName = null, TimeSpan? period = null)
    {
        var cutoff = period.HasValue ? DateTime.UtcNow.Subtract(period.Value) : DateTime.MinValue;
        
        var history = _history
            .Where(h => h.Timestamp >= cutoff)
            .Where(h => componentName == null || h.ComponentName == componentName)
            .OrderByDescending(h => h.Timestamp)
            .Take(_options.MaxHistoryEntries);

        return Task.FromResult(history);
    }

    public void RegisterHealthCheck(string name, IHealthCheck healthCheck, HealthStatus? failureStatus = null, IEnumerable<string>? tags = null)
    {
        _healthChecks[name] = healthCheck;
        _logger.LogInformation("Registered health check: {Name}", name);
    }

    public async Task<SystemMetrics> GetSystemMetricsAsync()
    {
        return await Task.Run(() =>
        {
            using var process = Process.GetCurrentProcess();
            
            var metrics = new SystemMetrics
            {
                CpuUsagePercentage = GetCpuUsage(process),
                MemoryUsedBytes = process.WorkingSet64,
                MemoryAvailableBytes = GetAvailableMemory(),
                ActiveConnections = GetActiveConnections(),
                ThreadCount = process.Threads.Count,
                Uptime = _uptime.Elapsed,
                Timestamp = DateTime.UtcNow
            };

            metrics.MemoryUsagePercentage = metrics.MemoryAvailableBytes > 0
                ? (double)metrics.MemoryUsedBytes / (metrics.MemoryUsedBytes + metrics.MemoryAvailableBytes) * 100
                : 0;

            // Get disk metrics
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady);
            if (drives.Any())
            {
                metrics.DiskUsedBytes = drives.Sum(d => d.TotalSize - d.TotalFreeSpace);
                metrics.DiskAvailableBytes = drives.Sum(d => d.AvailableFreeSpace);
                metrics.DiskUsagePercentage = metrics.DiskAvailableBytes > 0
                    ? (double)metrics.DiskUsedBytes / (metrics.DiskUsedBytes + metrics.DiskAvailableBytes) * 100
                    : 0;
            }

            return metrics;
        });
    }

    private void RegisterDefaultHealthChecks()
    {
        using var scope = _serviceProvider.CreateScope();
        var services = scope.ServiceProvider;

        // Register database health check
        RegisterHealthCheck("database", services.GetRequiredService<DatabaseHealthCheck>());

        // Register Redis health check (if configured)
        var redisConnectionString = services.GetService<IConfiguration>()?.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            RegisterHealthCheck("redis", services.GetRequiredService<RedisHealthCheck>());
        }

        // Register disk space health check
        RegisterHealthCheck("disk_space", services.GetRequiredService<DiskSpaceHealthCheck>());

        // Register memory health check
        RegisterHealthCheck("memory", services.GetRequiredService<MemoryHealthCheck>());

        // Register gRPC health checks for configured services
        var grpcServices = _options.GrpcServices ?? new[] { "AccessControl", "TenantService" };
        foreach (var service in grpcServices)
        {
            RegisterHealthCheck($"grpc_{service.ToLower()}", 
                new GrpcHealthCheck(
                    services.GetRequiredService<IConfiguration>(),
                    services.GetRequiredService<ILogger<GrpcHealthCheck>>(),
                    service));
        }

        _logger.LogInformation("Registered {Count} default health checks", _healthChecks.Count);
    }

    private async Task<(string Name, HealthCheckResult Result, TimeSpan Duration)> ExecuteHealthCheckAsync(
        string name,
        IHealthCheck healthCheck,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var context = new HealthCheckContext
            {
                Registration = new HealthCheckRegistration(name, healthCheck, null, null)
            };

            var result = await healthCheck.CheckHealthAsync(context, cancellationToken);
            stopwatch.Stop();
            
            return (name, result, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check {Name} threw an exception", name);
            stopwatch.Stop();
            
            return (name, 
                new HealthCheckResult(HealthStatus.Unhealthy, $"Health check failed: {ex.Message}", ex),
                stopwatch.Elapsed);
        }
    }

    private void RecordHistory(string componentName, HealthCheckResult result, TimeSpan duration)
    {
        var entry = new HealthCheckHistory
        {
            ComponentName = componentName,
            Status = result.Status,
            Description = result.Description,
            Duration = duration,
            Timestamp = DateTime.UtcNow,
            Data = result.Data?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, object>()
        };

        _history.Enqueue(entry);

        // Trim history if needed
        while (_history.Count > _options.MaxHistoryEntries)
        {
            _history.TryDequeue(out _);
        }
    }

    private async Task PerformPeriodicHealthCheck()
    {
        try
        {
            _logger.LogDebug("Performing periodic health check");
            var report = await CheckHealthAsync();
            
            if (report.Status != HealthStatus.Healthy)
            {
                _logger.LogWarning("Health check status: {Status}. Unhealthy components: {Components}",
                    report.Status,
                    string.Join(", ", report.Entries.Where(e => e.Value.Status != HealthStatus.Healthy).Select(e => e.Key)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during periodic health check");
        }
    }

    private HealthStatus CalculateOverallStatus(IEnumerable<HealthReportEntry> entries)
    {
        var statuses = entries.Select(e => e.Status).ToList();
        
        if (statuses.Any(s => s == HealthStatus.Unhealthy))
            return HealthStatus.Unhealthy;
        
        if (statuses.Any(s => s == HealthStatus.Degraded))
            return HealthStatus.Degraded;
        
        return HealthStatus.Healthy;
    }

    private double GetCpuUsage(Process process)
    {
        // This is a simplified calculation - in production you'd use performance counters
        try
        {
            var startTime = DateTime.UtcNow;
            var startCpuUsage = process.TotalProcessorTime;
            
            Thread.Sleep(100);
            
            var endTime = DateTime.UtcNow;
            var endCpuUsage = process.TotalProcessorTime;
            
            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
            
            return Math.Round(cpuUsageTotal * 100, 2);
        }
        catch
        {
            return 0;
        }
    }

    private long GetAvailableMemory()
    {
        try
        {
            // GC.GetMemoryInfo() is not available in this .NET version
            // Return total memory as approximation
            return GC.GetTotalMemory(false);
        }
        catch
        {
            return 0;
        }
    }

    private int GetActiveConnections()
    {
        // This would need actual implementation based on your connection tracking
        return 0;
    }
}

/// <summary>
/// Health check service options
/// </summary>
public class HealthCheckServiceOptions
{
    public bool EnablePeriodicChecks { get; set; } = true;
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(10);
    public int MaxHistoryEntries { get; set; } = 100;
    public string[] GrpcServices { get; set; } = { "AccessControl", "TenantService" };
}