using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using Grpc.Net.Client;

namespace ACS.Infrastructure;

public class TenantProcessDiscoveryService : IDisposable
{
    private readonly ConcurrentDictionary<string, TenantProcessInfo> _tenantProcesses = new();
    private readonly ILogger<TenantProcessDiscoveryService> _logger;

    public TenantProcessDiscoveryService(ILogger<TenantProcessDiscoveryService> logger)
    {
        _logger = logger;
    }

    public class TenantProcessInfo
    {
        public string TenantId { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public string GrpcEndpoint { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public bool IsHealthy { get; set; } = true;
        public DateTime LastHealthCheck { get; set; }
    }

    public async Task<TenantProcessInfo> GetOrStartTenantProcessAsync(string tenantId)
    {
        if (_tenantProcesses.TryGetValue(tenantId, out var existingProcess))
        {
            // Verify process is still running
            if (IsProcessRunning(existingProcess.ProcessId))
            {
                return existingProcess;
            }
            else
            {
                _logger.LogWarning("Tenant process {ProcessId} for tenant {TenantId} is no longer running", 
                    existingProcess.ProcessId, tenantId);
                _tenantProcesses.TryRemove(tenantId, out _);
            }
        }

        // Start new tenant process
        return await StartTenantProcessAsync(tenantId);
    }

    private async Task<TenantProcessInfo> StartTenantProcessAsync(string tenantId)
    {
        _logger.LogInformation("Starting new process for tenant {TenantId}", tenantId);

        var grpcPort = GetAvailablePort();
        var grpcEndpoint = $"http://localhost:{grpcPort}";

        var processInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project ACS.VerticalHost {tenantId} --grpc-port {grpcPort}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = Process.Start(processInfo);
        if (process == null)
        {
            throw new InvalidOperationException($"Failed to start process for tenant {tenantId}");
        }

        var tenantProcess = new TenantProcessInfo
        {
            TenantId = tenantId,
            ProcessId = process.Id,
            GrpcEndpoint = grpcEndpoint,
            StartTime = DateTime.UtcNow,
            IsHealthy = true,
            LastHealthCheck = DateTime.UtcNow
        };

        _tenantProcesses[tenantId] = tenantProcess;

        // Wait for process to be ready (simplified - should use health checks)
        await Task.Delay(2000);

        _logger.LogInformation("Started process {ProcessId} for tenant {TenantId} on {Endpoint}", 
            process.Id, tenantId, grpcEndpoint);

        return tenantProcess;
    }

    private int GetAvailablePort()
    {
        // Simple port assignment - in production, use more sophisticated port management
        var random = new Random();
        return random.Next(50000, 60000);
    }

    private bool IsProcessRunning(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public async Task<bool> StopTenantProcessAsync(string tenantId)
    {
        if (_tenantProcesses.TryRemove(tenantId, out var processInfo))
        {
            try
            {
                var process = Process.GetProcessById(processInfo.ProcessId);
                process.Kill();
                await process.WaitForExitAsync();
                
                _logger.LogInformation("Stopped process {ProcessId} for tenant {TenantId}", 
                    processInfo.ProcessId, tenantId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping process {ProcessId} for tenant {TenantId}", 
                    processInfo.ProcessId, tenantId);
            }
        }
        return false;
    }

    public void Dispose()
    {
        foreach (var tenantProcess in _tenantProcesses.Values)
        {
            try
            {
                var process = Process.GetProcessById(tenantProcess.ProcessId);
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing tenant process {ProcessId}", tenantProcess.ProcessId);
            }
        }
        _tenantProcesses.Clear();
    }
}
