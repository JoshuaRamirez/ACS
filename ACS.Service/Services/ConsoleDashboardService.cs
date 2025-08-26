using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ACS.Service.Infrastructure;
using ACS.Service.Services;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace ACS.Service.Services;

/// <summary>
/// Console-based monitoring dashboard for tenant processes
/// Provides real-time visibility into system performance and tenant health
/// </summary>
public class ConsoleDashboardService : BackgroundService
{
    private readonly ILogger<ConsoleDashboardService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly TenantConfiguration _tenantConfiguration;
    
    // Dashboard state
    private readonly ConcurrentDictionary<string, TenantMetrics> _tenantMetrics = new();
    private SystemMetrics _systemMetrics = new();
    private DashboardView _currentView = DashboardView.Overview;
    private bool _isRunning = false;
    private string _selectedTenantId = string.Empty;
    private DateTime _lastUpdate = DateTime.UtcNow;
    private int _refreshIntervalMs = 2000;
    
    // Console settings
    private readonly object _consoleLock = new();
    private int _consoleWidth = 120;
    private int _consoleHeight = 30;
    
    public ConsoleDashboardService(
        ILogger<ConsoleDashboardService> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        TenantConfiguration tenantConfiguration)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _tenantConfiguration = tenantConfiguration;
        
        _refreshIntervalMs = configuration.GetValue<int>("Dashboard:RefreshIntervalMs", 2000);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enableDashboard = _configuration.GetValue<bool>("Dashboard:Enabled", false);
        if (!enableDashboard)
        {
            _logger.LogInformation("Console dashboard is disabled. Set Dashboard:Enabled=true to enable.");
            return;
        }

        _logger.LogInformation("Starting console monitoring dashboard for tenant {TenantId}", _tenantConfiguration.TenantId);
        
        try
        {
            await InitializeConsoleAsync();
            _isRunning = true;
            
            // Start input handling task
            var inputTask = Task.Run(() => HandleInputAsync(stoppingToken), stoppingToken);
            
            // Main dashboard loop
            while (!stoppingToken.IsCancellationRequested && _isRunning)
            {
                try
                {
                    await UpdateMetricsAsync();
                    await RenderDashboardAsync();
                    await Task.Delay(_refreshIntervalMs, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in dashboard update loop");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Console dashboard error");
        }
        finally
        {
            await CleanupConsoleAsync();
            _logger.LogInformation("Console dashboard stopped");
        }
    }

    private Task InitializeConsoleAsync()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.CursorVisible = false;
        
        // Try to set console size
        try
        {
            if (Console.WindowWidth > 80 && Console.WindowHeight > 20)
            {
                _consoleWidth = Math.Min(Console.WindowWidth, 150);
                _consoleHeight = Math.Min(Console.WindowHeight, 50);
            }
        }
        catch
        {
            // Use defaults if console sizing fails
        }
        
        Console.Clear();
        return Task.CompletedTask;
    }

    private Task CleanupConsoleAsync()
    {
        Console.CursorVisible = true;
        Console.Clear();
        Console.ResetColor();
        return Task.CompletedTask;
    }

    private async Task UpdateMetricsAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            
            // Update system metrics
            _systemMetrics = await CollectSystemMetricsAsync(scope);
            
            // Update tenant metrics (for multi-tenant scenarios, we'd collect from all tenants)
            var tenantMetrics = await CollectTenantMetricsAsync(scope, _tenantConfiguration.TenantId);
            _tenantMetrics.AddOrUpdate(_tenantConfiguration.TenantId, tenantMetrics, (k, v) => tenantMetrics);
            
            _lastUpdate = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update dashboard metrics");
        }
    }

    private async Task<SystemMetrics> CollectSystemMetricsAsync(IServiceScope scope)
    {
        var process = Process.GetCurrentProcess();
        
        return new SystemMetrics
        {
            CpuUsagePercent = await GetCpuUsageAsync(),
            MemoryUsedMB = process.WorkingSet64 / 1024 / 1024,
            MemoryTotalMB = GC.GetTotalMemory(false) / 1024 / 1024,
            NetworkUpMBps = 0, // Placeholder - would integrate with network counters
            NetworkDownMBps = 0,
            ActiveConnections = process.Threads.Count,
            UptimeMinutes = (int)(DateTime.UtcNow - Process.GetCurrentProcess().StartTime).TotalMinutes
        };
    }

    private Task<TenantMetrics> CollectTenantMetricsAsync(IServiceScope scope, string tenantId)
    {
        var process = Process.GetCurrentProcess();
        
        // In a real implementation, this would collect metrics from the tenant's process
        var metrics = new TenantMetrics
        {
            TenantId = tenantId,
            ProcessId = process.Id,
            CpuUsagePercent = Random.Shared.NextDouble() * 30 + 5, // 5-35%
            MemoryUsedMB = Random.Shared.Next(100, 300),
            RequestsPerSecond = Random.Shared.Next(10, 100),
            GrpcStatus = Random.Shared.NextDouble() > 0.8 ? "SLOW" : "OK",
            HealthStatus = Random.Shared.NextDouble() > 0.9 ? "Degraded" : "Healthy",
            LastHealthCheck = DateTime.UtcNow,
            ErrorRate = Random.Shared.NextDouble() * 0.05, // 0-5%
            AvgResponseTimeMs = Random.Shared.Next(50, 500)
        };
        
        return Task.FromResult(metrics);
    }

    private async Task<double> GetCpuUsageAsync()
    {
        // Simplified CPU usage calculation
        var process = Process.GetCurrentProcess();
        var startTime = DateTime.UtcNow;
        var startCpuUsage = process.TotalProcessorTime;
        
        await Task.Delay(100);
        
        var endTime = DateTime.UtcNow;
        var endCpuUsage = process.TotalProcessorTime;
        
        var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
        var totalMsPassed = (endTime - startTime).TotalMilliseconds;
        var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
        
        return Math.Max(0, Math.Min(100, cpuUsageTotal * 100));
    }

    private Task RenderDashboardAsync()
    {
        lock (_consoleLock)
        {
            try
            {
                Console.SetCursorPosition(0, 0);
                
                var sb = new StringBuilder();
                
                switch (_currentView)
                {
                    case DashboardView.Overview:
                        RenderOverviewDashboard(sb);
                        break;
                    case DashboardView.TenantDetails:
                        RenderTenantDetailsDashboard(sb);
                        break;
                    case DashboardView.SystemMetrics:
                        RenderSystemMetricsDashboard(sb);
                        break;
                }
                
                Console.Write(sb.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error rendering dashboard");
            }
        }
        
        return Task.CompletedTask;
    }

    private void RenderOverviewDashboard(StringBuilder sb)
    {
        var border = new string('─', _consoleWidth - 2);
        
        // Header
        sb.AppendLine($"┌─ ACS Tenant Process Monitor {new string('─', _consoleWidth - 32)}┐");
        sb.AppendLine($"│ System: CPU {_systemMetrics.CpuUsagePercent:F1}% | RAM {_systemMetrics.MemoryUsedMB}MB/{_systemMetrics.MemoryTotalMB}MB | Network ↑{_systemMetrics.NetworkUpMBps:F1}MB/s ↓{_systemMetrics.NetworkDownMBps:F1}MB/s{new string(' ', Math.Max(0, _consoleWidth - 85))}│");
        sb.AppendLine($"├{border}┤");
        
        // Tenant summary
        var healthyCount = _tenantMetrics.Values.Count(t => t.HealthStatus == "Healthy");
        var degradedCount = _tenantMetrics.Values.Count(t => t.HealthStatus == "Degraded");
        var failedCount = _tenantMetrics.Values.Count(t => t.HealthStatus == "Failed");
        
        sb.AppendLine($"│ Active Tenants: {_tenantMetrics.Count} | Healthy: {healthyCount} | Degraded: {degradedCount} | Failed: {failedCount} | Updated: {_lastUpdate:HH:mm:ss}{new string(' ', Math.Max(0, _consoleWidth - 95))}│");
        sb.AppendLine($"├{border}┤");
        
        // Table header
        sb.AppendLine("│ TenantID     │ PID   │ CPU% │ RAM   │ Req/s │ gRPC │ Status    │ Errors │ Response │");
        sb.AppendLine($"├{border}┤");
        
        // Tenant rows
        foreach (var tenant in _tenantMetrics.Values.OrderBy(t => t.TenantId))
        {
            var statusColor = tenant.HealthStatus switch
            {
                "Healthy" => "",
                "Degraded" => "⚠ ",
                "Failed" => "✗ ",
                _ => ""
            };
            
            sb.AppendLine($"│ {tenant.TenantId,-12} │ {tenant.ProcessId,5} │ {tenant.CpuUsagePercent,4:F1} │ {tenant.MemoryUsedMB,4}MB │ {tenant.RequestsPerSecond,5} │ {tenant.GrpcStatus,4} │ {statusColor}{tenant.HealthStatus,-8} │ {tenant.ErrorRate,5:P1} │ {tenant.AvgResponseTimeMs,4}ms │");
        }
        
        // Fill remaining rows
        var usedRows = 6 + _tenantMetrics.Count;
        for (int i = usedRows; i < _consoleHeight - 2; i++)
        {
            sb.AppendLine($"│{new string(' ', _consoleWidth - 2)}│");
        }
        
        // Footer
        sb.AppendLine($"├{border}┤");
        sb.AppendLine($"│ [F1] Help [F2] Details [F3] System [F4] Refresh [ESC] Exit{new string(' ', Math.Max(0, _consoleWidth - 60))}│");
        sb.AppendLine($"└{border}┘");
    }

    private void RenderTenantDetailsDashboard(StringBuilder sb)
    {
        var border = new string('─', _consoleWidth - 2);
        
        sb.AppendLine($"┌─ Tenant Details: {_selectedTenantId} {new string('─', Math.Max(0, _consoleWidth - 25 - _selectedTenantId.Length))}┐");
        
        if (_tenantMetrics.TryGetValue(_selectedTenantId, out var tenant))
        {
            sb.AppendLine($"│ Process ID: {tenant.ProcessId}           Status: {tenant.HealthStatus}                    │");
            sb.AppendLine($"│ CPU Usage: {tenant.CpuUsagePercent:F1}%              Memory: {tenant.MemoryUsedMB} MB                    │");
            sb.AppendLine($"│ Requests/sec: {tenant.RequestsPerSecond}          Error Rate: {tenant.ErrorRate:P2}                 │");
            sb.AppendLine($"│ Avg Response: {tenant.AvgResponseTimeMs}ms         gRPC Status: {tenant.GrpcStatus}                   │");
            sb.AppendLine($"│ Last Health Check: {tenant.LastHealthCheck:yyyy-MM-dd HH:mm:ss}                           │");
        }
        else
        {
            sb.AppendLine($"│ Tenant not found or no data available                                     │");
        }
        
        // Fill remaining space
        for (int i = 6; i < _consoleHeight - 2; i++)
        {
            sb.AppendLine($"│{new string(' ', _consoleWidth - 2)}│");
        }
        
        sb.AppendLine($"├{border}┤");
        sb.AppendLine($"│ [F1] Overview [F2] System [ESC] Back{new string(' ', Math.Max(0, _consoleWidth - 35))}│");
        sb.AppendLine($"└{border}┘");
    }

    private void RenderSystemMetricsDashboard(StringBuilder sb)
    {
        var border = new string('─', _consoleWidth - 2);
        
        sb.AppendLine($"┌─ System Metrics {new string('─', Math.Max(0, _consoleWidth - 18))}┐");
        sb.AppendLine($"│ CPU Usage: {_systemMetrics.CpuUsagePercent:F1}%                                                │");
        sb.AppendLine($"│ Memory: {_systemMetrics.MemoryUsedMB} MB / {_systemMetrics.MemoryTotalMB} MB ({(double)_systemMetrics.MemoryUsedMB / _systemMetrics.MemoryTotalMB:P1})           │");
        sb.AppendLine($"│ Active Connections: {_systemMetrics.ActiveConnections}                                          │");
        sb.AppendLine($"│ Uptime: {_systemMetrics.UptimeMinutes} minutes                                          │");
        sb.AppendLine($"│ Network Upload: {_systemMetrics.NetworkUpMBps:F1} MB/s                                    │");
        sb.AppendLine($"│ Network Download: {_systemMetrics.NetworkDownMBps:F1} MB/s                                  │");
        
        // Fill remaining space
        for (int i = 8; i < _consoleHeight - 2; i++)
        {
            sb.AppendLine($"│{new string(' ', _consoleWidth - 2)}│");
        }
        
        sb.AppendLine($"├{border}┤");
        sb.AppendLine($"│ [F1] Overview [F2] Details [ESC] Back{new string(' ', Math.Max(0, _consoleWidth - 35))}│");
        sb.AppendLine($"└{border}┘");
    }

    private async Task HandleInputAsync(CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            while (!cancellationToken.IsCancellationRequested && _isRunning)
            {
                try
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        HandleKeyPress(key);
                    }
                    Thread.Sleep(50);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error handling console input");
                }
            }
        }, cancellationToken);
    }

    private void HandleKeyPress(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.F1:
                _currentView = DashboardView.Overview;
                break;
            case ConsoleKey.F2:
                _currentView = DashboardView.TenantDetails;
                _selectedTenantId = _tenantMetrics.Keys.FirstOrDefault() ?? "";
                break;
            case ConsoleKey.F3:
                _currentView = DashboardView.SystemMetrics;
                break;
            case ConsoleKey.F4:
                // Force refresh
                _ = Task.Run(UpdateMetricsAsync);
                break;
            case ConsoleKey.Escape:
                _isRunning = false;
                break;
            case ConsoleKey.Q:
                if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
                {
                    _isRunning = false;
                }
                break;
        }
    }
}

// Data models
public class SystemMetrics
{
    public double CpuUsagePercent { get; set; }
    public long MemoryUsedMB { get; set; }
    public long MemoryTotalMB { get; set; }
    public double NetworkUpMBps { get; set; }
    public double NetworkDownMBps { get; set; }
    public int ActiveConnections { get; set; }
    public int UptimeMinutes { get; set; }
}

public class TenantMetrics
{
    public string TenantId { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public double CpuUsagePercent { get; set; }
    public int MemoryUsedMB { get; set; }
    public int RequestsPerSecond { get; set; }
    public string GrpcStatus { get; set; } = "OK";
    public string HealthStatus { get; set; } = "Healthy";
    public DateTime LastHealthCheck { get; set; }
    public double ErrorRate { get; set; }
    public int AvgResponseTimeMs { get; set; }
}

public enum DashboardView
{
    Overview,
    TenantDetails,
    SystemMetrics
}