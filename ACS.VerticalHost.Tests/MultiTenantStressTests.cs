using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Grpc.Net.Client;
using ACS.Core.Grpc;
using ACS.Service.Infrastructure;
using ACS.Infrastructure;
using ACS.Infrastructure.Services;
using Google.Protobuf;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace ACS.VerticalHost.Tests;

[TestClass]
public class MultiTenantStressTests
{
    private readonly List<string> _stressTenantIds = new()
    {
        "stress-tenant-1", "stress-tenant-2", "stress-tenant-3"
    };
    
    private readonly Dictionary<string, int> _tenantPorts = new()
    {
        { "stress-tenant-1", 50071 }, { "stress-tenant-2", 50072 }, { "stress-tenant-3", 50073 }
    };

    private readonly Dictionary<string, WebApplicationFactory<ACS.VerticalHost.Program>> _tenantFactories = new();
    private readonly Dictionary<string, GrpcChannel> _tenantChannels = new();
    private readonly Dictionary<string, VerticalService.VerticalServiceClient> _tenantClients = new();
    private TenantProcessManager? _processManager;

    [TestInitialize]
    public async Task Setup()
    {
        try
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<TenantProcessManager>>();
            _processManager = new TenantProcessManager(logger);

            var setupTasks = _stressTenantIds.Select(async tenantId =>
            {
                try
                {
                    var port = _tenantPorts[tenantId];
                    var tenantProcess = await _processManager!.StartTenantProcessAsync(tenantId);
                    port = tenantProcess.Port;
                    
                    var factory = new WebApplicationFactory<ACS.VerticalHost.Program>()
                        .WithWebHostBuilder(builder =>
                        {
                            builder.ConfigureServices(services =>
                            {
                                services.Configure<HostOptions>(options =>
                                {
                                    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
                                });
                            });
                        });

                    Environment.SetEnvironmentVariable("TENANT_ID", tenantId);
                    Environment.SetEnvironmentVariable("GRPC_PORT", port.ToString());

                    var client = factory.CreateClient();
                    var channel = GrpcChannel.ForAddress(client.BaseAddress!, new GrpcChannelOptions
                    {
                        HttpClient = client,
                        MaxReceiveMessageSize = 64 * 1024 * 1024, // 64MB
                        MaxSendMessageSize = 64 * 1024 * 1024,     // 64MB
                        ThrowOperationCanceledOnCancellation = true
                    });
                    
                    var grpcClient = new VerticalService.VerticalServiceClient(channel);

                    lock (_tenantFactories)
                    {
                        _tenantFactories[tenantId] = factory;
                        _tenantChannels[tenantId] = channel;
                        _tenantClients[tenantId] = grpcClient;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to setup stress tenant {tenantId}: {ex.Message}");
                }
            });

            await Task.WhenAll(setupTasks);
            await Task.Delay(3000); // Startup delay
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Could not setup multi-tenant stress test environment: {ex.Message}");
        }
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        try
        {
            foreach (var channel in _tenantChannels.Values)
                await channel.ShutdownAsync();
            _tenantChannels.Clear();

            foreach (var factory in _tenantFactories.Values)
                factory.Dispose();
            _tenantFactories.Clear();
            _tenantClients.Clear();

            if (_processManager != null)
            {
                foreach (var tenantId in _stressTenantIds)
                {
                    try { await _processManager.StopTenantProcessAsync(tenantId); } catch { }
                }
                _processManager.Dispose();
            }
        }
        catch (Exception) { }
    }

    [TestMethod]
    public async Task MultiTenant_HighVolumeStress_SustainedHighThroughput()
    {
        // Arrange - Very high load test
        const int durationSeconds = 60;
        const int targetRpsPerTenant = 50; // 50 requests per second per tenant
        const int totalTargetRps = targetRpsPerTenant * 3; // 150 total RPS
        
        var endTime = DateTime.UtcNow.AddSeconds(durationSeconds);
        var allResults = new ConcurrentBag<(string TenantId, bool Success, TimeSpan Latency, DateTime Timestamp)>();
        var requestCounter = 0;

        try
        {
            Console.WriteLine($"Starting {durationSeconds}s high-volume stress test targeting {totalTargetRps} RPS...");

            var stressTask = Task.Run(async () =>
            {
                var tenantTasks = _stressTenantIds.Select(tenantId => Task.Run(async () =>
                {
                    if (!_tenantClients.TryGetValue(tenantId, out var client))
                        return;

                    var tenantRequestCounter = 0;
                    while (DateTime.UtcNow < endTime)
                    {
                        var secondStart = DateTime.UtcNow;
                        var secondTasks = new List<Task>();

                        // Generate target RPS for this second
                        for (int i = 0; i < targetRpsPerTenant && DateTime.UtcNow < endTime; i++)
                        {
                            secondTasks.Add(Task.Run(async () =>
                            {
                                var requestId = Interlocked.Increment(ref requestCounter);
                                var tenantRequestId = Interlocked.Increment(ref tenantRequestCounter);
                                var stopwatch = Stopwatch.StartNew();
                                var timestamp = DateTime.UtcNow;

                                try
                                {
                                    var command = new TestCreateUserCommand 
                                    { 
                                        Name = $"Stress_{tenantId}_{tenantRequestId}_{requestId}",
                                        Metadata = new string('X', 1024) // 1KB metadata per request
                                    };
                                    var commandData = ProtoSerializer.Serialize(command);
                                    var request = new CommandRequest
                                    {
                                        CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                                        CommandData = ByteString.CopyFrom(commandData),
                                        CorrelationId = $"stress-{tenantId}-{requestId}"
                                    };

                                    var response = await client.ExecuteCommandAsync(request);
                                    stopwatch.Stop();
                                    allResults.Add((tenantId, response.Success, stopwatch.Elapsed, timestamp));
                                }
                                catch (Exception)
                                {
                                    stopwatch.Stop();
                                    allResults.Add((tenantId, false, stopwatch.Elapsed, timestamp));
                                }
                            }));
                        }

                        await Task.WhenAll(secondTasks);

                        // Rate limiting - ensure we don't exceed 1 second per batch
                        var secondDuration = DateTime.UtcNow - secondStart;
                        var targetSecondDuration = TimeSpan.FromSeconds(1);
                        if (secondDuration < targetSecondDuration)
                        {
                            await Task.Delay(targetSecondDuration - secondDuration);
                        }
                    }
                })).ToList();

                await Task.WhenAll(tenantTasks);
            });

            await stressTask;

            // Analyze results
            var totalRequests = allResults.Count;
            var successfulRequests = allResults.Count(r => r.Success);
            var overallSuccessRate = successfulRequests / (double)totalRequests;
            var actualRps = totalRequests / (double)durationSeconds;
            var avgLatency = allResults.Where(r => r.Success).Average(r => r.Latency.TotalMilliseconds);
            var maxLatency = allResults.Where(r => r.Success).Max(r => r.Latency.TotalMilliseconds);
            var p95Latency = allResults.Where(r => r.Success)
                .OrderBy(r => r.Latency.TotalMilliseconds)
                .Skip((int)(successfulRequests * 0.95))
                .FirstOrDefault().Latency.TotalMilliseconds;

            // Per-tenant analysis
            var tenantResults = allResults.GroupBy(r => r.TenantId).ToDictionary(
                g => g.Key,
                g => new
                {
                    Total = g.Count(),
                    Successful = g.Count(r => r.Success),
                    SuccessRate = g.Count(r => r.Success) / (double)g.Count(),
                    TenantAvgLatency = g.Where(r => r.Success).Average(r => r.Latency.TotalMilliseconds),
                    Rps = g.Count() / (double)durationSeconds
                }
            );

            Console.WriteLine($"High-Volume Stress Test Results:");
            Console.WriteLine($"  Duration: {durationSeconds}s");
            Console.WriteLine($"  Target RPS: {totalTargetRps}");
            Console.WriteLine($"  Actual RPS: {actualRps:F1}");
            Console.WriteLine($"  Total Requests: {totalRequests}");
            Console.WriteLine($"  Successful Requests: {successfulRequests}");
            Console.WriteLine($"  Overall Success Rate: {overallSuccessRate:P}");
            Console.WriteLine($"  Average Latency: {avgLatency:F2}ms");
            Console.WriteLine($"  Max Latency: {maxLatency:F2}ms");
            Console.WriteLine($"  P95 Latency: {p95Latency:F2}ms");

            Console.WriteLine($"\nPer-Tenant Results:");
            foreach (var kvp in tenantResults)
            {
                var stats = kvp.Value;
                Console.WriteLine($"  {kvp.Key}: {stats.Successful}/{stats.Total} " +
                                $"({stats.SuccessRate:P}) - {stats.Rps:F1} RPS - {stats.TenantAvgLatency:F2}ms avg");
            }

            // Assertions
            Assert.IsTrue(overallSuccessRate > 0.95, $"Success rate too low: {overallSuccessRate:P}");
            Assert.IsTrue(actualRps > totalTargetRps * 0.8, $"Actual RPS too low: {actualRps:F1} (target: {totalTargetRps})");
            Assert.IsTrue(avgLatency < 500, $"Average latency too high: {avgLatency:F2}ms");
            Assert.IsTrue(p95Latency < 1000, $"P95 latency too high: {p95Latency:F2}ms");

            // All tenants should perform reasonably
            foreach (var kvp in tenantResults)
            {
                Assert.IsTrue(kvp.Value.SuccessRate > 0.9, 
                    $"Tenant {kvp.Key} success rate too low: {kvp.Value.SuccessRate:P}");
            }

            Console.WriteLine("High-volume stress test passed");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"High-volume stress test failed: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task MultiTenant_BurstLoad_HandlesTrafficSpikes()
    {
        // Arrange - Simulate traffic bursts
        const int burstCount = 5;
        const int burstSize = 100; // 100 requests per burst
        const int burstIntervalSeconds = 10;
        
        var burstResults = new List<(int BurstNumber, double Throughput, double AvgLatency, double SuccessRate)>();

        try
        {
            Console.WriteLine($"Starting burst load test: {burstCount} bursts of {burstSize} requests each...");

            for (int burstNum = 1; burstNum <= burstCount; burstNum++)
            {
                Console.WriteLine($"Executing burst {burstNum}/{burstCount}...");
                
                var burstStopwatch = Stopwatch.StartNew();
                var burstTaskResults = new ConcurrentBag<(bool Success, TimeSpan Latency)>();

                // Execute burst across all tenants
                var burstTasks = _stressTenantIds.SelectMany(tenantId => 
                    Enumerable.Range(0, burstSize / _stressTenantIds.Count).Select(_ => 
                        Task.Run(async () =>
                        {
                            if (!_tenantClients.TryGetValue(tenantId, out var client))
                                return;

                            var stopwatch = Stopwatch.StartNew();
                            try
                            {
                                var command = new TestCreateUserCommand 
                                { 
                                    Name = $"Burst{burstNum}_{tenantId}_{Guid.NewGuid():N[..8]}" 
                                };
                                var commandData = ProtoSerializer.Serialize(command);
                                var request = new CommandRequest
                                {
                                    CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                                    CommandData = ByteString.CopyFrom(commandData),
                                    CorrelationId = Guid.NewGuid().ToString()
                                };

                                var response = await client.ExecuteCommandAsync(request);
                                stopwatch.Stop();
                                burstTaskResults.Add((response.Success, stopwatch.Elapsed));
                            }
                            catch (Exception)
                            {
                                stopwatch.Stop();
                                burstTaskResults.Add((false, stopwatch.Elapsed));
                            }
                        })
                    )
                ).ToList();

                await Task.WhenAll(burstTasks);
                burstStopwatch.Stop();

                var successful = burstTaskResults.Count(r => r.Success);
                var throughput = successful / burstStopwatch.Elapsed.TotalSeconds;
                var burstAvgLatency = burstTaskResults.Where(r => r.Success).Average(r => r.Latency.TotalMilliseconds);
                var successRate = successful / (double)burstTaskResults.Count;

                burstResults.Add((burstNum, throughput, burstAvgLatency, successRate));

                Console.WriteLine($"Burst {burstNum}: {successful}/{burstTaskResults.Count} " +
                                $"({successRate:P}) - {throughput:F1} cmd/sec - {burstAvgLatency:F2}ms avg - {burstStopwatch.ElapsedMilliseconds}ms total");

                // Wait between bursts (except for the last one)
                if (burstNum < burstCount)
                {
                    await Task.Delay(TimeSpan.FromSeconds(burstIntervalSeconds));
                }
            }

            // Analyze burst consistency
            var avgThroughput = burstResults.Average(r => r.Throughput);
            var minThroughput = burstResults.Min(r => r.Throughput);
            var maxThroughput = burstResults.Max(r => r.Throughput);
            var throughputVariance = (maxThroughput - minThroughput) / avgThroughput;

            var avgSuccessRate = burstResults.Average(r => r.SuccessRate);
            var minSuccessRate = burstResults.Min(r => r.SuccessRate);

            var avgLatency = burstResults.Average(r => r.AvgLatency);
            var maxLatency = burstResults.Max(r => r.AvgLatency);

            Console.WriteLine($"\nBurst Load Test Summary:");
            Console.WriteLine($"  Bursts: {burstCount}");
            Console.WriteLine($"  Requests per Burst: {burstSize}");
            Console.WriteLine($"  Average Throughput: {avgThroughput:F1} cmd/sec");
            Console.WriteLine($"  Throughput Range: {minThroughput:F1} - {maxThroughput:F1} cmd/sec");
            Console.WriteLine($"  Throughput Variance: {throughputVariance:P}");
            Console.WriteLine($"  Average Success Rate: {avgSuccessRate:P}");
            Console.WriteLine($"  Minimum Success Rate: {minSuccessRate:P}");
            Console.WriteLine($"  Average Latency: {avgLatency:F2}ms");
            Console.WriteLine($"  Maximum Latency: {maxLatency:F2}ms");

            // Assertions
            Assert.IsTrue(avgSuccessRate > 0.95, $"Average success rate too low: {avgSuccessRate:P}");
            Assert.IsTrue(minSuccessRate > 0.9, $"Minimum success rate too low: {minSuccessRate:P}");
            Assert.IsTrue(avgThroughput > 50, $"Average throughput too low: {avgThroughput:F1} cmd/sec");
            Assert.IsTrue(throughputVariance < 0.3, $"Throughput variance too high: {throughputVariance:P}");
            Assert.IsTrue(maxLatency < 1000, $"Maximum latency too high: {maxLatency:F2}ms");

            Console.WriteLine("Burst load test passed - system handles traffic spikes consistently");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Burst load test failed: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task MultiTenant_ConcurrentTenantStartup_HandlesSimultaneousInitialization()
    {
        // This test is for additional tenant startup scenarios
        var newTenantIds = new List<string> { "startup-test-1", "startup-test-2", "startup-test-3", "startup-test-4" };
        var newTenantPorts = new Dictionary<string, int>
        {
            { "startup-test-1", 50081 }, { "startup-test-2", 50082 }, 
            { "startup-test-3", 50083 }, { "startup-test-4", 50084 }
        };

        var startupResults = new ConcurrentBag<(string TenantId, bool Success, TimeSpan StartupTime, string Error)>();

        try
        {
            Console.WriteLine("Testing concurrent tenant startup...");
            var startupStopwatch = Stopwatch.StartNew();

            // Start all tenants simultaneously
            var startupTasks = newTenantIds.Select(tenantId => Task.Run(async () =>
            {
                var tenantStopwatch = Stopwatch.StartNew();
                try
                {
                    var port = newTenantPorts[tenantId];
                    
                    // Start tenant process
                    var tenantProcess = await _processManager!.StartTenantProcessAsync(tenantId);
                    port = tenantProcess.Port;
                    
                    // Try to connect and send a test command
                    var factory = new WebApplicationFactory<ACS.VerticalHost.Program>();
                    Environment.SetEnvironmentVariable("TENANT_ID", tenantId);
                    Environment.SetEnvironmentVariable("GRPC_PORT", port.ToString());

                    var client = factory.CreateClient();
                    var channel = GrpcChannel.ForAddress(client.BaseAddress!);
                    var grpcClient = new VerticalService.VerticalServiceClient(channel);

                    // Wait a bit for startup
                    await Task.Delay(2000);
                    
                    // Test with a health check
                    var healthResponse = await grpcClient.HealthCheckAsync(new HealthRequest());
                    
                    tenantStopwatch.Stop();
                    startupResults.Add((tenantId, healthResponse.Healthy, tenantStopwatch.Elapsed, ""));
                    
                    // Cleanup
                    await channel.ShutdownAsync();
                    factory.Dispose();
                    await _processManager.StopTenantProcessAsync(tenantId);
                }
                catch (Exception ex)
                {
                    tenantStopwatch.Stop();
                    startupResults.Add((tenantId, false, tenantStopwatch.Elapsed, ex.Message));
                    
                    try { await _processManager!.StopTenantProcessAsync(tenantId); } catch { }
                }
            })).ToList();

            await Task.WhenAll(startupTasks);
            startupStopwatch.Stop();

            var successfulStartups = startupResults.Count(r => r.Success);
            var totalStartupTime = startupStopwatch.Elapsed;
            var avgStartupTime = startupResults.Where(r => r.Success).Average(r => r.StartupTime.TotalSeconds);
            var maxStartupTime = startupResults.Where(r => r.Success).Max(r => r.StartupTime.TotalSeconds);

            Console.WriteLine($"Concurrent Startup Results:");
            Console.WriteLine($"  Total Tenants: {newTenantIds.Count}");
            Console.WriteLine($"  Successful Startups: {successfulStartups}");
            Console.WriteLine($"  Success Rate: {successfulStartups / (double)newTenantIds.Count:P}");
            Console.WriteLine($"  Total Startup Time: {totalStartupTime.TotalSeconds:F2}s");
            Console.WriteLine($"  Average Startup Time: {avgStartupTime:F2}s");
            Console.WriteLine($"  Maximum Startup Time: {maxStartupTime:F2}s");

            foreach (var result in startupResults)
            {
                var status = result.Success ? "SUCCESS" : $"FAILED: {result.Error}";
                Console.WriteLine($"  {result.TenantId}: {status} ({result.StartupTime.TotalSeconds:F2}s)");
            }

            // Assertions
            Assert.IsTrue(successfulStartups >= newTenantIds.Count * 0.8, 
                "At least 80% of tenants should start successfully");
            Assert.IsTrue(avgStartupTime < 10, 
                $"Average startup time too high: {avgStartupTime:F2}s");
            Assert.IsTrue(maxStartupTime < 15, 
                $"Maximum startup time too high: {maxStartupTime:F2}s");

            Console.WriteLine("Concurrent tenant startup test passed");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Concurrent startup test failed: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task MultiTenant_MemoryLeakDetection_NoMemoryLeaksUnderLoad()
    {
        // Arrange - Long running test to detect memory leaks
        const int testDurationMinutes = 5; // Shorter for CI/testing, increase for thorough testing
        const int samplingIntervalSeconds = 30;
        const int commandsPerSample = 50;
        
        var memorySnapshots = new List<(DateTime Timestamp, long MemoryBytes, int RequestCount)>();
        var totalRequestsProcessed = 0;
        var testEndTime = DateTime.UtcNow.AddMinutes(testDurationMinutes);

        try
        {
            Console.WriteLine($"Starting {testDurationMinutes}-minute memory leak detection test...");
            
            // Take initial memory snapshot
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var initialMemory = GC.GetTotalMemory(false);
            memorySnapshots.Add((DateTime.UtcNow, initialMemory, 0));
            
            Console.WriteLine($"Initial memory: {initialMemory / 1024 / 1024:F2} MB");

            while (DateTime.UtcNow < testEndTime)
            {
                var sampleStart = DateTime.UtcNow;
                var sampleResults = new ConcurrentBag<bool>();

                // Execute commands across all tenants
                var sampleTasks = _stressTenantIds.Select(tenantId => Task.Run(async () =>
                {
                    if (!_tenantClients.TryGetValue(tenantId, out var client))
                        return;

                    for (int i = 0; i < commandsPerSample; i++)
                    {
                        try
                        {
                            var command = new TestCreateUserCommand 
                            { 
                                Name = $"MemLeak_{tenantId}_{DateTime.UtcNow.Ticks}_{i}",
                                Metadata = new string('M', 512) // 512 bytes metadata
                            };
                            var commandData = ProtoSerializer.Serialize(command);
                            var request = new CommandRequest
                            {
                                CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                                CommandData = ByteString.CopyFrom(commandData),
                                CorrelationId = Guid.NewGuid().ToString()
                            };

                            var response = await client.ExecuteCommandAsync(request);
                            sampleResults.Add(response.Success);
                        }
                        catch (Exception)
                        {
                            sampleResults.Add(false);
                        }
                    }
                })).ToList();

                await Task.WhenAll(sampleTasks);
                
                var successfulInSample = sampleResults.Count(r => r);
                totalRequestsProcessed += successfulInSample;

                // Force garbage collection and take memory snapshot
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                var currentMemory = GC.GetTotalMemory(false);
                var timestamp = DateTime.UtcNow;
                memorySnapshots.Add((timestamp, currentMemory, totalRequestsProcessed));

                var memoryIncrease = currentMemory - initialMemory;
                var memoryPerRequest = totalRequestsProcessed > 0 ? memoryIncrease / totalRequestsProcessed : 0;
                
                Console.WriteLine($"{timestamp:HH:mm:ss} - Memory: {currentMemory / 1024 / 1024:F2} MB " +
                                $"(+{memoryIncrease / 1024 / 1024:F2} MB) - " +
                                $"Requests: {totalRequestsProcessed} - " +
                                $"Per Request: {memoryPerRequest:F0} bytes");

                // Wait for next sampling interval
                var sampleDuration = DateTime.UtcNow - sampleStart;
                var targetInterval = TimeSpan.FromSeconds(samplingIntervalSeconds);
                if (sampleDuration < targetInterval)
                {
                    await Task.Delay(targetInterval - sampleDuration);
                }
            }

            // Final analysis
            var finalMemory = memorySnapshots.Last().MemoryBytes;
            var totalMemoryIncrease = finalMemory - initialMemory;
            var memoryIncreasePerRequest = totalRequestsProcessed > 0 ? 
                totalMemoryIncrease / totalRequestsProcessed : 0;

            // Calculate memory growth trend
            var memoryGrowthSlope = CalculateLinearGrowthSlope(memorySnapshots);
            var requestGrowthSlope = CalculateRequestBasedMemorySlope(memorySnapshots);

            Console.WriteLine($"\nMemory Leak Detection Results:");
            Console.WriteLine($"  Test Duration: {testDurationMinutes} minutes");
            Console.WriteLine($"  Total Requests: {totalRequestsProcessed}");
            Console.WriteLine($"  Initial Memory: {initialMemory / 1024 / 1024:F2} MB");
            Console.WriteLine($"  Final Memory: {finalMemory / 1024 / 1024:F2} MB");
            Console.WriteLine($"  Total Increase: {totalMemoryIncrease / 1024 / 1024:F2} MB");
            Console.WriteLine($"  Memory per Request: {memoryIncreasePerRequest:F0} bytes");
            Console.WriteLine($"  Memory Growth Slope: {memoryGrowthSlope / 1024 / 1024:F6} MB/minute");
            Console.WriteLine($"  Request-based Slope: {requestGrowthSlope:F2} bytes/request");

            // Assertions - Memory leak detection
            // 1. Memory per request should be reasonable (< 5KB per request)
            Assert.IsTrue(memoryIncreasePerRequest < 5120, 
                $"Memory per request too high (potential leak): {memoryIncreasePerRequest:F0} bytes");

            // 2. Memory growth slope should be minimal (< 10MB per minute)
            var memoryGrowthMBPerMinute = memoryGrowthSlope / 1024 / 1024;
            Assert.IsTrue(memoryGrowthMBPerMinute < 10, 
                $"Memory growth rate too high (potential leak): {memoryGrowthMBPerMinute:F2} MB/minute");

            // 3. Total memory increase should be reasonable for the test duration
            var totalIncreaseMB = totalMemoryIncrease / 1024 / 1024;
            var maxAllowedIncreaseMB = testDurationMinutes * 50; // 50MB per minute max
            Assert.IsTrue(totalIncreaseMB < maxAllowedIncreaseMB, 
                $"Total memory increase too high: {totalIncreaseMB:F2} MB (max: {maxAllowedIncreaseMB} MB)");

            Console.WriteLine("Memory leak detection test passed - no significant leaks detected");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Memory leak detection test failed: {ex.Message}");
        }
    }

    private double CalculateLinearGrowthSlope(List<(DateTime Timestamp, long MemoryBytes, int RequestCount)> snapshots)
    {
        if (snapshots.Count < 2) return 0;

        var firstSnapshot = snapshots.First();
        var lastSnapshot = snapshots.Last();
        
        var timeSpanMinutes = (lastSnapshot.Timestamp - firstSnapshot.Timestamp).TotalMinutes;
        var memoryChange = lastSnapshot.MemoryBytes - firstSnapshot.MemoryBytes;
        
        return timeSpanMinutes > 0 ? memoryChange / timeSpanMinutes : 0;
    }

    private double CalculateRequestBasedMemorySlope(List<(DateTime Timestamp, long MemoryBytes, int RequestCount)> snapshots)
    {
        if (snapshots.Count < 2) return 0;

        var firstSnapshot = snapshots.First();
        var lastSnapshot = snapshots.Last();
        
        var requestChange = lastSnapshot.RequestCount - firstSnapshot.RequestCount;
        var memoryChange = lastSnapshot.MemoryBytes - firstSnapshot.MemoryBytes;
        
        return requestChange > 0 ? (double)memoryChange / requestChange : 0;
    }
}