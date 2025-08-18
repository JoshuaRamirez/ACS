using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using Grpc.Net.Client;

namespace ACS.Infrastructure;

public class TenantProcessDiscoveryService : IDisposable
{
    private readonly ConcurrentDictionary<string, TenantProcessInfo> _tenantProcesses = new();
    private readonly ConcurrentDictionary<int, string> _allocatedPorts = new(); // Port -> TenantId mapping
    private readonly ILogger<TenantProcessDiscoveryService> _logger;
    private readonly object _portAllocationLock = new();

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

        int grpcPort = 0;
        try
        {
            grpcPort = GetAvailablePort();
            
            // Reserve the port
            _allocatedPorts[grpcPort] = tenantId;
            
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
                // Release the port if process failed to start
                _allocatedPorts.TryRemove(grpcPort, out _);
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

            // Wait for process to be ready with timeout
            var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await WaitForProcessHealthy(grpcEndpoint, timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Timeout waiting for tenant process {TenantId} to become healthy", tenantId);
                // Continue anyway - process might still be starting
            }

            _logger.LogInformation("Started process {ProcessId} for tenant {TenantId} on {Endpoint}", 
                process.Id, tenantId, grpcEndpoint);

            return tenantProcess;
        }
        catch
        {
            // Clean up port allocation on any failure
            if (grpcPort > 0)
            {
                _allocatedPorts.TryRemove(grpcPort, out _);
            }
            throw;
        }
    }

    private async Task WaitForProcessHealthy(string grpcEndpoint, CancellationToken cancellationToken)
    {
        const int maxRetries = 15;
        const int delayMs = 2000;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using var channel = GrpcChannel.ForAddress(grpcEndpoint);
                // Could add a health check gRPC call here
                // For now, just verify the channel can be created
                _logger.LogDebug("Process health check attempt {Attempt}/{MaxRetries} successful for {Endpoint}", 
                    i + 1, maxRetries, grpcEndpoint);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Process health check attempt {Attempt}/{MaxRetries} failed for {Endpoint}", 
                    i + 1, maxRetries, grpcEndpoint);
                
                if (i < maxRetries - 1)
                {
                    await Task.Delay(delayMs, cancellationToken);
                }
            }
        }
    }

    private int GetAvailablePort()
    {
        lock (_portAllocationLock)
        {
            const int startPort = 50000;
            const int endPort = 59999;
            const int maxAttempts = 100;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // Try sequential allocation first, then random
                int port = attempt < 50 ? startPort + attempt : new Random().Next(startPort, endPort + 1);

                // Skip if we've already allocated this port
                if (_allocatedPorts.ContainsKey(port))
                {
                    continue;
                }

                // Check if port is actually available on the system
                if (IsPortAvailable(port))
                {
                    _logger.LogDebug("Allocated port {Port} for tenant process", port);
                    return port;
                }
                else
                {
                    _logger.LogDebug("Port {Port} is in use by system, trying next", port);
                }
            }

            throw new InvalidOperationException($"Unable to find available port after {maxAttempts} attempts in range {startPort}-{endPort}");
        }
    }

    private bool IsPortAvailable(int port)
    {
        try
        {
            // Check TCP listeners
            var tcpListeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            if (tcpListeners.Any(listener => listener.Port == port))
            {
                return false;
            }

            // Check TCP connections
            var tcpConnections = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
            if (tcpConnections.Any(conn => conn.LocalEndPoint.Port == port))
            {
                return false;
            }

            // Check UDP listeners
            var udpListeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners();
            if (udpListeners.Any(listener => listener.Port == port))
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking port {Port} availability, assuming unavailable", port);
            return false;
        }
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
                
                // Release the port
                ReleasePortForTenant(tenantId);
                
                _logger.LogInformation("Stopped process {ProcessId} for tenant {TenantId}", 
                    processInfo.ProcessId, tenantId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping process {ProcessId} for tenant {TenantId}", 
                    processInfo.ProcessId, tenantId);
                
                // Still try to release the port even if process stop failed
                ReleasePortForTenant(tenantId);
            }
        }
        return false;
    }

    private void ReleasePortForTenant(string tenantId)
    {
        // Find and remove the port allocated to this tenant
        var portToRemove = _allocatedPorts.FirstOrDefault(kvp => kvp.Value == tenantId);
        if (portToRemove.Key != 0)
        {
            _allocatedPorts.TryRemove(portToRemove.Key, out _);
            _logger.LogDebug("Released port {Port} for tenant {TenantId}", portToRemove.Key, tenantId);
        }
    }

    public Task<List<TenantProcessInfo>> GetAllTenantProcessesAsync()
    {
        var processes = new List<TenantProcessInfo>();
        
        foreach (var (tenantId, processInfo) in _tenantProcesses.ToList())
        {
            // Verify process is still running and update health status
            if (IsProcessRunning(processInfo.ProcessId))
            {
                processInfo.IsHealthy = true;
                processInfo.LastHealthCheck = DateTime.UtcNow;
                processes.Add(processInfo);
            }
            else
            {
                // Process died, clean up
                _logger.LogWarning("Removing dead process {ProcessId} for tenant {TenantId}", 
                    processInfo.ProcessId, tenantId);
                _tenantProcesses.TryRemove(tenantId, out _);
                ReleasePortForTenant(tenantId);
            }
        }
        
        return Task.FromResult(processes);
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
        _allocatedPorts.Clear();
        
        _logger.LogInformation("TenantProcessDiscoveryService disposed");
    }
}
