using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ACS.Infrastructure.HealthChecks;

/// <summary>
/// Health check for memory usage and garbage collection
/// </summary>
public class MemoryHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MemoryHealthCheck> _logger;
    private readonly long _maxMemoryMB;
    private readonly double _warningThresholdPercentage;

    public MemoryHealthCheck(
        IConfiguration configuration,
        ILogger<MemoryHealthCheck> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _maxMemoryMB = configuration.GetValue<long>("HealthChecks:Memory:MaxMemoryMB", 2048); // 2GB default
        _warningThresholdPercentage = configuration.GetValue<double>("HealthChecks:Memory:WarningThresholdPercent", 80.0);
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get current process memory
            using var process = Process.GetCurrentProcess();
            var workingSetMB = process.WorkingSet64 / (1024 * 1024);
            var privateMemoryMB = process.PrivateMemorySize64 / (1024 * 1024);
            var virtualMemoryMB = process.VirtualMemorySize64 / (1024 * 1024);
            var pagedMemoryMB = process.PagedMemorySize64 / (1024 * 1024);
            
            // Get GC memory info
            var gcInfo = GC.GetMemoryInfo();
            var totalMemoryMB = gcInfo.TotalAvailableMemoryBytes / (1024 * 1024);
            var heapSizeMB = gcInfo.HeapSizeBytes / (1024 * 1024);
            var memoryLoadPercent = (double)gcInfo.MemoryLoadBytes / gcInfo.TotalAvailableMemoryBytes * 100;
            
            // Get generation information
            var gen0Collections = GC.CollectionCount(0);
            var gen1Collections = GC.CollectionCount(1);
            var gen2Collections = GC.CollectionCount(2);
            var totalMemory = GC.GetTotalMemory(false) / (1024 * 1024);
            var allocatedMemory = GC.GetTotalAllocatedBytes() / (1024 * 1024);

            var data = new Dictionary<string, object>
            {
                ["WorkingSetMB"] = workingSetMB,
                ["PrivateMemoryMB"] = privateMemoryMB,
                ["VirtualMemoryMB"] = virtualMemoryMB,
                ["PagedMemoryMB"] = pagedMemoryMB,
                ["ManagedHeapMB"] = heapSizeMB,
                ["TotalManagedMemoryMB"] = totalMemory,
                ["TotalAllocatedMB"] = allocatedMemory,
                ["AvailableMemoryMB"] = totalMemoryMB,
                ["MemoryLoadPercent"] = Math.Round(memoryLoadPercent, 2),
                ["Gen0Collections"] = gen0Collections,
                ["Gen1Collections"] = gen1Collections,
                ["Gen2Collections"] = gen2Collections,
                ["IsServerGC"] = GCSettings.IsServerGC,
                ["LatencyMode"] = GCSettings.LatencyMode.ToString(),
                ["LargeObjectHeapCompactionMode"] = GCSettings.LargeObjectHeapCompactionMode.ToString()
            };

            // Add additional GC memory info if available
            if (gcInfo.Generation0Size > 0)
            {
                data["Gen0SizeMB"] = gcInfo.Generation0Size / (1024 * 1024);
                data["Gen1SizeMB"] = gcInfo.Generation1Size / (1024 * 1024);
                data["Gen2SizeMB"] = gcInfo.Generation2Size / (1024 * 1024);
                data["LOHSizeMB"] = gcInfo.LargeObjectHeapSize / (1024 * 1024);
                data["POHSizeMB"] = gcInfo.PinnedObjectHeapSize / (1024 * 1024);
            }

            // Check for fragmentation
            var fragmentedMemoryMB = gcInfo.FragmentedBytes / (1024 * 1024);
            if (fragmentedMemoryMB > 0)
            {
                data["FragmentedMemoryMB"] = fragmentedMemoryMB;
                var fragmentationPercent = (double)gcInfo.FragmentedBytes / gcInfo.HeapSizeBytes * 100;
                data["FragmentationPercent"] = Math.Round(fragmentationPercent, 2);
                
                if (fragmentationPercent > 30)
                {
                    _logger.LogWarning("High memory fragmentation detected: {Percent}%", fragmentationPercent);
                }
            }

            // Check thresholds
            if (workingSetMB > _maxMemoryMB)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Memory usage ({workingSetMB}MB) exceeds maximum ({_maxMemoryMB}MB)",
                    null,
                    data));
            }

            var usagePercentage = (double)workingSetMB / _maxMemoryMB * 100;
            if (usagePercentage > _warningThresholdPercentage)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Memory usage ({workingSetMB}MB, {usagePercentage:F1}%) is above warning threshold",
                    null,
                    data));
            }

            // Check for excessive Gen2 collections (potential memory pressure)
            if (gen2Collections > 10 && process.TotalProcessorTime.TotalMinutes > 1)
            {
                var gen2PerMinute = gen2Collections / process.TotalProcessorTime.TotalMinutes;
                if (gen2PerMinute > 1)
                {
                    data["Gen2PerMinute"] = Math.Round(gen2PerMinute, 2);
                    return Task.FromResult(HealthCheckResult.Degraded(
                        $"Excessive Gen2 garbage collections detected ({gen2PerMinute:F1} per minute)",
                        null,
                        data));
                }
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Memory usage is normal ({workingSetMB}MB, {usagePercentage:F1}%)",
                data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking memory health");
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Failed to check memory health: {ex.Message}",
                ex));
        }
    }
}