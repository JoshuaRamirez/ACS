using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace ACS.Infrastructure.HealthChecks;

/// <summary>
/// Health check for disk space availability
/// </summary>
public class DiskSpaceHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DiskSpaceHealthCheck> _logger;
    private readonly long _minimumFreeMegabytes;
    private readonly double _warningThresholdPercentage;

    public DiskSpaceHealthCheck(
        IConfiguration configuration,
        ILogger<DiskSpaceHealthCheck> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _minimumFreeMegabytes = configuration.GetValue<long>("HealthChecks:DiskSpace:MinimumFreeMB", 1024); // 1GB default
        _warningThresholdPercentage = configuration.GetValue<double>("HealthChecks:DiskSpace:WarningThresholdPercent", 10.0);
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady);
            var results = new List<DriveHealthInfo>();
            var unhealthyDrives = new List<string>();
            var degradedDrives = new List<string>();

            foreach (var drive in drives)
            {
                var info = new DriveHealthInfo
                {
                    Name = drive.Name,
                    VolumeLabel = drive.VolumeLabel,
                    DriveType = drive.DriveType.ToString(),
                    DriveFormat = drive.DriveFormat,
                    TotalSize = drive.TotalSize,
                    TotalFreeSpace = drive.TotalFreeSpace,
                    AvailableFreeSpace = drive.AvailableFreeSpace,
                    UsedSpace = drive.TotalSize - drive.TotalFreeSpace,
                    FreeSpacePercentage = (double)drive.AvailableFreeSpace / drive.TotalSize * 100
                };

                results.Add(info);

                // Check for critical low space
                var freeMegabytes = drive.AvailableFreeSpace / (1024 * 1024);
                if (freeMegabytes < _minimumFreeMegabytes)
                {
                    unhealthyDrives.Add($"{drive.Name} ({freeMegabytes}MB free)");
                }
                // Check for warning threshold
                else if (info.FreeSpacePercentage < _warningThresholdPercentage)
                {
                    degradedDrives.Add($"{drive.Name} ({info.FreeSpacePercentage:F1}% free)");
                }
            }

            var data = new Dictionary<string, object>();
            
            foreach (var drive in results)
            {
                var driveData = new Dictionary<string, object>
                {
                    ["TotalGB"] = Math.Round(drive.TotalSize / 1073741824.0, 2),
                    ["FreeGB"] = Math.Round(drive.AvailableFreeSpace / 1073741824.0, 2),
                    ["UsedGB"] = Math.Round(drive.UsedSpace / 1073741824.0, 2),
                    ["FreePercent"] = Math.Round(drive.FreeSpacePercentage, 2),
                    ["Type"] = drive.DriveType,
                    ["Format"] = drive.DriveFormat
                };

                if (!string.IsNullOrEmpty(drive.VolumeLabel))
                {
                    driveData["Label"] = drive.VolumeLabel;
                }

                data[$"Drive_{drive.Name.TrimEnd('\\').Replace(":", "")}"] = driveData;
            }

            if (unhealthyDrives.Any())
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Critical disk space on: {string.Join(", ", unhealthyDrives)}",
                    null,
                    data));
            }

            if (degradedDrives.Any())
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Low disk space warning on: {string.Join(", ", degradedDrives)}",
                    null,
                    data));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                "All drives have sufficient free space",
                data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking disk space");
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Failed to check disk space: {ex.Message}",
                ex));
        }
    }

    private class DriveHealthInfo
    {
        public string Name { get; set; } = string.Empty;
        public string VolumeLabel { get; set; } = string.Empty;
        public string DriveType { get; set; } = string.Empty;
        public string DriveFormat { get; set; } = string.Empty;
        public long TotalSize { get; set; }
        public long TotalFreeSpace { get; set; }
        public long AvailableFreeSpace { get; set; }
        public long UsedSpace { get; set; }
        public double FreeSpacePercentage { get; set; }
    }
}