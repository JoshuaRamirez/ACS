using ACS.Infrastructure.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace ACS.WebApi.Controllers;

/// <summary>
/// Controller for diagnostic and troubleshooting endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Operator")]
public class DiagnosticsController : ControllerBase
{
    private readonly IDiagnosticService _diagnosticService;
    private readonly ILogger<DiagnosticsController> _logger;

    public DiagnosticsController(
        IDiagnosticService diagnosticService,
        ILogger<DiagnosticsController> logger)
    {
        _diagnosticService = diagnosticService;
        _logger = logger;
    }

    /// <summary>
    /// Get comprehensive system information
    /// </summary>
    [HttpGet("system")]
    [ProducesResponseType(typeof(SystemInfo), 200)]
    public async Task<IActionResult> GetSystemInfo()
    {
        var systemInfo = await _diagnosticService.GetSystemInfoAsync();
        return Ok(systemInfo);
    }

    /// <summary>
    /// Get memory usage information
    /// </summary>
    [HttpGet("memory")]
    [ProducesResponseType(typeof(MemoryInfo), 200)]
    public async Task<IActionResult> GetMemoryInfo()
    {
        var memoryInfo = await _diagnosticService.GetMemoryInfoAsync();
        return Ok(memoryInfo);
    }

    /// <summary>
    /// Get thread information
    /// </summary>
    [HttpGet("threads")]
    [ProducesResponseType(typeof(ThreadInfo), 200)]
    public async Task<IActionResult> GetThreadInfo()
    {
        var threadInfo = await _diagnosticService.GetThreadInfoAsync();
        return Ok(threadInfo);
    }

    /// <summary>
    /// Get process information
    /// </summary>
    [HttpGet("process")]
    [ProducesResponseType(typeof(ProcessInfo), 200)]
    public async Task<IActionResult> GetProcessInfo()
    {
        var processInfo = await _diagnosticService.GetProcessInfoAsync();
        return Ok(processInfo);
    }

    /// <summary>
    /// Get garbage collection information
    /// </summary>
    [HttpGet("gc")]
    [ProducesResponseType(typeof(GCInfo), 200)]
    public async Task<IActionResult> GetGCInfo()
    {
        var gcInfo = await _diagnosticService.GetGCInfoAsync();
        return Ok(gcInfo);
    }

    /// <summary>
    /// Get assembly information
    /// </summary>
    [HttpGet("assemblies")]
    [ProducesResponseType(typeof(AssemblyInfo), 200)]
    public async Task<IActionResult> GetAssemblyInfo()
    {
        var assemblyInfo = await _diagnosticService.GetAssemblyInfoAsync();
        return Ok(assemblyInfo);
    }

    /// <summary>
    /// Get connection pool information
    /// </summary>
    [HttpGet("connection-pools")]
    [ProducesResponseType(typeof(ConnectionPoolInfo), 200)]
    public async Task<IActionResult> GetConnectionPoolInfo()
    {
        var connectionPoolInfo = await _diagnosticService.GetConnectionPoolInfoAsync();
        return Ok(connectionPoolInfo);
    }

    /// <summary>
    /// Get current activity information
    /// </summary>
    [HttpGet("activity")]
    [ProducesResponseType(typeof(ActivityInfo), 200)]
    public async Task<IActionResult> GetActivityInfo()
    {
        var activityInfo = await _diagnosticService.GetActivityInfoAsync();
        return Ok(activityInfo);
    }

    /// <summary>
    /// Get performance counters
    /// </summary>
    [HttpGet("performance-counters")]
    [ProducesResponseType(typeof(Dictionary<string, double>), 200)]
    public async Task<IActionResult> GetPerformanceCounters()
    {
        var counters = await _diagnosticService.GetPerformanceCountersAsync();
        return Ok(counters);
    }

    /// <summary>
    /// Get thread stack traces
    /// </summary>
    [HttpGet("threads/stack-traces")]
    [ProducesResponseType(typeof(Dictionary<int, string>), 200)]
    public async Task<IActionResult> GetThreadStackTraces()
    {
        var stackTraces = await _diagnosticService.GetThreadStackTracesAsync();
        return Ok(stackTraces);
    }

    /// <summary>
    /// Trigger garbage collection
    /// </summary>
    [HttpPost("gc/collect")]
    [ProducesResponseType(typeof(GCCollectionResult), 200)]
    public async Task<IActionResult> TriggerGC(
        [FromQuery] int generation = 2,
        [FromQuery] GCCollectionMode mode = GCCollectionMode.Default)
    {
        var beforeMemory = GC.GetTotalMemory(false);
        var beforeGen0 = GC.CollectionCount(0);
        var beforeGen1 = GC.CollectionCount(1);
        var beforeGen2 = GC.CollectionCount(2);

        await _diagnosticService.TriggerGCAsync(generation, mode);

        var afterMemory = GC.GetTotalMemory(false);
        var afterGen0 = GC.CollectionCount(0);
        var afterGen1 = GC.CollectionCount(1);
        var afterGen2 = GC.CollectionCount(2);

        var result = new GCCollectionResult
        {
            Generation = generation,
            Mode = mode.ToString(),
            MemoryBeforeBytes = beforeMemory,
            MemoryAfterBytes = afterMemory,
            MemoryFreedBytes = beforeMemory - afterMemory,
            Gen0CollectionsBefore = beforeGen0,
            Gen0CollectionsAfter = afterGen0,
            Gen1CollectionsBefore = beforeGen1,
            Gen1CollectionsAfter = afterGen1,
            Gen2CollectionsBefore = beforeGen2,
            Gen2CollectionsAfter = afterGen2,
            Timestamp = DateTime.UtcNow
        };

        _logger.LogInformation("GC triggered via API: Generation {Generation}, Mode {Mode}, Memory freed: {MemoryFreed:N0} bytes",
            generation, mode, result.MemoryFreedBytes);

        return Ok(result);
    }

    /// <summary>
    /// Create a memory dump
    /// </summary>
    [HttpPost("memory/dump")]
    [ProducesResponseType(typeof(MemoryDumpResult), 200)]
    public async Task<IActionResult> CreateMemoryDump(
        [FromQuery] string? fileName = null,
        [FromQuery] bool includeHeap = false)
    {
        try
        {
            var dumpPath = await _diagnosticService.CreateMemoryDumpAsync(fileName, includeHeap);
            var fileInfo = new FileInfo(dumpPath);

            var result = new MemoryDumpResult
            {
                FilePath = dumpPath,
                FileName = fileInfo.Name,
                FileSizeBytes = fileInfo.Length,
                IncludeHeap = includeHeap,
                CreatedAt = fileInfo.CreationTimeUtc,
                Success = true
            };

            _logger.LogInformation("Memory dump created via API: {FilePath}", dumpPath);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create memory dump");

            var result = new MemoryDumpResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                CreatedAt = DateTime.UtcNow
            };

            return Ok(result);
        }
    }

    /// <summary>
    /// Get comprehensive diagnostic summary
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DiagnosticSummary), 200)]
    public async Task<IActionResult> GetDiagnosticSummary()
    {
        var tasks = new Task[]
        {
            _diagnosticService.GetSystemInfoAsync(),
            _diagnosticService.GetMemoryInfoAsync(),
            _diagnosticService.GetThreadInfoAsync(),
            _diagnosticService.GetProcessInfoAsync(),
            _diagnosticService.GetGCInfoAsync(),
            _diagnosticService.GetPerformanceCountersAsync()
        };

        await Task.WhenAll(tasks);

        var systemInfo = ((Task<SystemInfo>)tasks[0]).Result;
        var memoryInfo = ((Task<MemoryInfo>)tasks[1]).Result;
        var threadInfo = ((Task<ThreadInfo>)tasks[2]).Result;
        var processInfo = ((Task<ProcessInfo>)tasks[3]).Result;
        var gcInfo = ((Task<GCInfo>)tasks[4]).Result;
        var counters = ((Task<Dictionary<string, double>>)tasks[5]).Result;

        var summary = new DiagnosticSummary
        {
            Timestamp = DateTime.UtcNow,
            SystemInfo = systemInfo,
            MemoryInfo = memoryInfo,
            ThreadInfo = threadInfo,
            ProcessInfo = processInfo,
            GCInfo = gcInfo,
            PerformanceCounters = counters,
            
            // Calculate health indicators
            HealthIndicators = new DiagnosticHealthIndicators
            {
                MemoryUsagePercentage = (double)memoryInfo.WorkingSet / systemInfo.TotalPhysicalMemory * 100,
                ThreadUtilization = (double)threadInfo.WorkerThreadCount / threadInfo.MaxWorkerThreads * 100,
                GCPressure = gcInfo.Gen2Collections > 0 ? (double)gcInfo.TotalMemoryBytes / gcInfo.Gen2Collections : 0,
                HandleCount = processInfo.HandleCount,
                ResponseTime = Activity.Current?.Duration.TotalMilliseconds ?? 0
            }
        };

        return Ok(summary);
    }

    /// <summary>
    /// Export diagnostic data to file
    /// </summary>
    [HttpPost("export")]
    [ProducesResponseType(typeof(DiagnosticExportResult), 200)]
    public async Task<IActionResult> ExportDiagnostics(
        [FromQuery] DiagnosticExportFormat format = DiagnosticExportFormat.Json,
        [FromQuery] bool includeStackTraces = false,
        [FromQuery] bool includeMemoryDump = false)
    {
        try
        {
            var summary = await GetDiagnosticSummaryData();
            
            if (includeStackTraces)
            {
                summary.ThreadStackTraces = await _diagnosticService.GetThreadStackTracesAsync();
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"diagnostics_export_{timestamp}";
            string filePath;
            string contentType;

            switch (format)
            {
                case DiagnosticExportFormat.Json:
                    fileName += ".json";
                    filePath = Path.Combine(Path.GetTempPath(), fileName);
                    await System.IO.File.WriteAllTextAsync(filePath, 
                        System.Text.Json.JsonSerializer.Serialize(summary, new System.Text.Json.JsonSerializerOptions
                        {
                            WriteIndented = true,
                            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                        }));
                    contentType = "application/json";
                    break;

                case DiagnosticExportFormat.Xml:
                    fileName += ".xml";
                    filePath = Path.Combine(Path.GetTempPath(), fileName);
                    var xmlSerializer = new System.Xml.Serialization.XmlSerializer(typeof(DiagnosticSummary));
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        xmlSerializer.Serialize(fileStream, summary);
                    }
                    contentType = "application/xml";
                    break;

                case DiagnosticExportFormat.Csv:
                    fileName += ".csv";
                    filePath = Path.Combine(Path.GetTempPath(), fileName);
                    await WriteCsvExport(filePath, summary);
                    contentType = "text/csv";
                    break;

                default:
                    return BadRequest("Unsupported export format");
            }

            var result = new DiagnosticExportResult
            {
                FilePath = filePath,
                FileName = fileName,
                Format = format.ToString(),
                FileSizeBytes = new FileInfo(filePath).Length,
                IncludeStackTraces = includeStackTraces,
                IncludeMemoryDump = includeMemoryDump,
                CreatedAt = DateTime.UtcNow,
                Success = true
            };

            if (includeMemoryDump)
            {
                result.MemoryDumpPath = await _diagnosticService.CreateMemoryDumpAsync();
            }

            _logger.LogInformation("Diagnostic export created: {FilePath}", filePath);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export diagnostics");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private async Task<DiagnosticSummary> GetDiagnosticSummaryData()
    {
        var systemInfo = await _diagnosticService.GetSystemInfoAsync();
        var memoryInfo = await _diagnosticService.GetMemoryInfoAsync();
        var threadInfo = await _diagnosticService.GetThreadInfoAsync();
        var processInfo = await _diagnosticService.GetProcessInfoAsync();
        var gcInfo = await _diagnosticService.GetGCInfoAsync();
        var counters = await _diagnosticService.GetPerformanceCountersAsync();

        return new DiagnosticSummary
        {
            Timestamp = DateTime.UtcNow,
            SystemInfo = systemInfo,
            MemoryInfo = memoryInfo,
            ThreadInfo = threadInfo,
            ProcessInfo = processInfo,
            GCInfo = gcInfo,
            PerformanceCounters = counters,
            HealthIndicators = new DiagnosticHealthIndicators
            {
                MemoryUsagePercentage = (double)memoryInfo.WorkingSet / systemInfo.TotalPhysicalMemory * 100,
                ThreadUtilization = (double)threadInfo.WorkerThreadCount / threadInfo.MaxWorkerThreads * 100,
                GCPressure = gcInfo.Gen2Collections > 0 ? (double)gcInfo.TotalMemoryBytes / gcInfo.Gen2Collections : 0,
                HandleCount = processInfo.HandleCount,
                ResponseTime = Activity.Current?.Duration.TotalMilliseconds ?? 0
            }
        };
    }

    private static async Task WriteCsvExport(string filePath, DiagnosticSummary summary)
    {
        using var writer = new StreamWriter(filePath);

        // Write system info
        await writer.WriteLineAsync("Section,Property,Value");
        await writer.WriteLineAsync($"System,MachineName,{summary.SystemInfo.MachineName}");
        await writer.WriteLineAsync($"System,OSVersion,{summary.SystemInfo.OSVersion}");
        await writer.WriteLineAsync($"System,RuntimeVersion,{summary.SystemInfo.RuntimeVersion}");
        await writer.WriteLineAsync($"System,ProcessorCount,{summary.SystemInfo.ProcessorCount}");

        // Write memory info
        await writer.WriteLineAsync($"Memory,WorkingSet,{summary.MemoryInfo.WorkingSet}");
        await writer.WriteLineAsync($"Memory,PrivateMemorySize,{summary.MemoryInfo.PrivateMemorySize}");
        await writer.WriteLineAsync($"Memory,VirtualMemorySize,{summary.MemoryInfo.VirtualMemorySize}");

        // Write thread info
        await writer.WriteLineAsync($"Threads,ThreadCount,{summary.ThreadInfo.ThreadCount}");
        await writer.WriteLineAsync($"Threads,WorkerThreadCount,{summary.ThreadInfo.WorkerThreadCount}");
        await writer.WriteLineAsync($"Threads,MaxWorkerThreads,{summary.ThreadInfo.MaxWorkerThreads}");

        // Write GC info
        await writer.WriteLineAsync($"GC,TotalMemoryBytes,{summary.GCInfo.TotalMemoryBytes}");
        await writer.WriteLineAsync($"GC,Gen0Collections,{summary.GCInfo.Gen0Collections}");
        await writer.WriteLineAsync($"GC,Gen1Collections,{summary.GCInfo.Gen1Collections}");
        await writer.WriteLineAsync($"GC,Gen2Collections,{summary.GCInfo.Gen2Collections}");

        // Write performance counters
        foreach (var counter in summary.PerformanceCounters)
        {
            await writer.WriteLineAsync($"PerformanceCounter,{counter.Key},{counter.Value}");
        }
    }
}

// Response DTOs
public class GCCollectionResult
{
    public int Generation { get; set; }
    public string Mode { get; set; } = string.Empty;
    public long MemoryBeforeBytes { get; set; }
    public long MemoryAfterBytes { get; set; }
    public long MemoryFreedBytes { get; set; }
    public long Gen0CollectionsBefore { get; set; }
    public long Gen0CollectionsAfter { get; set; }
    public long Gen1CollectionsBefore { get; set; }
    public long Gen1CollectionsAfter { get; set; }
    public long Gen2CollectionsBefore { get; set; }
    public long Gen2CollectionsAfter { get; set; }
    public DateTime Timestamp { get; set; }
}

public class MemoryDumpResult
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public bool IncludeHeap { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class DiagnosticSummary
{
    public DateTime Timestamp { get; set; }
    public SystemInfo SystemInfo { get; set; } = new();
    public MemoryInfo MemoryInfo { get; set; } = new();
    public ThreadInfo ThreadInfo { get; set; } = new();
    public ProcessInfo ProcessInfo { get; set; } = new();
    public GCInfo GCInfo { get; set; } = new();
    public Dictionary<string, double> PerformanceCounters { get; set; } = new();
    public DiagnosticHealthIndicators HealthIndicators { get; set; } = new();
    public Dictionary<int, string>? ThreadStackTraces { get; set; }
}

public class DiagnosticHealthIndicators
{
    public double MemoryUsagePercentage { get; set; }
    public double ThreadUtilization { get; set; }
    public double GCPressure { get; set; }
    public int HandleCount { get; set; }
    public double ResponseTime { get; set; }
}

public enum DiagnosticExportFormat
{
    Json,
    Xml,
    Csv
}

public class DiagnosticExportResult
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public bool IncludeStackTraces { get; set; }
    public bool IncludeMemoryDump { get; set; }
    public string? MemoryDumpPath { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}