using System.Diagnostics;

namespace ACS.Infrastructure.Diagnostics;

/// <summary>
/// Service for diagnostic operations
/// </summary>
public interface IDiagnosticService
{
    /// <summary>
    /// Gets current system information
    /// </summary>
    Task<SystemInfo> GetSystemInfoAsync();

    /// <summary>
    /// Gets current memory usage information
    /// </summary>
    Task<MemoryInfo> GetMemoryInfoAsync();

    /// <summary>
    /// Gets current thread information
    /// </summary>
    Task<ThreadInfo> GetThreadInfoAsync();

    /// <summary>
    /// Gets current process information
    /// </summary>
    Task<ProcessInfo> GetProcessInfoAsync();

    /// <summary>
    /// Gets garbage collection information
    /// </summary>
    Task<GCInfo> GetGCInfoAsync();

    /// <summary>
    /// Gets assembly information
    /// </summary>
    Task<AssemblyInfo> GetAssemblyInfoAsync();

    /// <summary>
    /// Gets connection pool information
    /// </summary>
    Task<ConnectionPoolInfo> GetConnectionPoolInfoAsync();

    /// <summary>
    /// Triggers garbage collection
    /// </summary>
    Task TriggerGCAsync(int generation = 2, GCCollectionMode mode = GCCollectionMode.Default);

    /// <summary>
    /// Creates a memory dump
    /// </summary>
    Task<string> CreateMemoryDumpAsync(string? fileName = null, bool includeHeap = false);

    /// <summary>
    /// Gets thread stack traces
    /// </summary>
    Task<Dictionary<int, string>> GetThreadStackTracesAsync();

    /// <summary>
    /// Gets current activity information
    /// </summary>
    Task<ActivityInfo> GetActivityInfoAsync();

    /// <summary>
    /// Gets performance counters
    /// </summary>
    Task<Dictionary<string, double>> GetPerformanceCountersAsync();
}

/// <summary>
/// System information
/// </summary>
public class SystemInfo
{
    public string MachineName { get; set; } = string.Empty;
    public string OSVersion { get; set; } = string.Empty;
    public string RuntimeVersion { get; set; } = string.Empty;
    public string FrameworkDescription { get; set; } = string.Empty;
    public string ProcessArchitecture { get; set; } = string.Empty;
    public string OSArchitecture { get; set; } = string.Empty;
    public int ProcessorCount { get; set; }
    public DateTime SystemStartTime { get; set; }
    public TimeSpan SystemUptime { get; set; }
    public long TotalPhysicalMemory { get; set; }
    public long AvailablePhysicalMemory { get; set; }
    public double CpuUsage { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserDomainName { get; set; } = string.Empty;
    public bool IsElevated { get; set; }
}

/// <summary>
/// Memory information
/// </summary>
public class MemoryInfo
{
    public long WorkingSet { get; set; }
    public long PrivateMemorySize { get; set; }
    public long VirtualMemorySize { get; set; }
    public long PagedMemorySize { get; set; }
    public long PagedSystemMemorySize { get; set; }
    public long NonpagedSystemMemorySize { get; set; }
    public long PeakWorkingSet { get; set; }
    public long PeakVirtualMemorySize { get; set; }
    public long PeakPagedMemorySize { get; set; }
    public long TotalAllocatedBytes { get; set; }
    public long Gen0Collections { get; set; }
    public long Gen1Collections { get; set; }
    public long Gen2Collections { get; set; }
    public long TotalMemory { get; set; }
    public Dictionary<int, long> GenerationSizes { get; set; } = new();
    public Dictionary<string, object> MemoryStatistics { get; set; } = new();
}

/// <summary>
/// Thread information
/// </summary>
public class ThreadInfo
{
    public int ThreadCount { get; set; }
    public int CompletionPortThreadCount { get; set; }
    public int WorkerThreadCount { get; set; }
    public int MaxWorkerThreads { get; set; }
    public int AvailableWorkerThreads { get; set; }
    public int MaxCompletionPortThreads { get; set; }
    public int AvailableCompletionPortThreads { get; set; }
    public List<ThreadDetails> Threads { get; set; } = new();
    public Dictionary<ThreadState, int> ThreadsByState { get; set; } = new();
    public int ManagedThreadId { get; set; }
    public bool IsThreadPoolThread { get; set; }
    public bool IsBackground { get; set; }
    public ThreadPriority Priority { get; set; }
}

/// <summary>
/// Individual thread details
/// </summary>
public class ThreadDetails
{
    public int ManagedThreadId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ThreadState ThreadState { get; set; }
    public ThreadPriority Priority { get; set; }
    public bool IsAlive { get; set; }
    public bool IsBackground { get; set; }
    public bool IsThreadPoolThread { get; set; }
    public string? StackTrace { get; set; }
    public TimeSpan TotalProcessorTime { get; set; }
    public TimeSpan UserProcessorTime { get; set; }
    public DateTime StartTime { get; set; }
    public ThreadWaitReason? WaitReason { get; set; }
}

/// <summary>
/// Process information
/// </summary>
public class ProcessInfo
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public TimeSpan TotalProcessorTime { get; set; }
    public TimeSpan UserProcessorTime { get; set; }
    public TimeSpan PrivilegedProcessorTime { get; set; }
    public int HandleCount { get; set; }
    public int SessionId { get; set; }
    public ProcessPriorityClass PriorityClass { get; set; }
    public bool Responding { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public string MainWindowTitle { get; set; } = string.Empty;
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
    public List<ProcessModule> Modules { get; set; } = new();
    public List<ProcessThread> ProcessThreads { get; set; } = new();
}

/// <summary>
/// Garbage collection information
/// </summary>
public class GCInfo
{
    public long TotalMemoryBytes { get; set; }
    public long Gen0Collections { get; set; }
    public long Gen1Collections { get; set; }
    public long Gen2Collections { get; set; }
    public bool IsServerGC { get; set; }
    public int MaxGeneration { get; set; }
    public GCLatencyMode LatencyMode { get; set; }
    public Dictionary<int, long> GenerationSizes { get; set; } = new();
    public long TotalPauseDuration { get; set; }
    public double CollectionEfficiency { get; set; }
    public List<GCGenerationInfo> Generations { get; set; } = new();
}

/// <summary>
/// GC generation information
/// </summary>
public class GCGenerationInfo
{
    public int Generation { get; set; }
    public long SizeBytes { get; set; }
    public long Collections { get; set; }
    public double FragmentationPercentage { get; set; }
}

/// <summary>
/// Assembly information
/// </summary>
public class AssemblyInfo
{
    public List<AssemblyDetails> LoadedAssemblies { get; set; } = new();
    public string EntryAssemblyName { get; set; } = string.Empty;
    public string EntryAssemblyVersion { get; set; } = string.Empty;
    public string EntryAssemblyLocation { get; set; } = string.Empty;
    public long TotalAssemblySize { get; set; }
    public int AssemblyCount { get; set; }
    public Dictionary<string, int> AssembliesByCompany { get; set; } = new();
}

/// <summary>
/// Individual assembly details
/// </summary>
public class AssemblyDetails
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public bool IsGAC { get; set; }
    public bool IsDynamic { get; set; }
    public DateTime LoadTime { get; set; }
    public string Company { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public List<string> ReferencedAssemblies { get; set; } = new();
}

/// <summary>
/// Connection pool information
/// </summary>
public class ConnectionPoolInfo
{
    public Dictionary<string, ConnectionPoolDetails> Pools { get; set; } = new();
    public int TotalConnections { get; set; }
    public int ActiveConnections { get; set; }
    public int IdleConnections { get; set; }
    public DateTime LastReset { get; set; }
}

/// <summary>
/// Individual connection pool details
/// </summary>
public class ConnectionPoolDetails
{
    public string ConnectionString { get; set; } = string.Empty;
    public int PoolSize { get; set; }
    public int MinPoolSize { get; set; }
    public int MaxPoolSize { get; set; }
    public int ActiveConnections { get; set; }
    public int IdleConnections { get; set; }
    public TimeSpan ConnectionLifetime { get; set; }
    public DateTime LastUsed { get; set; }
    public long TotalCreated { get; set; }
    public long TotalDestroyed { get; set; }
    public double UtilizationPercentage { get; set; }
}

/// <summary>
/// Activity information
/// </summary>
public class ActivityInfo
{
    public string? CurrentActivityId { get; set; }
    public string? CurrentActivityName { get; set; }
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public Dictionary<string, object?> Tags { get; set; } = new();
    public Dictionary<string, object?> Baggage { get; set; } = new();
    public List<ActivityEvent> Events { get; set; } = new();
    public ActivityStatusCode Status { get; set; }
    public string? StatusDescription { get; set; }
    public DateTime StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public List<ActivityInfo> Children { get; set; } = new();
}