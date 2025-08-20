using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace ACS.Infrastructure.Diagnostics;

/// <summary>
/// Service for diagnostic operations
/// </summary>
public class DiagnosticService : IDiagnosticService
{
    private readonly ILogger<DiagnosticService> _logger;
    private readonly Process _currentProcess;

    public DiagnosticService(ILogger<DiagnosticService> logger)
    {
        _logger = logger;
        _currentProcess = Process.GetCurrentProcess();
    }

    public async Task<SystemInfo> GetSystemInfoAsync()
    {
        return await Task.FromResult(new SystemInfo
        {
            MachineName = Environment.MachineName,
            OSVersion = Environment.OSVersion.ToString(),
            RuntimeVersion = Environment.Version.ToString(),
            FrameworkDescription = RuntimeInformation.FrameworkDescription,
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            OSArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            ProcessorCount = Environment.ProcessorCount,
            SystemStartTime = DateTime.Now - TimeSpan.FromMilliseconds(Environment.TickCount64),
            SystemUptime = TimeSpan.FromMilliseconds(Environment.TickCount64),
            TotalPhysicalMemory = GC.GetTotalMemory(false),
            AvailablePhysicalMemory = GetAvailablePhysicalMemory(),
            CpuUsage = GetCpuUsage(),
            UserName = Environment.UserName,
            UserDomainName = Environment.UserDomainName,
            IsElevated = IsCurrentProcessElevated()
        });
    }

    public async Task<MemoryInfo> GetMemoryInfoAsync()
    {
        var memoryInfo = new MemoryInfo
        {
            WorkingSet = _currentProcess.WorkingSet64,
            PrivateMemorySize = _currentProcess.PrivateMemorySize64,
            VirtualMemorySize = _currentProcess.VirtualMemorySize64,
            PagedMemorySize = _currentProcess.PagedMemorySize64,
            PagedSystemMemorySize = _currentProcess.PagedSystemMemorySize64,
            NonpagedSystemMemorySize = _currentProcess.NonpagedSystemMemorySize64,
            PeakWorkingSet = _currentProcess.PeakWorkingSet64,
            PeakVirtualMemorySize = _currentProcess.PeakVirtualMemorySize64,
            PeakPagedMemorySize = _currentProcess.PeakPagedMemorySize64,
            TotalMemory = GC.GetTotalMemory(false),
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2)
        };

        // Add generation sizes
        for (int i = 0; i <= GC.MaxGeneration; i++)
        {
            memoryInfo.GenerationSizes[i] = GC.GetTotalMemory(false);
        }

        // Add memory statistics
        var gcInfo = GC.GetGCMemoryInfo();
        memoryInfo.MemoryStatistics["TotalAllocatedBytes"] = GC.GetTotalAllocatedBytes();
        memoryInfo.MemoryStatistics["HeapSizeBytes"] = gcInfo.HeapSizeBytes;
        memoryInfo.MemoryStatistics["FragmentedBytes"] = gcInfo.FragmentedBytes;
        memoryInfo.MemoryStatistics["HighMemoryLoadThresholdBytes"] = gcInfo.HighMemoryLoadThresholdBytes;
        memoryInfo.MemoryStatistics["MemoryLoadBytes"] = gcInfo.MemoryLoadBytes;
        memoryInfo.MemoryStatistics["TotalAvailableMemoryBytes"] = gcInfo.TotalAvailableMemoryBytes;

        return await Task.FromResult(memoryInfo);
    }

    public async Task<ThreadInfo> GetThreadInfoAsync()
    {
        var threadInfo = new ThreadInfo
        {
            ManagedThreadId = Thread.CurrentThread.ManagedThreadId,
            IsThreadPoolThread = Thread.CurrentThread.IsThreadPoolThread,
            IsBackground = Thread.CurrentThread.IsBackground,
            Priority = Thread.CurrentThread.Priority
        };

        // Get thread pool information
        ThreadPool.GetAvailableThreads(out int availableWorker, out int availableCompletion);
        ThreadPool.GetMaxThreads(out int maxWorker, out int maxCompletion);
        
        threadInfo.AvailableWorkerThreads = availableWorker;
        threadInfo.AvailableCompletionPortThreads = availableCompletion;
        threadInfo.MaxWorkerThreads = maxWorker;
        threadInfo.MaxCompletionPortThreads = maxCompletion;
        threadInfo.WorkerThreadCount = maxWorker - availableWorker;
        threadInfo.CompletionPortThreadCount = maxCompletion - availableCompletion;

        // Get process threads
        _currentProcess.Refresh();
        threadInfo.ThreadCount = _currentProcess.Threads.Count;

        foreach (ProcessThread thread in _currentProcess.Threads)
        {
            try
            {
                var threadDetails = new ThreadDetails
                {
                    ManagedThreadId = thread.Id,
                    ThreadState = thread.ThreadState,
                    Priority = thread.PriorityLevel,
                    StartTime = thread.StartTime,
                    TotalProcessorTime = thread.TotalProcessorTime,
                    UserProcessorTime = thread.UserProcessorTime,
                    WaitReason = thread.ThreadState == ThreadState.Wait ? thread.WaitReason : null
                };

                threadInfo.Threads.Add(threadDetails);

                // Count threads by state
                if (threadInfo.ThreadsByState.ContainsKey(thread.ThreadState))
                {
                    threadInfo.ThreadsByState[thread.ThreadState]++;
                }
                else
                {
                    threadInfo.ThreadsByState[thread.ThreadState] = 1;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get thread details for thread {ThreadId}", thread.Id);
            }
        }

        return await Task.FromResult(threadInfo);
    }

    public async Task<ProcessInfo> GetProcessInfoAsync()
    {
        _currentProcess.Refresh();

        var processInfo = new ProcessInfo
        {
            ProcessId = _currentProcess.Id,
            ProcessName = _currentProcess.ProcessName,
            StartTime = _currentProcess.StartTime,
            TotalProcessorTime = _currentProcess.TotalProcessorTime,
            UserProcessorTime = _currentProcess.UserProcessorTime,
            PrivilegedProcessorTime = _currentProcess.PrivilegedProcessorTime,
            HandleCount = _currentProcess.HandleCount,
            SessionId = _currentProcess.SessionId,
            PriorityClass = _currentProcess.PriorityClass,
            Responding = _currentProcess.Responding,
            MachineName = _currentProcess.MachineName,
            MainWindowTitle = _currentProcess.MainWindowTitle
        };

        // Get environment variables
        foreach (DictionaryEntry envVar in Environment.GetEnvironmentVariables())
        {
            var key = envVar.Key?.ToString() ?? string.Empty;
            var value = envVar.Value?.ToString() ?? string.Empty;
            
            // Redact sensitive environment variables
            if (IsSensitiveEnvironmentVariable(key))
            {
                value = "***REDACTED***";
            }
            
            processInfo.EnvironmentVariables[key] = value;
        }

        // Get loaded modules
        try
        {
            foreach (ProcessModule module in _currentProcess.Modules)
            {
                processInfo.Modules.Add(module);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get process modules");
        }

        // Get process threads
        foreach (ProcessThread thread in _currentProcess.Threads)
        {
            processInfo.ProcessThreads.Add(thread);
        }

        return await Task.FromResult(processInfo);
    }

    public async Task<GCInfo> GetGCInfoAsync()
    {
        var gcInfo = new GCInfo
        {
            TotalMemoryBytes = GC.GetTotalMemory(false),
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            IsServerGC = GCSettings.IsServerGC,
            MaxGeneration = GC.MaxGeneration,
            LatencyMode = GCSettings.LatencyMode
        };

        // Get generation sizes
        for (int i = 0; i <= GC.MaxGeneration; i++)
        {
            gcInfo.GenerationSizes[i] = GC.GetTotalMemory(false);
            
            gcInfo.Generations.Add(new GCGenerationInfo
            {
                Generation = i,
                Collections = GC.CollectionCount(i),
                SizeBytes = GC.GetTotalMemory(false)
            });
        }

        // Calculate collection efficiency
        var totalCollections = gcInfo.Gen0Collections + gcInfo.Gen1Collections + gcInfo.Gen2Collections;
        if (totalCollections > 0)
        {
            gcInfo.CollectionEfficiency = (double)gcInfo.TotalMemoryBytes / totalCollections;
        }

        return await Task.FromResult(gcInfo);
    }

    public async Task<AssemblyInfo> GetAssemblyInfoAsync()
    {
        var assemblyInfo = new AssemblyInfo();
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly != null)
        {
            assemblyInfo.EntryAssemblyName = entryAssembly.GetName().Name ?? string.Empty;
            assemblyInfo.EntryAssemblyVersion = entryAssembly.GetName().Version?.ToString() ?? string.Empty;
            assemblyInfo.EntryAssemblyLocation = entryAssembly.Location;
        }

        assemblyInfo.AssemblyCount = loadedAssemblies.Length;

        foreach (var assembly in loadedAssemblies)
        {
            try
            {
                var assemblyDetails = new AssemblyDetails
                {
                    Name = assembly.GetName().Name ?? string.Empty,
                    Version = assembly.GetName().Version?.ToString() ?? string.Empty,
                    Location = assembly.Location,
                    FullName = assembly.FullName ?? string.Empty,
                    IsGAC = assembly.GlobalAssemblyCache,
                    IsDynamic = assembly.IsDynamic,
                    LoadTime = DateTime.Now // Approximation
                };

                // Get assembly size
                if (!string.IsNullOrEmpty(assemblyDetails.Location) && File.Exists(assemblyDetails.Location))
                {
                    assemblyDetails.SizeBytes = new FileInfo(assemblyDetails.Location).Length;
                    assemblyInfo.TotalAssemblySize += assemblyDetails.SizeBytes;
                }

                // Get assembly attributes
                try
                {
                    var companyAttribute = assembly.GetCustomAttribute<AssemblyCompanyAttribute>();
                    if (companyAttribute != null)
                    {
                        assemblyDetails.Company = companyAttribute.Company;
                        
                        if (assemblyInfo.AssembliesByCompany.ContainsKey(assemblyDetails.Company))
                        {
                            assemblyInfo.AssembliesByCompany[assemblyDetails.Company]++;
                        }
                        else
                        {
                            assemblyInfo.AssembliesByCompany[assemblyDetails.Company] = 1;
                        }
                    }

                    var productAttribute = assembly.GetCustomAttribute<AssemblyProductAttribute>();
                    if (productAttribute != null)
                    {
                        assemblyDetails.Product = productAttribute.Product;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to get assembly attributes for {AssemblyName}", assemblyDetails.Name);
                }

                // Get referenced assemblies
                foreach (var referencedAssembly in assembly.GetReferencedAssemblies())
                {
                    assemblyDetails.ReferencedAssemblies.Add(referencedAssembly.FullName);
                }

                assemblyInfo.LoadedAssemblies.Add(assemblyDetails);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get assembly details for {AssemblyName}", assembly.GetName().Name);
            }
        }

        return await Task.FromResult(assemblyInfo);
    }

    public async Task<ConnectionPoolInfo> GetConnectionPoolInfoAsync()
    {
        // This is a simplified implementation - in a real application,
        // you would integrate with your specific connection pool implementations
        var connectionPoolInfo = new ConnectionPoolInfo
        {
            LastReset = DateTime.Now
        };

        // Add placeholder data - in practice, you'd integrate with your actual connection pools
        connectionPoolInfo.Pools["DefaultConnection"] = new ConnectionPoolDetails
        {
            ConnectionString = "***REDACTED***",
            PoolSize = 10,
            MinPoolSize = 5,
            MaxPoolSize = 100,
            ActiveConnections = 3,
            IdleConnections = 7,
            ConnectionLifetime = TimeSpan.FromMinutes(30),
            LastUsed = DateTime.Now.AddMinutes(-5),
            TotalCreated = 25,
            TotalDestroyed = 15,
            UtilizationPercentage = 30.0
        };

        connectionPoolInfo.TotalConnections = connectionPoolInfo.Pools.Values.Sum(p => p.PoolSize);
        connectionPoolInfo.ActiveConnections = connectionPoolInfo.Pools.Values.Sum(p => p.ActiveConnections);
        connectionPoolInfo.IdleConnections = connectionPoolInfo.Pools.Values.Sum(p => p.IdleConnections);

        return await Task.FromResult(connectionPoolInfo);
    }

    public async Task TriggerGCAsync(int generation = 2, GCCollectionMode mode = GCCollectionMode.Default)
    {
        _logger.LogInformation("Triggering GC for generation {Generation} with mode {Mode}", generation, mode);
        
        await Task.Run(() =>
        {
            var beforeMemory = GC.GetTotalMemory(false);
            GC.Collect(generation, mode);
            GC.WaitForPendingFinalizers();
            var afterMemory = GC.GetTotalMemory(false);
            
            _logger.LogInformation("GC completed. Memory before: {BeforeMemory:N0} bytes, after: {AfterMemory:N0} bytes, freed: {FreedMemory:N0} bytes",
                beforeMemory, afterMemory, beforeMemory - afterMemory);
        });
    }

    public async Task<string> CreateMemoryDumpAsync(string? fileName = null, bool includeHeap = false)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        fileName ??= $"memorydump_{_currentProcess.ProcessName}_{timestamp}.dmp";

        var dumpPath = Path.Combine(Path.GetTempPath(), fileName);

        _logger.LogInformation("Creating memory dump: {DumpPath}, Include heap: {IncludeHeap}", dumpPath, includeHeap);

        try
        {
            // This is a simplified implementation - in practice, you might use:
            // - Windows: MiniDumpWriteDump API
            // - Linux: gcore or similar
            // - Cross-platform: dotnet-dump tool
            
            await Task.Run(() =>
            {
                using var fileStream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write);
                using var writer = new StreamWriter(fileStream);
                
                // Write basic process information
                writer.WriteLine($"Process Dump - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine($"Process ID: {_currentProcess.Id}");
                writer.WriteLine($"Process Name: {_currentProcess.ProcessName}");
                writer.WriteLine($"Working Set: {_currentProcess.WorkingSet64:N0} bytes");
                writer.WriteLine($"Private Memory: {_currentProcess.PrivateMemorySize64:N0} bytes");
                writer.WriteLine($"Virtual Memory: {_currentProcess.VirtualMemorySize64:N0} bytes");
                writer.WriteLine();
                
                // Write thread information
                writer.WriteLine("Threads:");
                foreach (ProcessThread thread in _currentProcess.Threads)
                {
                    writer.WriteLine($"  Thread {thread.Id}: State={thread.ThreadState}, Priority={thread.PriorityLevel}");
                }
                writer.WriteLine();
                
                // Write loaded modules
                writer.WriteLine("Loaded Modules:");
                foreach (ProcessModule module in _currentProcess.Modules)
                {
                    writer.WriteLine($"  {module.ModuleName}: {module.BaseAddress} - {module.EntryPointAddress}");
                }
                
                if (includeHeap)
                {
                    writer.WriteLine();
                    writer.WriteLine("GC Information:");
                    writer.WriteLine($"Total Memory: {GC.GetTotalMemory(false):N0} bytes");
                    writer.WriteLine($"Gen 0 Collections: {GC.CollectionCount(0)}");
                    writer.WriteLine($"Gen 1 Collections: {GC.CollectionCount(1)}");
                    writer.WriteLine($"Gen 2 Collections: {GC.CollectionCount(2)}");
                }
            });

            _logger.LogInformation("Memory dump created successfully: {DumpPath}", dumpPath);
            return dumpPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create memory dump: {DumpPath}", dumpPath);
            throw;
        }
    }

    public async Task<Dictionary<int, string>> GetThreadStackTracesAsync()
    {
        var stackTraces = new Dictionary<int, string>();

        await Task.Run(() =>
        {
            foreach (ProcessThread thread in _currentProcess.Threads)
            {
                try
                {
                    // Note: Getting stack traces for other threads is complex and platform-specific
                    // This is a simplified implementation
                    stackTraces[thread.Id] = $"Thread {thread.Id} - State: {thread.ThreadState}";
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to get stack trace for thread {ThreadId}", thread.Id);
                    stackTraces[thread.Id] = $"Thread {thread.Id} - Unable to get stack trace";
                }
            }
            
            // Add current thread stack trace
            var currentThread = Thread.CurrentThread;
            stackTraces[currentThread.ManagedThreadId] = Environment.StackTrace;
        });

        return stackTraces;
    }

    public async Task<ActivityInfo> GetActivityInfoAsync()
    {
        var current = Activity.Current;
        if (current == null)
        {
            return await Task.FromResult(new ActivityInfo());
        }

        var activityInfo = new ActivityInfo
        {
            CurrentActivityId = current.Id,
            CurrentActivityName = current.OperationName,
            TraceId = current.TraceId.ToString(),
            SpanId = current.SpanId.ToString(),
            Status = current.Status,
            StatusDescription = current.StatusDescription,
            StartTime = current.StartTimeUtc,
            Duration = current.Duration
        };

        // Get tags
        foreach (var tag in current.Tags)
        {
            activityInfo.Tags[tag.Key] = tag.Value;
        }

        // Get baggage
        foreach (var baggage in current.Baggage)
        {
            activityInfo.Baggage[baggage.Key] = baggage.Value;
        }

        // Get events
        foreach (var activityEvent in current.Events)
        {
            activityInfo.Events.Add(activityEvent);
        }

        return await Task.FromResult(activityInfo);
    }

    public async Task<Dictionary<string, double>> GetPerformanceCountersAsync()
    {
        var counters = new Dictionary<string, double>();

        await Task.Run(() =>
        {
            try
            {
                // Add basic performance metrics
                counters["Process.WorkingSet"] = _currentProcess.WorkingSet64;
                counters["Process.PrivateMemory"] = _currentProcess.PrivateMemorySize64;
                counters["Process.VirtualMemory"] = _currentProcess.VirtualMemorySize64;
                counters["Process.ThreadCount"] = _currentProcess.Threads.Count;
                counters["Process.HandleCount"] = _currentProcess.HandleCount;
                
                counters["GC.TotalMemory"] = GC.GetTotalMemory(false);
                counters["GC.Gen0Collections"] = GC.CollectionCount(0);
                counters["GC.Gen1Collections"] = GC.CollectionCount(1);
                counters["GC.Gen2Collections"] = GC.CollectionCount(2);
                
                ThreadPool.GetAvailableThreads(out int availableWorker, out int availableCompletion);
                ThreadPool.GetMaxThreads(out int maxWorker, out int maxCompletion);
                
                counters["ThreadPool.WorkerThreads"] = maxWorker - availableWorker;
                counters["ThreadPool.CompletionPortThreads"] = maxCompletion - availableCompletion;
                counters["ThreadPool.AvailableWorkerThreads"] = availableWorker;
                counters["ThreadPool.AvailableCompletionPortThreads"] = availableCompletion;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get performance counters");
            }
        });

        return counters;
    }

    private static long GetAvailablePhysicalMemory()
    {
        try
        {
            var gcMemoryInfo = GC.GetGCMemoryInfo();
            return gcMemoryInfo.TotalAvailableMemoryBytes;
        }
        catch
        {
            return 0;
        }
    }

    private double GetCpuUsage()
    {
        try
        {
            // This is a simplified implementation
            // In practice, you would use performance counters or WMI
            return (_currentProcess.TotalProcessorTime.TotalMilliseconds / Environment.TickCount64) * 100;
        }
        catch
        {
            return 0.0;
        }
    }

    private static bool IsCurrentProcessElevated()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            
            // For non-Windows platforms, check if running as root
            return Environment.UserName == "root";
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSensitiveEnvironmentVariable(string name)
    {
        var sensitiveNames = new[]
        {
            "PASSWORD", "SECRET", "KEY", "TOKEN", "CREDENTIAL", "CONNECTION",
            "API_KEY", "AUTH", "PRIVATE", "CERT", "CERTIFICATE"
        };

        return sensitiveNames.Any(sensitive => 
            name.Contains(sensitive, StringComparison.OrdinalIgnoreCase));
    }
}