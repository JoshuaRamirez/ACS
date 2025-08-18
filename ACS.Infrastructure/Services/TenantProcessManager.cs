using System.Diagnostics;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using ACS.Core.Grpc;

namespace ACS.Infrastructure.Services;

public class TenantProcessManager : IDisposable
{
    private readonly ILogger<TenantProcessManager> _logger;
    private readonly Dictionary<string, TenantProcess> _processes = new();
    private readonly object _lock = new();
    private bool _disposed = false;
    
    // Port management with configurable range
    private const int MIN_PORT = 5001;
    private const int MAX_PORT = 5100;
    private readonly HashSet<int> _usedPorts = new();

    public class TenantProcess
    {
        public string TenantId { get; set; } = null!;
        public Process Process { get; set; } = null!;
        public int Port { get; set; }
        public GrpcChannel? Channel { get; set; }
        public VerticalService.VerticalServiceClient? Client { get; set; }
        public DateTime StartTime { get; set; }
        public bool IsHealthy { get; set; }
    }

    public TenantProcessManager(ILogger<TenantProcessManager> logger)
    {
        _logger = logger;
    }

    public async Task<TenantProcess> StartTenantProcessAsync(string tenantId)
    {
        lock (_lock)
        {
            if (_processes.ContainsKey(tenantId))
            {
                var existing = _processes[tenantId];
                if (existing.IsHealthy)
                {
                    return existing;
                }
                // Process exists but unhealthy, stop it first
                StopTenantProcessInternal(tenantId);
            }
        }

        var port = AllocatePort();
        
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project ACS.VerticalHost -- --tenant {tenantId} --port {port}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        // Set environment variables
        process.StartInfo.Environment["TENANT_ID"] = tenantId;
        process.StartInfo.Environment["GRPC_PORT"] = port.ToString();
        
        // Handle process output
        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                _logger.LogDebug("[{TenantId}] {Output}", tenantId, e.Data);
        };
        
        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                _logger.LogWarning("[{TenantId}] {Error}", tenantId, e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Create gRPC channel
        var channel = GrpcChannel.ForAddress($"http://localhost:{port}");
        var client = new VerticalService.VerticalServiceClient(channel);

        var tenantProcess = new TenantProcess
        {
            TenantId = tenantId,
            Process = process,
            Port = port,
            Channel = channel,
            Client = client,
            StartTime = DateTime.UtcNow,
            IsHealthy = false
        };

        lock (_lock)
        {
            _processes[tenantId] = tenantProcess;
        }

        // Wait for process to be ready
        await WaitForHealthyAsync(tenantProcess);

        _logger.LogInformation("Started tenant process for {TenantId} on port {Port}", 
            tenantId, port);

        return tenantProcess;
    }

    private int AllocatePort()
    {
        lock (_lock)
        {
            // Find first available port in range
            for (int port = MIN_PORT; port <= MAX_PORT; port++)
            {
                if (!_usedPorts.Contains(port))
                {
                    _usedPorts.Add(port);
                    _logger.LogDebug("Allocated port {Port}, {UsedCount} ports in use", 
                        port, _usedPorts.Count);
                    return port;
                }
            }
            
            throw new InvalidOperationException($"No available ports in range {MIN_PORT}-{MAX_PORT}. All {_usedPorts.Count} ports are in use.");
        }
    }
    
    private void ReleasePort(int port)
    {
        lock (_lock)
        {
            if (_usedPorts.Remove(port))
            {
                _logger.LogDebug("Released port {Port}, {UsedCount} ports in use", 
                    port, _usedPorts.Count);
            }
        }
    }

    private async Task WaitForHealthyAsync(TenantProcess process, int maxRetries = 30)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var health = await process.Client!.HealthCheckAsync(new HealthRequest());
                if (health.Healthy)
                {
                    process.IsHealthy = true;
                    _logger.LogInformation("Tenant process {TenantId} is healthy", process.TenantId);
                    return;
                }
            }
            catch (Exception ex)
            {
                // Expected during startup
                _logger.LogDebug("Health check attempt {Attempt} failed for {TenantId}: {Message}", 
                    i + 1, process.TenantId, ex.Message);
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException($"Tenant process {process.TenantId} failed to become healthy after {maxRetries} attempts");
    }

    public async Task StopTenantProcessAsync(string tenantId)
    {
        await Task.Run(() => StopTenantProcessInternal(tenantId));
    }

    private void StopTenantProcessInternal(string tenantId)
    {
        TenantProcess? process = null;
        
        lock (_lock)
        {
            if (!_processes.TryGetValue(tenantId, out process))
                return;

            try
            {
                process.Channel?.Dispose();
                
                if (!process.Process.HasExited)
                {
                    try
                    {
                        process.Process.Kill();
                        process.Process.WaitForExit(5000);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error killing process for tenant {TenantId}", tenantId);
                    }
                }

                process.Process.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping tenant process {TenantId}", tenantId);
            }
            finally
            {
                _processes.Remove(tenantId);
                // Release the port for reuse
                ReleasePort(process.Port);
            }
        }

        _logger.LogInformation("Stopped tenant process for {TenantId}", tenantId);
    }

    public async Task<TenantProcess> GetOrStartProcessAsync(string tenantId)
    {
        TenantProcess? existing = null;
        
        lock (_lock)
        {
            if (_processes.TryGetValue(tenantId, out existing) && existing.IsHealthy)
            {
                // Quick health check
                Task.Run(async () =>
                {
                    try
                    {
                        var health = await existing.Client!.HealthCheckAsync(new HealthRequest());
                        existing.IsHealthy = health.Healthy;
                    }
                    catch
                    {
                        existing.IsHealthy = false;
                    }
                });

                if (existing.IsHealthy)
                {
                    return existing;
                }
            }
        }

        return await StartTenantProcessAsync(tenantId);
    }

    public Task<Dictionary<string, bool>> GetProcessStatusAsync()
    {
        var status = new Dictionary<string, bool>();
        
        lock (_lock)
        {
            foreach (var kvp in _processes)
            {
                status[kvp.Key] = kvp.Value.IsHealthy && !kvp.Value.Process.HasExited;
            }
        }

        return Task.FromResult(status);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Get all tenant IDs to avoid modification during iteration
        List<string> tenantIds;
        lock (_lock)
        {
            tenantIds = _processes.Keys.ToList();
        }

        // Stop all processes in parallel for faster shutdown
        if (tenantIds.Count > 0)
        {
            _logger.LogInformation("Disposing TenantProcessManager, stopping {Count} processes", tenantIds.Count);
            
            Parallel.ForEach(tenantIds, new ParallelOptions { MaxDegreeOfParallelism = 4 }, tenantId =>
            {
                try
                {
                    StopTenantProcessInternal(tenantId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing tenant process {TenantId}", tenantId);
                }
            });
        }

        // Final cleanup
        lock (_lock)
        {
            _processes.Clear();
            _usedPorts.Clear();
        }

        _logger.LogInformation("TenantProcessManager disposed, all processes stopped");
    }
}