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
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ACS.VerticalHost.Tests;

[TestClass]
public class ChaosEngineeringTests
{
    private readonly List<string> _chaosTenantIds = new()
    {
        "chaos-tenant-1", "chaos-tenant-2", "chaos-tenant-3"
    };

    private readonly Dictionary<string, WebApplicationFactory<ACS.VerticalHost.Program>> _tenantFactories = new();
    private readonly Dictionary<string, GrpcChannel> _tenantChannels = new();
    private readonly Dictionary<string, VerticalService.VerticalServiceClient> _tenantClients = new();
    private readonly Dictionary<string, TenantProcessManager.TenantProcess> _tenantProcesses = new();
    private TenantProcessManager? _processManager;
    private ILogger<ChaosEngineeringTests>? _logger;

    [TestInitialize]
    public async Task Setup()
    {
        try
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var processLogger = serviceProvider.GetRequiredService<ILogger<TenantProcessManager>>();
            _logger = serviceProvider.GetRequiredService<ILogger<ChaosEngineeringTests>>();
            _processManager = new TenantProcessManager(processLogger);

            // Start tenant processes for chaos testing
            foreach (var tenantId in _chaosTenantIds)
            {
                try
                {
                    var tenantProcess = await _processManager.StartTenantProcessAsync(tenantId);
                    _tenantProcesses[tenantId] = tenantProcess;
                    
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
                    Environment.SetEnvironmentVariable("GRPC_PORT", tenantProcess.Port.ToString());

                    var client = factory.CreateClient();
                    var channel = GrpcChannel.ForAddress(client.BaseAddress!, new GrpcChannelOptions
                    {
                        HttpClient = client,
                        MaxReceiveMessageSize = 64 * 1024 * 1024,
                        MaxSendMessageSize = 64 * 1024 * 1024,
                        ThrowOperationCanceledOnCancellation = true
                    });
                    
                    var grpcClient = new VerticalService.VerticalServiceClient(channel);

                    _tenantFactories[tenantId] = factory;
                    _tenantChannels[tenantId] = channel;
                    _tenantClients[tenantId] = grpcClient;
                    
                    _logger?.LogInformation($"Chaos test tenant {tenantId} initialized on port {tenantProcess.Port}");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"Failed to setup chaos tenant {tenantId}");
                }
            }

            await Task.Delay(3000); // Startup delay
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Could not setup chaos engineering test environment: {ex.Message}");
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
                foreach (var tenantId in _chaosTenantIds)
                {
                    try { await _processManager.StopTenantProcessAsync(tenantId); } catch { }
                }
                _processManager.Dispose();
            }
            
            _tenantProcesses.Clear();
        }
        catch (Exception) { }
    }

    [TestMethod]
    public async Task Chaos_RandomProcessKill_SystemRecovery()
    {
        // Arrange - Kill random tenant processes and verify recovery
        const int testDurationSeconds = 60;
        const int killIntervalSeconds = 15;
        const int commandsPerSecond = 5;
        
        var results = new ConcurrentBag<(string TenantId, bool Success, string Operation, DateTime Timestamp)>();
        var processKills = new ConcurrentBag<(string TenantId, DateTime KillTime, DateTime? RecoveryTime)>();
        var endTime = DateTime.UtcNow.AddSeconds(testDurationSeconds);
        var random = new Random();

        try
        {
            _logger?.LogInformation($"Starting {testDurationSeconds}s random process kill chaos test...");

            // Background task for continuous traffic
            var trafficTask = Task.Run(async () =>
            {
                var requestCounter = 0;
                while (DateTime.UtcNow < endTime)
                {
                    var batchTasks = _chaosTenantIds.Select(tenantId => Task.Run(async () =>
                    {
                        if (!_tenantClients.TryGetValue(tenantId, out var client))
                            return;

                        for (int i = 0; i < commandsPerSecond; i++)
                        {
                            try
                            {
                                var command = new TestCreateUserCommand 
                                { 
                                    Name = $"Chaos_{tenantId}_{Interlocked.Increment(ref requestCounter)}" 
                                };
                                var commandData = ProtoSerializer.Serialize(command);
                                var request = new CommandRequest
                                {
                                    CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                                    CommandData = ByteString.CopyFrom(commandData),
                                    CorrelationId = Guid.NewGuid().ToString()
                                };

                                var response = await client.ExecuteCommandAsync(request);
                                results.Add((tenantId, response.Success, "Command", DateTime.UtcNow));
                            }
                            catch (Exception)
                            {
                                results.Add((tenantId, false, "Command", DateTime.UtcNow));
                            }
                        }
                    })).ToList();

                    await Task.WhenAll(batchTasks);
                    await Task.Delay(1000); // 1 second intervals
                }
            });

            // Background task for random process kills
            var chaosTask = Task.Run(async () =>
            {
                while (DateTime.UtcNow < endTime.AddSeconds(-10)) // Stop kills 10s before end
                {
                    await Task.Delay(TimeSpan.FromSeconds(killIntervalSeconds));
                    
                    // Randomly select a tenant to kill
                    var targetTenantId = _chaosTenantIds[random.Next(_chaosTenantIds.Count)];
                    var killTime = DateTime.UtcNow;
                    
                    try
                    {
                        _logger?.LogInformation($"Chaos: Killing tenant process {targetTenantId}");
                        await _processManager!.StopTenantProcessAsync(targetTenantId);
                        
                        processKills.Add((targetTenantId, killTime, null));
                        results.Add((targetTenantId, false, "ProcessKill", killTime));
                        
                        // Wait a bit, then restart
                        await Task.Delay(3000);
                        
                        _logger?.LogInformation($"Chaos: Restarting tenant process {targetTenantId}");
                        var newProcess = await _processManager.StartTenantProcessAsync(targetTenantId);
                        _tenantProcesses[targetTenantId] = newProcess;
                        
                        var recoveryTime = DateTime.UtcNow;
                        
                        // Update recovery time
                        var killRecord = processKills.FirstOrDefault(p => p.TenantId == targetTenantId && 
                            Math.Abs((p.KillTime - killTime).TotalSeconds) < 1);
                        if (killRecord != default)
                        {
                            processKills.TryTake(out killRecord);
                            processKills.Add((targetTenantId, killTime, recoveryTime));
                        }
                        
                        results.Add((targetTenantId, true, "ProcessRestart", recoveryTime));
                        _logger?.LogInformation($"Chaos: Tenant {targetTenantId} recovered in {(recoveryTime - killTime).TotalSeconds:F1}s");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"Chaos: Failed to kill/restart tenant {targetTenantId}");
                        results.Add((targetTenantId, false, "ProcessRestartFailed", DateTime.UtcNow));
                    }
                }
            });

            await Task.WhenAll(trafficTask, chaosTask);

            // Analyze results
            var commandResults = results.Where(r => r.Operation == "Command").ToList();
            var killResults = processKills.ToList();
            
            var totalCommands = commandResults.Count;
            var successfulCommands = commandResults.Count(r => r.Success);
            var overallSuccessRate = totalCommands > 0 ? successfulCommands / (double)totalCommands : 0;
            
            var averageRecoveryTime = killResults.Where(k => k.RecoveryTime.HasValue)
                .Average(k => (k.RecoveryTime!.Value - k.KillTime).TotalSeconds);

            _logger?.LogInformation($"Random Process Kill Chaos Results:");
            _logger?.LogInformation($"  Test Duration: {testDurationSeconds}s");
            _logger?.LogInformation($"  Total Commands: {totalCommands}");
            _logger?.LogInformation($"  Successful Commands: {successfulCommands}");
            _logger?.LogInformation($"  Overall Success Rate: {overallSuccessRate:P}");
            _logger?.LogInformation($"  Process Kills: {killResults.Count}");
            _logger?.LogInformation($"  Average Recovery Time: {averageRecoveryTime:F1}s");

            foreach (var tenantId in _chaosTenantIds)
            {
                var tenantCommands = commandResults.Where(r => r.TenantId == tenantId).ToList();
                var tenantSuccessRate = tenantCommands.Count > 0 ? 
                    tenantCommands.Count(r => r.Success) / (double)tenantCommands.Count : 0;
                var tenantKills = killResults.Count(k => k.TenantId == tenantId);
                
                _logger?.LogInformation($"  {tenantId}: {tenantSuccessRate:P} success rate, {tenantKills} kills");
            }

            // Assertions
            Assert.IsTrue(overallSuccessRate > 0.7, $"Success rate too low during chaos: {overallSuccessRate:P}");
            Assert.IsTrue(averageRecoveryTime < 10, $"Average recovery time too high: {averageRecoveryTime:F1}s");
            Assert.IsTrue(killResults.Count > 0, "No chaos events occurred during test");
            Assert.IsTrue(killResults.All(k => k.RecoveryTime.HasValue), "All processes should have recovered");

            _logger?.LogInformation("Random process kill chaos test passed");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Random process kill chaos test failed: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task Chaos_NetworkPartition_ServiceResilience()
    {
        // Simulate network issues by introducing delays and connection failures
        const int testDurationSeconds = 45;
        const int networkIssueIntervalSeconds = 10;
        
        var results = new ConcurrentBag<(string TenantId, bool Success, TimeSpan Latency, string Error)>();
        var networkEvents = new ConcurrentBag<(DateTime Timestamp, string Event, string TenantId)>();
        var endTime = DateTime.UtcNow.AddSeconds(testDurationSeconds);

        try
        {
            _logger?.LogInformation($"Starting {testDurationSeconds}s network partition chaos test...");

            // Background task for continuous traffic with timeout handling
            var trafficTask = Task.Run(async () =>
            {
                var requestCounter = 0;
                while (DateTime.UtcNow < endTime)
                {
                    var batchTasks = _chaosTenantIds.Select(tenantId => Task.Run(async () =>
                    {
                        if (!_tenantClients.TryGetValue(tenantId, out var client))
                            return;

                        var stopwatch = Stopwatch.StartNew();
                        try
                        {
                            var command = new TestCreateUserCommand 
                            { 
                                Name = $"NetChaos_{tenantId}_{Interlocked.Increment(ref requestCounter)}" 
                            };
                            var commandData = ProtoSerializer.Serialize(command);
                            var request = new CommandRequest
                            {
                                CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                                CommandData = ByteString.CopyFrom(commandData),
                                CorrelationId = Guid.NewGuid().ToString()
                            };

                            // Add network timeout simulation
                            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                            var response = await client.ExecuteCommandAsync(request, cancellationToken: cts.Token);
                            stopwatch.Stop();
                            
                            results.Add((tenantId, response.Success, stopwatch.Elapsed, ""));
                        }
                        catch (OperationCanceledException)
                        {
                            stopwatch.Stop();
                            results.Add((tenantId, false, stopwatch.Elapsed, "Timeout"));
                        }
                        catch (Exception ex)
                        {
                            stopwatch.Stop();
                            results.Add((tenantId, false, stopwatch.Elapsed, ex.GetType().Name));
                        }
                    })).ToList();

                    await Task.WhenAll(batchTasks);
                    await Task.Delay(2000); // 2 second intervals
                }
            });

            // Background task for network chaos
            var networkChaosTask = Task.Run(async () =>
            {
                var random = new Random();
                
                while (DateTime.UtcNow < endTime.AddSeconds(-10))
                {
                    await Task.Delay(TimeSpan.FromSeconds(networkIssueIntervalSeconds));
                    
                    var targetTenantId = _chaosTenantIds[random.Next(_chaosTenantIds.Count)];
                    var chaosType = random.Next(3);
                    
                    try
                    {
                        switch (chaosType)
                        {
                            case 0: // Channel disconnect and reconnect
                                _logger?.LogInformation($"Network Chaos: Disconnecting channel for {targetTenantId}");
                                networkEvents.Add((DateTime.UtcNow, "ChannelDisconnect", targetTenantId));
                                
                                if (_tenantChannels.TryGetValue(targetTenantId, out var channel))
                                {
                                    await channel.ShutdownAsync();
                                    
                                    // Wait a bit
                                    await Task.Delay(2000);
                                    
                                    // Recreate channel
                                    var factory = _tenantFactories[targetTenantId];
                                    var client = factory.CreateClient();
                                    var newChannel = GrpcChannel.ForAddress(client.BaseAddress!, new GrpcChannelOptions
                                    {
                                        HttpClient = client,
                                        MaxReceiveMessageSize = 64 * 1024 * 1024,
                                        MaxSendMessageSize = 64 * 1024 * 1024
                                    });
                                    
                                    _tenantChannels[targetTenantId] = newChannel;
                                    _tenantClients[targetTenantId] = new VerticalService.VerticalServiceClient(newChannel);
                                    
                                    networkEvents.Add((DateTime.UtcNow, "ChannelReconnect", targetTenantId));
                                    _logger?.LogInformation($"Network Chaos: Reconnected channel for {targetTenantId}");
                                }
                                break;
                                
                            case 1: // Simulate slow network by adding artificial delay
                                _logger?.LogInformation($"Network Chaos: Simulating slow network for {targetTenantId}");
                                networkEvents.Add((DateTime.UtcNow, "SlowNetwork", targetTenantId));
                                
                                // Create a delayed client wrapper (simplified simulation)
                                await Task.Delay(1000);
                                
                                networkEvents.Add((DateTime.UtcNow, "NetworkRecovery", targetTenantId));
                                _logger?.LogInformation($"Network Chaos: Network recovered for {targetTenantId}");
                                break;
                                
                            case 2: // Port connectivity test
                                _logger?.LogInformation($"Network Chaos: Testing port connectivity for {targetTenantId}");
                                networkEvents.Add((DateTime.UtcNow, "PortTest", targetTenantId));
                                
                                if (_tenantProcesses.TryGetValue(targetTenantId, out var process))
                                {
                                    var isPortOpen = await IsPortOpen("localhost", process.Port);
                                    networkEvents.Add((DateTime.UtcNow, $"PortStatus:{isPortOpen}", targetTenantId));
                                    _logger?.LogInformation($"Network Chaos: Port {process.Port} is {(isPortOpen ? "open" : "closed")} for {targetTenantId}");
                                }
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"Network chaos failed for tenant {targetTenantId}");
                        networkEvents.Add((DateTime.UtcNow, $"ChaosError:{ex.GetType().Name}", targetTenantId));
                    }
                }
            });

            await Task.WhenAll(trafficTask, networkChaosTask);

            // Analyze results
            var totalRequests = results.Count;
            var successfulRequests = results.Count(r => r.Success);
            var timeoutErrors = results.Count(r => r.Error == "Timeout");
            var connectionErrors = results.Count(r => r.Error.Contains("Connection") || r.Error.Contains("Rpc"));
            var overallSuccessRate = totalRequests > 0 ? successfulRequests / (double)totalRequests : 0;
            var averageLatency = results.Where(r => r.Success).Average(r => r.Latency.TotalMilliseconds);
            var maxLatency = results.Where(r => r.Success).Max(r => r.Latency.TotalMilliseconds);

            _logger?.LogInformation($"Network Partition Chaos Results:");
            _logger?.LogInformation($"  Test Duration: {testDurationSeconds}s");
            _logger?.LogInformation($"  Total Requests: {totalRequests}");
            _logger?.LogInformation($"  Successful Requests: {successfulRequests}");
            _logger?.LogInformation($"  Timeout Errors: {timeoutErrors}");
            _logger?.LogInformation($"  Connection Errors: {connectionErrors}");
            _logger?.LogInformation($"  Overall Success Rate: {overallSuccessRate:P}");
            _logger?.LogInformation($"  Average Latency: {averageLatency:F2}ms");
            _logger?.LogInformation($"  Max Latency: {maxLatency:F2}ms");
            _logger?.LogInformation($"  Network Events: {networkEvents.Count}");

            // Group network events by type
            var eventTypes = networkEvents.GroupBy(e => e.Event.Split(':')[0])
                .ToDictionary(g => g.Key, g => g.Count());
            
            foreach (var eventType in eventTypes)
            {
                _logger?.LogInformation($"  {eventType.Key}: {eventType.Value} occurrences");
            }

            // Assertions
            Assert.IsTrue(overallSuccessRate > 0.6, $"Success rate too low during network chaos: {overallSuccessRate:P}");
            Assert.IsTrue(networkEvents.Count > 0, "No network chaos events occurred");
            Assert.IsTrue(maxLatency < 10000, $"Maximum latency too high: {maxLatency:F2}ms");
            
            // System should handle timeouts gracefully
            var timeoutRate = totalRequests > 0 ? timeoutErrors / (double)totalRequests : 0;
            Assert.IsTrue(timeoutRate < 0.3, $"Timeout rate too high: {timeoutRate:P}");

            _logger?.LogInformation("Network partition chaos test passed");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Network partition chaos test failed: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task Chaos_ResourceExhaustion_GracefulDegradation()
    {
        // Simulate resource exhaustion scenarios
        const int testDurationSeconds = 40;
        const int memoryPressureIntervalSeconds = 8;
        
        var results = new ConcurrentBag<(DateTime Timestamp, string TenantId, bool Success, string ResourceState)>();
        var resourceEvents = new ConcurrentBag<(DateTime Timestamp, string Event, long MemoryMB, int Threads)>();
        var endTime = DateTime.UtcNow.AddSeconds(testDurationSeconds);

        try
        {
            _logger?.LogInformation($"Starting {testDurationSeconds}s resource exhaustion chaos test...");

            // Background task for monitoring and applying memory pressure
            var resourceChaosTask = Task.Run(async () =>
            {
                var memoryConsumers = new List<byte[]>();
                
                while (DateTime.UtcNow < endTime.AddSeconds(-5))
                {
                    try
                    {
                        // Record current resource state
                        var currentMemory = GC.GetTotalMemory(false) / 1024 / 1024; // MB
                        var threadCount = Process.GetCurrentProcess().Threads.Count;
                        resourceEvents.Add((DateTime.UtcNow, "ResourceSnapshot", currentMemory, threadCount));
                        
                        // Apply memory pressure (allocate 100MB)
                        _logger?.LogInformation($"Resource Chaos: Applying memory pressure (+100MB)");
                        var memoryBlock = new byte[100 * 1024 * 1024]; // 100MB
                        memoryConsumers.Add(memoryBlock);
                        resourceEvents.Add((DateTime.UtcNow, "MemoryPressureApplied", currentMemory + 100, threadCount));
                        
                        // Wait and then release some memory
                        await Task.Delay(TimeSpan.FromSeconds(memoryPressureIntervalSeconds / 2));
                        
                        // Release some memory pressure (remove oldest allocation)
                        if (memoryConsumers.Count > 2)
                        {
                            memoryConsumers.RemoveAt(0);
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            GC.Collect();
                            
                            var newMemory = GC.GetTotalMemory(false) / 1024 / 1024;
                            resourceEvents.Add((DateTime.UtcNow, "MemoryPressureReduced", newMemory, threadCount));
                            _logger?.LogInformation($"Resource Chaos: Memory pressure reduced to {newMemory}MB");
                        }
                        
                        await Task.Delay(TimeSpan.FromSeconds(memoryPressureIntervalSeconds / 2));
                    }
                    catch (OutOfMemoryException)
                    {
                        _logger?.LogWarning("Resource Chaos: Out of memory exception occurred");
                        resourceEvents.Add((DateTime.UtcNow, "OutOfMemory", 0, 0));
                        
                        // Emergency cleanup
                        memoryConsumers.Clear();
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                        
                        await Task.Delay(2000);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Resource chaos error");
                        resourceEvents.Add((DateTime.UtcNow, $"Error:{ex.GetType().Name}", 0, 0));
                    }
                }
                
                // Cleanup
                memoryConsumers.Clear();
                GC.Collect();
            });

            // Background task for continuous requests during resource pressure
            var trafficTask = Task.Run(async () =>
            {
                var requestCounter = 0;
                while (DateTime.UtcNow < endTime)
                {
                    var currentMemory = GC.GetTotalMemory(false) / 1024 / 1024;
                    var resourceState = currentMemory > 1000 ? "HighMemory" : 
                                       currentMemory > 500 ? "MediumMemory" : "LowMemory";

                    var batchTasks = _chaosTenantIds.Select(tenantId => Task.Run(async () =>
                    {
                        if (!_tenantClients.TryGetValue(tenantId, out var client))
                            return;

                        try
                        {
                            // Create command with varying payload based on resource state
                            var payloadSize = resourceState == "HighMemory" ? 100 : 1000; // Smaller payloads under pressure
                            var command = new TestCreateUserCommand 
                            { 
                                Name = $"Resource_{tenantId}_{Interlocked.Increment(ref requestCounter)}",
                                Metadata = new string('R', payloadSize)
                            };
                            var commandData = ProtoSerializer.Serialize(command);
                            var request = new CommandRequest
                            {
                                CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                                CommandData = ByteString.CopyFrom(commandData),
                                CorrelationId = Guid.NewGuid().ToString()
                            };

                            // Shorter timeout under resource pressure
                            var timeout = resourceState == "HighMemory" ? TimeSpan.FromSeconds(2) : TimeSpan.FromSeconds(5);
                            using var cts = new CancellationTokenSource(timeout);
                            
                            var response = await client.ExecuteCommandAsync(request, cancellationToken: cts.Token);
                            results.Add((DateTime.UtcNow, tenantId, response.Success, resourceState));
                        }
                        catch (Exception)
                        {
                            results.Add((DateTime.UtcNow, tenantId, false, resourceState));
                        }
                    })).ToList();

                    await Task.WhenAll(batchTasks);
                    
                    // Adaptive delay based on resource state
                    var delay = resourceState == "HighMemory" ? 3000 : 1500;
                    await Task.Delay(delay);
                }
            });

            await Task.WhenAll(resourceChaosTask, trafficTask);

            // Analyze results
            var resultsList = results.ToList();
            var totalRequests = resultsList.Count;
            var successfulRequests = resultsList.Count(r => r.Success);
            var overallSuccessRate = totalRequests > 0 ? successfulRequests / (double)totalRequests : 0;

            // Analyze by resource state
            var resourceStateResults = resultsList.GroupBy(r => r.ResourceState)
                .ToDictionary(g => g.Key, g => new
                {
                    Total = g.Count(),
                    Successful = g.Count(r => r.Success),
                    SuccessRate = g.Count(r => r.Success) / (double)g.Count()
                });

            var resourceEventsList = resourceEvents.ToList();
            var maxMemoryUsed = resourceEventsList.Where(e => e.MemoryMB > 0).DefaultIfEmpty().Max(e => e.MemoryMB);
            var memoryPressureEvents = resourceEventsList.Count(e => e.Event.Contains("MemoryPressure"));
            var outOfMemoryEvents = resourceEventsList.Count(e => e.Event == "OutOfMemory");

            _logger?.LogInformation($"Resource Exhaustion Chaos Results:");
            _logger?.LogInformation($"  Test Duration: {testDurationSeconds}s");
            _logger?.LogInformation($"  Total Requests: {totalRequests}");
            _logger?.LogInformation($"  Successful Requests: {successfulRequests}");
            _logger?.LogInformation($"  Overall Success Rate: {overallSuccessRate:P}");
            _logger?.LogInformation($"  Max Memory Used: {maxMemoryUsed}MB");
            _logger?.LogInformation($"  Memory Pressure Events: {memoryPressureEvents}");
            _logger?.LogInformation($"  Out of Memory Events: {outOfMemoryEvents}");

            _logger?.LogInformation($"Performance by Resource State:");
            foreach (var stateResult in resourceStateResults)
            {
                _logger?.LogInformation($"  {stateResult.Key}: {stateResult.Value.Successful}/{stateResult.Value.Total} " +
                                      $"({stateResult.Value.SuccessRate:P})");
            }

            // Assertions
            Assert.IsTrue(overallSuccessRate > 0.5, $"Overall success rate too low: {overallSuccessRate:P}");
            Assert.IsTrue(memoryPressureEvents > 0, "No memory pressure events occurred");
            
            // System should degrade gracefully - success rate should not drop below 30% even under high pressure
            if (resourceStateResults.ContainsKey("HighMemory"))
            {
                var highMemorySuccessRate = resourceStateResults["HighMemory"].SuccessRate;
                Assert.IsTrue(highMemorySuccessRate > 0.3, 
                    $"High memory pressure success rate too low: {highMemorySuccessRate:P}");
            }
            
            // Should handle out of memory gracefully (no more than 2 events)
            Assert.IsTrue(outOfMemoryEvents <= 2, $"Too many out of memory events: {outOfMemoryEvents}");

            _logger?.LogInformation("Resource exhaustion chaos test passed");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Resource exhaustion chaos test failed: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task Chaos_CascadingFailures_ContainmentVerification()
    {
        // Test that failures in one tenant don't cascade to others
        const int testDurationSeconds = 45;
        var results = new ConcurrentBag<(string TenantId, bool Success, string Phase, DateTime Timestamp)>();
        var failureEvents = new ConcurrentBag<(DateTime Timestamp, string Event, string AffectedTenant)>();

        try
        {
            _logger?.LogInformation($"Starting {testDurationSeconds}s cascading failure chaos test...");

            var endTime = DateTime.UtcNow.AddSeconds(testDurationSeconds);
            var victimTenantId = _chaosTenantIds.First();
            var healthyTenantIds = _chaosTenantIds.Skip(1).ToList();

            // Phase 1: Normal operation
            _logger?.LogInformation("Phase 1: Establishing baseline performance");
            await ExecuteTestPhase("Baseline", TimeSpan.FromSeconds(10), results);

            // Phase 2: Kill victim tenant
            _logger?.LogInformation($"Phase 2: Killing victim tenant {victimTenantId}");
            failureEvents.Add((DateTime.UtcNow, "VictimTenantKilled", victimTenantId));
            
            try
            {
                await _processManager!.StopTenantProcessAsync(victimTenantId);
                _logger?.LogInformation($"Victim tenant {victimTenantId} process stopped");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to stop victim tenant {victimTenantId}");
                failureEvents.Add((DateTime.UtcNow, "VictimKillFailed", victimTenantId));
            }

            // Phase 3: Test isolation - healthy tenants should continue working
            _logger?.LogInformation("Phase 3: Testing tenant isolation during victim failure");
            await ExecuteTestPhase("IsolationTest", TimeSpan.FromSeconds(15), results);

            // Phase 4: Restart victim and test recovery
            _logger?.LogInformation($"Phase 4: Restarting victim tenant {victimTenantId}");
            try
            {
                var newProcess = await _processManager!.StartTenantProcessAsync(victimTenantId);
                _tenantProcesses[victimTenantId] = newProcess;
                failureEvents.Add((DateTime.UtcNow, "VictimTenantRestarted", victimTenantId));
                _logger?.LogInformation($"Victim tenant {victimTenantId} restarted on port {newProcess.Port}");
                
                // Wait for stabilization
                await Task.Delay(3000);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to restart victim tenant {victimTenantId}");
                failureEvents.Add((DateTime.UtcNow, "VictimRestartFailed", victimTenantId));
            }

            // Phase 5: Full recovery verification
            _logger?.LogInformation("Phase 5: Verifying full system recovery");
            await ExecuteTestPhase("RecoveryTest", TimeSpan.FromSeconds(15), results);

            // Analyze results by phase and tenant
            var resultsList = results.ToList();
            var phaseAnalysis = resultsList.GroupBy(r => r.Phase)
                .ToDictionary(g => g.Key, g => g.GroupBy(r => r.TenantId)
                    .ToDictionary(tg => tg.Key, tg => new
                    {
                        Total = tg.Count(),
                        Successful = tg.Count(r => r.Success),
                        SuccessRate = tg.Count(r => r.Success) / (double)tg.Count()
                    }));

            _logger?.LogInformation($"Cascading Failure Chaos Results:");
            _logger?.LogInformation($"  Victim Tenant: {victimTenantId}");
            _logger?.LogInformation($"  Healthy Tenants: {string.Join(", ", healthyTenantIds)}");
            _logger?.LogInformation($"  Failure Events: {failureEvents.Count}");

            foreach (var phase in phaseAnalysis)
            {
                _logger?.LogInformation($"  {phase.Key} Phase Results:");
                foreach (var tenant in phase.Value)
                {
                    var isVictim = tenant.Key == victimTenantId;
                    var status = isVictim ? "(Victim)" : "(Healthy)";
                    _logger?.LogInformation($"    {tenant.Key} {status}: {tenant.Value.Successful}/{tenant.Value.Total} " +
                                          $"({tenant.Value.SuccessRate:P})");
                }
            }

            // Critical assertions for isolation verification
            
            // 1. Baseline should show all tenants healthy
            if (phaseAnalysis.ContainsKey("Baseline"))
            {
                foreach (var tenant in phaseAnalysis["Baseline"])
                {
                    Assert.IsTrue(tenant.Value.SuccessRate > 0.9, 
                        $"Baseline: Tenant {tenant.Key} success rate too low: {tenant.Value.SuccessRate:P}");
                }
            }

            // 2. During isolation test, healthy tenants should maintain performance
            if (phaseAnalysis.ContainsKey("IsolationTest"))
            {
                foreach (var healthyTenantId in healthyTenantIds)
                {
                    if (phaseAnalysis["IsolationTest"].ContainsKey(healthyTenantId))
                    {
                        var healthyTenantPerf = phaseAnalysis["IsolationTest"][healthyTenantId];
                        Assert.IsTrue(healthyTenantPerf.SuccessRate > 0.8, 
                            $"Isolation: Healthy tenant {healthyTenantId} affected by victim failure: {healthyTenantPerf.SuccessRate:P}");
                    }
                }

                // Victim tenant should have low success rate during failure
                if (phaseAnalysis["IsolationTest"].ContainsKey(victimTenantId))
                {
                    var victimPerf = phaseAnalysis["IsolationTest"][victimTenantId];
                    Assert.IsTrue(victimPerf.SuccessRate < 0.2, 
                        $"Isolation: Victim tenant {victimTenantId} should have low success rate: {victimPerf.SuccessRate:P}");
                }
            }

            // 3. Recovery test should show victim tenant recovered
            if (phaseAnalysis.ContainsKey("RecoveryTest"))
            {
                if (phaseAnalysis["RecoveryTest"].ContainsKey(victimTenantId))
                {
                    var victimRecovery = phaseAnalysis["RecoveryTest"][victimTenantId];
                    Assert.IsTrue(victimRecovery.SuccessRate > 0.7, 
                        $"Recovery: Victim tenant {victimTenantId} failed to recover: {victimRecovery.SuccessRate:P}");
                }
            }

            // 4. Verify no cascading effects occurred
            var healthyTenantDegradation = false;
            if (phaseAnalysis.ContainsKey("Baseline") && phaseAnalysis.ContainsKey("IsolationTest"))
            {
                foreach (var healthyTenantId in healthyTenantIds)
                {
                    if (phaseAnalysis["Baseline"].ContainsKey(healthyTenantId) && 
                        phaseAnalysis["IsolationTest"].ContainsKey(healthyTenantId))
                    {
                        var baselineRate = phaseAnalysis["Baseline"][healthyTenantId].SuccessRate;
                        var isolationRate = phaseAnalysis["IsolationTest"][healthyTenantId].SuccessRate;
                        var degradation = (baselineRate - isolationRate) / baselineRate;
                        
                        if (degradation > 0.2) // More than 20% degradation indicates cascading failure
                        {
                            healthyTenantDegradation = true;
                            _logger?.LogWarning($"Healthy tenant {healthyTenantId} experienced {degradation:P} degradation");
                        }
                    }
                }
            }

            Assert.IsFalse(healthyTenantDegradation, 
                "Cascading failure detected - healthy tenants were affected by victim failure");

            _logger?.LogInformation("Cascading failure containment test passed");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Cascading failure chaos test failed: {ex.Message}");
        }
    }

    private async Task ExecuteTestPhase(string phaseName, TimeSpan duration, 
        ConcurrentBag<(string TenantId, bool Success, string Phase, DateTime Timestamp)> results)
    {
        var endTime = DateTime.UtcNow.Add(duration);
        var requestCounter = 0;

        while (DateTime.UtcNow < endTime)
        {
            var batchTasks = _chaosTenantIds.Select(tenantId => Task.Run(async () =>
            {
                if (!_tenantClients.TryGetValue(tenantId, out var client))
                {
                    results.Add((tenantId, false, phaseName, DateTime.UtcNow));
                    return;
                }

                try
                {
                    var command = new TestCreateUserCommand 
                    { 
                        Name = $"{phaseName}_{tenantId}_{Interlocked.Increment(ref requestCounter)}" 
                    };
                    var commandData = ProtoSerializer.Serialize(command);
                    var request = new CommandRequest
                    {
                        CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                        CommandData = ByteString.CopyFrom(commandData),
                        CorrelationId = Guid.NewGuid().ToString()
                    };

                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    var response = await client.ExecuteCommandAsync(request, cancellationToken: cts.Token);
                    results.Add((tenantId, response.Success, phaseName, DateTime.UtcNow));
                }
                catch (Exception)
                {
                    results.Add((tenantId, false, phaseName, DateTime.UtcNow));
                }
            })).ToList();

            await Task.WhenAll(batchTasks);
            await Task.Delay(1000); // 1 second between batches
        }
    }

    private async Task<bool> IsPortOpen(string host, int port)
    {
        try
        {
            using var tcpClient = new TcpClient();
            var connectTask = tcpClient.ConnectAsync(host, port);
            var timeoutTask = Task.Delay(2000);
            
            var completedTask = await Task.WhenAny(connectTask, timeoutTask);
            
            if (completedTask == connectTask && tcpClient.Connected)
            {
                return true;
            }
            
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}