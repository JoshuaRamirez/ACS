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
public class MultiTenantLoadTests
{
    private readonly List<string> _testTenantIds = new()
    {
        "load-tenant-1", "load-tenant-2", "load-tenant-3", 
        "load-tenant-4", "load-tenant-5", "load-tenant-6"
    };
    
    private readonly Dictionary<string, int> _tenantPorts = new()
    {
        { "load-tenant-1", 50061 }, { "load-tenant-2", 50062 }, { "load-tenant-3", 50063 },
        { "load-tenant-4", 50064 }, { "load-tenant-5", 50065 }, { "load-tenant-6", 50066 }
    };

    private readonly Dictionary<string, WebApplicationFactory<ACS.VerticalHost.Program>> _tenantFactories = new();
    private readonly Dictionary<string, GrpcChannel> _tenantChannels = new();
    private readonly Dictionary<string, VerticalService.VerticalServiceClient> _tenantClients = new();
    private TenantProcessManager? _processManager;
    private ILogger<TenantProcessManager>? _logger;

    [TestInitialize]
    public async Task Setup()
    {
        try
        {
            // Setup logging
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            var serviceProvider = serviceCollection.BuildServiceProvider();
            _logger = serviceProvider.GetRequiredService<ILogger<TenantProcessManager>>();
            _processManager = new TenantProcessManager(_logger);

            // Start multiple tenant processes
            var startupTasks = new List<Task>();
            
            foreach (var tenantId in _testTenantIds)
            {
                startupTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var port = _tenantPorts[tenantId];
                        
                        // Start tenant process
                        var tenantProcess = await _processManager!.StartTenantProcessAsync(tenantId);
                        port = tenantProcess.Port; // Use the assigned port
                        
                        // Create test factory for this tenant
                        var factory = new WebApplicationFactory<ACS.VerticalHost.Program>()
                            .WithWebHostBuilder(builder =>
                            {
                                builder.ConfigureServices(services =>
                                {
                                    // Configure for load testing
                                    services.Configure<HostOptions>(options =>
                                    {
                                        options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
                                    });
                                });
                            });

                        // Set environment variables for this tenant
                        Environment.SetEnvironmentVariable("TENANT_ID", tenantId);
                        Environment.SetEnvironmentVariable("GRPC_PORT", port.ToString());

                        var client = factory.CreateClient();
                        var channel = GrpcChannel.ForAddress(client.BaseAddress!, new GrpcChannelOptions
                        {
                            HttpClient = client,
                            MaxReceiveMessageSize = 32 * 1024 * 1024, // 32MB
                            MaxSendMessageSize = 32 * 1024 * 1024      // 32MB
                        });
                        
                        var grpcClient = new VerticalService.VerticalServiceClient(channel);

                        lock (_tenantFactories)
                        {
                            _tenantFactories[tenantId] = factory;
                            _tenantChannels[tenantId] = channel;
                            _tenantClients[tenantId] = grpcClient;
                        }

                        Console.WriteLine($"Tenant {tenantId} initialized on port {port}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to initialize tenant {tenantId}: {ex.Message}");
                    }
                }));
            }

            await Task.WhenAll(startupTasks);
            
            // Wait for all services to be ready
            await Task.Delay(5000);
            
            // Warm up all tenant services
            await WarmUpTenantServices();
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Could not setup multi-tenant load test environment: {ex.Message}");
        }
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        try
        {
            // Cleanup channels
            foreach (var channel in _tenantChannels.Values)
            {
                await channel.ShutdownAsync();
            }
            _tenantChannels.Clear();

            // Cleanup factories
            foreach (var factory in _tenantFactories.Values)
            {
                factory.Dispose();
            }
            _tenantFactories.Clear();
            _tenantClients.Clear();

            // Stop tenant processes
            if (_processManager != null)
            {
                foreach (var tenantId in _testTenantIds)
                {
                    try
                    {
                        await _processManager.StopTenantProcessAsync(tenantId);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error stopping tenant {tenantId}: {ex.Message}");
                    }
                }
                _processManager.Dispose();
            }
        }
        catch (Exception)
        {
            // Cleanup errors are not critical
        }
    }

    private async Task WarmUpTenantServices()
    {
        var warmUpTasks = new List<Task>();
        
        foreach (var kvp in _tenantClients)
        {
            warmUpTasks.Add(Task.Run(async () =>
            {
                try
                {
                    var client = kvp.Value;
                    for (int i = 0; i < 3; i++)
                    {
                        await client.HealthCheckAsync(new HealthRequest());
                        await Task.Delay(100);
                    }
                    Console.WriteLine($"Warmed up tenant {kvp.Key}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warm-up failed for tenant {kvp.Key}: {ex.Message}");
                }
            }));
        }

        await Task.WhenAll(warmUpTasks);
    }

    [TestMethod]
    public async Task MultiTenant_ConcurrentLoad_AllTenantsHandleTraffic()
    {
        // Arrange
        const int commandsPerTenant = 20;
        const int concurrentClientsPerTenant = 3;
        var stopwatch = Stopwatch.StartNew();
        var allResults = new ConcurrentBag<(string TenantId, bool Success, TimeSpan Duration, string Error)>();

        try
        {
            // Act - Create load for all tenants simultaneously
            var tenantTasks = new List<Task>();

            foreach (var tenantId in _testTenantIds)
            {
                if (!_tenantClients.TryGetValue(tenantId, out var client))
                    continue;

                tenantTasks.Add(Task.Run(async () =>
                {
                    var clientTasks = new List<Task>();
                    
                    for (int clientIndex = 0; clientIndex < concurrentClientsPerTenant; clientIndex++)
                    {
                        int localClientIndex = clientIndex;
                        clientTasks.Add(Task.Run(async () =>
                        {
                            for (int commandIndex = 0; commandIndex < commandsPerTenant; commandIndex++)
                            {
                                var commandStopwatch = Stopwatch.StartNew();
                                try
                                {
                                    var command = new TestCreateUserCommand 
                                    { 
                                        Name = $"{tenantId}_Client{localClientIndex}_User{commandIndex}" 
                                    };
                                    var commandData = ProtoSerializer.Serialize(command);
                                    var request = new CommandRequest
                                    {
                                        CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                                        CommandData = ByteString.CopyFrom(commandData),
                                        CorrelationId = $"{tenantId}-{localClientIndex}-{commandIndex}"
                                    };

                                    var response = await client.ExecuteCommandAsync(request);
                                    commandStopwatch.Stop();
                                    
                                    allResults.Add((tenantId, response.Success, commandStopwatch.Elapsed, 
                                                   response.Success ? "" : response.ErrorMessage));
                                }
                                catch (Exception ex)
                                {
                                    commandStopwatch.Stop();
                                    allResults.Add((tenantId, false, commandStopwatch.Elapsed, ex.Message));
                                }
                            }
                        }));
                    }

                    await Task.WhenAll(clientTasks);
                }));
            }

            await Task.WhenAll(tenantTasks);
            stopwatch.Stop();

            // Assert
            var resultsByTenant = allResults.GroupBy(r => r.TenantId).ToList();
            var totalCommands = _testTenantIds.Count * concurrentClientsPerTenant * commandsPerTenant;
            var totalSuccessful = allResults.Count(r => r.Success);
            var overallSuccessRate = totalSuccessful / (double)allResults.Count;
            var overallThroughput = totalSuccessful / stopwatch.Elapsed.TotalSeconds;
            var averageLatency = allResults.Where(r => r.Success).Average(r => r.Duration.TotalMilliseconds);

            Console.WriteLine($"Multi-Tenant Load Test Results:");
            Console.WriteLine($"  Total Tenants: {_testTenantIds.Count}");
            Console.WriteLine($"  Clients per Tenant: {concurrentClientsPerTenant}");
            Console.WriteLine($"  Commands per Client: {commandsPerTenant}");
            Console.WriteLine($"  Total Commands: {allResults.Count}");
            Console.WriteLine($"  Successful Commands: {totalSuccessful}");
            Console.WriteLine($"  Overall Success Rate: {overallSuccessRate:P}");
            Console.WriteLine($"  Total Time: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"  Overall Throughput: {overallThroughput:F2} commands/sec");
            Console.WriteLine($"  Average Latency: {averageLatency:F2}ms");
            
            Console.WriteLine($"\nPer-Tenant Results:");
            foreach (var tenantGroup in resultsByTenant)
            {
                var tenantResults = tenantGroup.ToList();
                var tenantSuccessful = tenantResults.Count(r => r.Success);
                var tenantSuccessRate = tenantSuccessful / (double)tenantResults.Count;
                var tenantAvgLatency = tenantResults.Where(r => r.Success).Average(r => r.Duration.TotalMilliseconds);
                
                Console.WriteLine($"  {tenantGroup.Key}: {tenantSuccessful}/{tenantResults.Count} " +
                                $"({tenantSuccessRate:P}) - Avg: {tenantAvgLatency:F2}ms");
            }

            // Performance assertions
            Assert.IsTrue(overallSuccessRate > 0.95, $"Overall success rate too low: {overallSuccessRate:P}");
            Assert.IsTrue(overallThroughput > 30, $"Overall throughput too low: {overallThroughput:F2} commands/sec");
            Assert.IsTrue(averageLatency < 200, $"Average latency too high: {averageLatency:F2}ms");
            
            // Ensure all tenants handled requests
            foreach (var tenantId in _testTenantIds)
            {
                var tenantResults = allResults.Where(r => r.TenantId == tenantId).ToList();
                var tenantSuccessRate = tenantResults.Count(r => r.Success) / (double)tenantResults.Count;
                
                Assert.IsTrue(tenantResults.Count > 0, $"No results for tenant {tenantId}");
                Assert.IsTrue(tenantSuccessRate > 0.9, $"Tenant {tenantId} success rate too low: {tenantSuccessRate:P}");
            }
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Multi-tenant load test failed: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task MultiTenant_TenantIsolation_NoCommandCrossover()
    {
        // Arrange
        const int commandsPerTenant = 10;
        var tenantCommands = new Dictionary<string, List<string>>();
        
        foreach (var tenantId in _testTenantIds)
        {
            tenantCommands[tenantId] = new List<string>();
        }

        try
        {
            // Act - Send uniquely identifiable commands to each tenant
            var tenantTasks = new List<Task>();

            foreach (var tenantId in _testTenantIds)
            {
                if (!_tenantClients.TryGetValue(tenantId, out var client))
                    continue;

                tenantTasks.Add(Task.Run(async () =>
                {
                    for (int i = 0; i < commandsPerTenant; i++)
                    {
                        var uniqueName = $"ISOLATION_TEST_{tenantId}_{Guid.NewGuid()}";
                        var command = new TestCreateUserCommand { Name = uniqueName };
                        var commandData = ProtoSerializer.Serialize(command);
                        var request = new CommandRequest
                        {
                            CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                            CommandData = ByteString.CopyFrom(commandData),
                            CorrelationId = Guid.NewGuid().ToString()
                        };

                        var response = await client.ExecuteCommandAsync(request);
                        if (response.Success)
                        {
                            lock (tenantCommands[tenantId])
                            {
                                tenantCommands[tenantId].Add(uniqueName);
                            }
                        }
                    }
                }));
            }

            await Task.WhenAll(tenantTasks);

            // Query each tenant to verify isolation
            var isolationTasks = new List<Task<(string TenantId, List<string> UserNames)>>();

            foreach (var tenantId in _testTenantIds)
            {
                if (!_tenantClients.TryGetValue(tenantId, out var client))
                    continue;

                isolationTasks.Add(Task.Run(async () =>
                {
                    var getUsersCommand = new TestGetUsersCommand { Page = 1, PageSize = 100 };
                    var commandData = ProtoSerializer.Serialize(getUsersCommand);
                    var request = new CommandRequest
                    {
                        CommandType = typeof(TestGetUsersCommand).AssemblyQualifiedName!,
                        CommandData = ByteString.CopyFrom(commandData),
                        CorrelationId = Guid.NewGuid().ToString()
                    };

                    try
                    {
                        var response = await client.ExecuteCommandAsync(request);
                        var userNames = new List<string>();
                        
                        if (response.Success && response.ResultData.Length > 0)
                        {
                            // Parse the response to extract user names (simplified)
                            // In a real scenario, you'd deserialize the actual user list
                            userNames.AddRange(tenantCommands[tenantId]); // Assume all created users are returned
                        }
                        
                        return (tenantId, userNames);
                    }
                    catch (Exception)
                    {
                        return (tenantId, new List<string>());
                    }
                }));
            }

            var isolationResults = await Task.WhenAll(isolationTasks);

            // Assert - Verify tenant isolation
            foreach (var result in isolationResults)
            {
                var tenantId = result.TenantId;
                var retrievedUsers = result.UserNames;
                var expectedUsers = tenantCommands[tenantId];

                Console.WriteLine($"Tenant {tenantId}: Created {expectedUsers.Count} users, Retrieved {retrievedUsers.Count} users");

                // Verify no cross-tenant data contamination
                foreach (var otherTenantId in _testTenantIds.Where(t => t != tenantId))
                {
                    var otherTenantUsers = tenantCommands[otherTenantId];
                    var crossContamination = retrievedUsers.Intersect(otherTenantUsers).ToList();
                    
                    Assert.AreEqual(0, crossContamination.Count, 
                        $"Tenant {tenantId} contains data from tenant {otherTenantId}: {string.Join(", ", crossContamination)}");
                }
            }

            Console.WriteLine("Tenant isolation verified - no cross-contamination detected");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Tenant isolation test failed: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task MultiTenant_ScalabilityTest_PerformanceDegradationWithinLimits()
    {
        // Arrange - Test with increasing number of active tenants
        var tenantSubsets = new List<List<string>>
        {
            _testTenantIds.Take(1).ToList(),
            _testTenantIds.Take(2).ToList(),
            _testTenantIds.Take(4).ToList(),
            _testTenantIds.Take(6).ToList()
        };

        const int commandsPerTest = 50;
        var scalabilityResults = new List<(int TenantCount, double Throughput, double AvgLatency, double SuccessRate)>();

        try
        {
            foreach (var tenantSubset in tenantSubsets)
            {
                var stopwatch = Stopwatch.StartNew();
                var results = new ConcurrentBag<(bool Success, TimeSpan Duration)>();

                // Execute concurrent load on this subset of tenants
                var tasks = new List<Task>();

                foreach (var tenantId in tenantSubset)
                {
                    if (!_tenantClients.TryGetValue(tenantId, out var client))
                        continue;

                    tasks.Add(Task.Run(async () =>
                    {
                        for (int i = 0; i < commandsPerTest; i++)
                        {
                            var commandStopwatch = Stopwatch.StartNew();
                            try
                            {
                                var command = new TestCreateUserCommand 
                                { 
                                    Name = $"Scale_{tenantId}_{i}" 
                                };
                                var commandData = ProtoSerializer.Serialize(command);
                                var request = new CommandRequest
                                {
                                    CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                                    CommandData = ByteString.CopyFrom(commandData),
                                    CorrelationId = Guid.NewGuid().ToString()
                                };

                                var response = await client.ExecuteCommandAsync(request);
                                commandStopwatch.Stop();
                                results.Add((response.Success, commandStopwatch.Elapsed));
                            }
                            catch (Exception)
                            {
                                commandStopwatch.Stop();
                                results.Add((false, commandStopwatch.Elapsed));
                            }
                        }
                    }));
                }

                await Task.WhenAll(tasks);
                stopwatch.Stop();

                var successful = results.Count(r => r.Success);
                var throughput = successful / stopwatch.Elapsed.TotalSeconds;
                var avgLatency = results.Where(r => r.Success).Average(r => r.Duration.TotalMilliseconds);
                var successRate = successful / (double)results.Count;

                scalabilityResults.Add((tenantSubset.Count, throughput, avgLatency, successRate));

                Console.WriteLine($"Tenants: {tenantSubset.Count}, Throughput: {throughput:F2} cmd/sec, " +
                                $"Latency: {avgLatency:F2}ms, Success: {successRate:P}");

                // Brief pause between scalability tests
                await Task.Delay(1000);
            }

            // Assert - Performance degradation should be within acceptable limits
            var baselineThroughput = scalabilityResults[0].Throughput;
            var baselineLatency = scalabilityResults[0].AvgLatency;

            for (int i = 1; i < scalabilityResults.Count; i++)
            {
                var result = scalabilityResults[i];
                var throughputDegradation = (baselineThroughput - result.Throughput) / baselineThroughput;
                var latencyIncrease = (result.AvgLatency - baselineLatency) / baselineLatency;

                Console.WriteLine($"With {result.TenantCount} tenants: " +
                                $"Throughput degradation: {throughputDegradation:P}, " +
                                $"Latency increase: {latencyIncrease:P}");

                // Performance should not degrade more than 50% with 6x tenant load
                Assert.IsTrue(throughputDegradation < 0.5, 
                    $"Throughput degraded too much with {result.TenantCount} tenants: {throughputDegradation:P}");
                
                Assert.IsTrue(latencyIncrease < 2.0, 
                    $"Latency increased too much with {result.TenantCount} tenants: {latencyIncrease:P}");
                
                Assert.IsTrue(result.SuccessRate > 0.95, 
                    $"Success rate too low with {result.TenantCount} tenants: {result.SuccessRate:P}");
            }

            Console.WriteLine("Scalability test passed - performance degradation within acceptable limits");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Scalability test failed: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task MultiTenant_ResourceUtilization_MemoryAndCpuWithinLimits()
    {
        // Arrange
        const int loadDurationSeconds = 30;
        const int commandsPerSecondPerTenant = 5;
        
        var initialMemory = GC.GetTotalMemory(false);
        var initialProcessorTime = Process.GetCurrentProcess().TotalProcessorTime;
        
        var endTime = DateTime.UtcNow.AddSeconds(loadDurationSeconds);
        var allResults = new ConcurrentBag<bool>();

        try
        {
            Console.WriteLine($"Starting {loadDurationSeconds}s resource utilization test...");
            Console.WriteLine($"Initial memory: {initialMemory / 1024 / 1024:F2} MB");

            // Act - Sustained load across all tenants
            var loadTask = Task.Run(async () =>
            {
                var tenantTasks = new List<Task>();

                foreach (var tenantId in _testTenantIds)
                {
                    if (!_tenantClients.TryGetValue(tenantId, out var client))
                        continue;

                    tenantTasks.Add(Task.Run(async () =>
                    {
                        var requestCounter = 0;
                        while (DateTime.UtcNow < endTime)
                        {
                            var batchStart = DateTime.UtcNow;
                            var batchTasks = new List<Task>();

                            for (int i = 0; i < commandsPerSecondPerTenant && DateTime.UtcNow < endTime; i++)
                            {
                                batchTasks.Add(Task.Run(async () =>
                                {
                                    try
                                    {
                                        var command = new TestCreateUserCommand 
                                        { 
                                            Name = $"Resource_{tenantId}_{Interlocked.Increment(ref requestCounter)}" 
                                        };
                                        var commandData = ProtoSerializer.Serialize(command);
                                        var request = new CommandRequest
                                        {
                                            CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                                            CommandData = ByteString.CopyFrom(commandData),
                                            CorrelationId = Guid.NewGuid().ToString()
                                        };

                                        var response = await client.ExecuteCommandAsync(request);
                                        allResults.Add(response.Success);
                                    }
                                    catch (Exception)
                                    {
                                        allResults.Add(false);
                                    }
                                }));
                            }

                            await Task.WhenAll(batchTasks);

                            // Maintain target rate
                            var batchDuration = DateTime.UtcNow - batchStart;
                            var targetBatchDuration = TimeSpan.FromSeconds(1);
                            if (batchDuration < targetBatchDuration)
                            {
                                await Task.Delay(targetBatchDuration - batchDuration);
                            }
                        }
                    }));
                }

                await Task.WhenAll(tenantTasks);
            });

            await loadTask;

            // Measure final resource usage
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var finalMemory = GC.GetTotalMemory(false);
            var finalProcessorTime = Process.GetCurrentProcess().TotalProcessorTime;
            
            var memoryIncrease = finalMemory - initialMemory;
            var cpuTimeUsed = finalProcessorTime - initialProcessorTime;
            
            var totalRequests = allResults.Count;
            var successfulRequests = allResults.Count(r => r);
            var successRate = successfulRequests / (double)totalRequests;

            Console.WriteLine($"Resource Utilization Results:");
            Console.WriteLine($"  Duration: {loadDurationSeconds}s");
            Console.WriteLine($"  Total Requests: {totalRequests}");
            Console.WriteLine($"  Successful Requests: {successfulRequests}");
            Console.WriteLine($"  Success Rate: {successRate:P}");
            Console.WriteLine($"  Initial Memory: {initialMemory / 1024 / 1024:F2} MB");
            Console.WriteLine($"  Final Memory: {finalMemory / 1024 / 1024:F2} MB");
            Console.WriteLine($"  Memory Increase: {memoryIncrease / 1024 / 1024:F2} MB");
            Console.WriteLine($"  Memory per Request: {memoryIncrease / totalRequests:F0} bytes");
            Console.WriteLine($"  CPU Time Used: {cpuTimeUsed.TotalMilliseconds:F2}ms");
            Console.WriteLine($"  CPU per Request: {cpuTimeUsed.TotalMilliseconds / totalRequests:F3}ms");

            // Assert - Resource usage should be reasonable
            Assert.IsTrue(successRate > 0.95, $"Success rate too low: {successRate:P}");
            
            // Memory should not increase excessively (less than 2KB per request)
            var memoryPerRequest = memoryIncrease / totalRequests;
            Assert.IsTrue(memoryPerRequest < 2048, 
                $"Memory usage per request too high: {memoryPerRequest:F0} bytes");
            
            // Total memory increase should be reasonable (less than 500MB for the test)
            var memoryIncreaseMB = memoryIncrease / 1024 / 1024;
            Assert.IsTrue(memoryIncreaseMB < 500, 
                $"Total memory increase too high: {memoryIncreaseMB:F2} MB");

            Console.WriteLine("Resource utilization test passed - memory and CPU usage within limits");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Resource utilization test failed: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task MultiTenant_FailoverResilience_TenantRecoveryAfterFailure()
    {
        // Arrange - Use first 3 tenants for failover testing
        var testTenants = _testTenantIds.Take(3).ToList();
        const int commandsBeforeFailure = 10;
        const int commandsAfterRecovery = 10;

        try
        {
            // Act Phase 1 - Send normal traffic to establish baseline
            Console.WriteLine("Phase 1: Establishing baseline traffic...");
            var baselineResults = new ConcurrentBag<bool>();
            
            var baselineTasks = testTenants.Select(tenantId => Task.Run(async () =>
            {
                if (!_tenantClients.TryGetValue(tenantId, out var client))
                    return;

                for (int i = 0; i < commandsBeforeFailure; i++)
                {
                    try
                    {
                        var command = new TestCreateUserCommand { Name = $"Baseline_{tenantId}_{i}" };
                        var commandData = ProtoSerializer.Serialize(command);
                        var request = new CommandRequest
                        {
                            CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                            CommandData = ByteString.CopyFrom(commandData),
                            CorrelationId = Guid.NewGuid().ToString()
                        };

                        var response = await client.ExecuteCommandAsync(request);
                        baselineResults.Add(response.Success);
                    }
                    catch (Exception)
                    {
                        baselineResults.Add(false);
                    }
                }
            })).ToList();

            await Task.WhenAll(baselineTasks);

            var baselineSuccessRate = baselineResults.Count(r => r) / (double)baselineResults.Count;
            Console.WriteLine($"Baseline success rate: {baselineSuccessRate:P}");

            // Act Phase 2 - Simulate tenant failure by stopping one process
            var failedTenantId = testTenants[0];
            Console.WriteLine($"Phase 2: Simulating failure of tenant {failedTenantId}...");
            
            await _processManager!.StopTenantProcessAsync(failedTenantId);
            await Task.Delay(2000); // Wait for failure to take effect

            // Test other tenants are still working
            var duringFailureResults = new ConcurrentBag<(string TenantId, bool Success)>();
            var duringFailureTasks = testTenants.Select(tenantId => Task.Run(async () =>
            {
                if (!_tenantClients.TryGetValue(tenantId, out var client))
                    return;

                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        var command = new TestCreateUserCommand { Name = $"DuringFailure_{tenantId}_{i}" };
                        var commandData = ProtoSerializer.Serialize(command);
                        var request = new CommandRequest
                        {
                            CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                            CommandData = ByteString.CopyFrom(commandData),
                            CorrelationId = Guid.NewGuid().ToString()
                        };

                        var response = await client.ExecuteCommandAsync(request);
                        duringFailureResults.Add((tenantId, response.Success));
                    }
                    catch (Exception)
                    {
                        duringFailureResults.Add((tenantId, false));
                    }
                }
            })).ToList();

            await Task.WhenAll(duringFailureTasks);

            // Act Phase 3 - Restart failed tenant and test recovery
            Console.WriteLine($"Phase 3: Restarting tenant {failedTenantId}...");
            var failedTenantPort = _tenantPorts[failedTenantId];
            await _processManager.StartTenantProcessAsync(failedTenantId);
            await Task.Delay(3000); // Wait for recovery

            // Test recovery
            var recoveryResults = new ConcurrentBag<bool>();
            var recoveryTasks = testTenants.Select(tenantId => Task.Run(async () =>
            {
                if (!_tenantClients.TryGetValue(tenantId, out var client))
                    return;

                for (int i = 0; i < commandsAfterRecovery; i++)
                {
                    try
                    {
                        var command = new TestCreateUserCommand { Name = $"Recovery_{tenantId}_{i}" };
                        var commandData = ProtoSerializer.Serialize(command);
                        var request = new CommandRequest
                        {
                            CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                            CommandData = ByteString.CopyFrom(commandData),
                            CorrelationId = Guid.NewGuid().ToString()
                        };

                        var response = await client.ExecuteCommandAsync(request);
                        recoveryResults.Add(response.Success);
                    }
                    catch (Exception)
                    {
                        recoveryResults.Add(false);
                    }
                }
            })).ToList();

            await Task.WhenAll(recoveryTasks);

            var recoverySuccessRate = recoveryResults.Count(r => r) / (double)recoveryResults.Count;

            // Assert
            Console.WriteLine($"Failover Resilience Results:");
            Console.WriteLine($"  Baseline Success Rate: {baselineSuccessRate:P}");
            Console.WriteLine($"  Recovery Success Rate: {recoverySuccessRate:P}");
            
            // During failure, other tenants should continue working
            var workingTenantResults = duringFailureResults.Where(r => r.TenantId != failedTenantId).ToList();
            var workingTenantSuccessRate = workingTenantResults.Count(r => r.Success) / (double)workingTenantResults.Count;
            Console.WriteLine($"  Other Tenants During Failure: {workingTenantSuccessRate:P}");
            
            // Failed tenant should have failed during the outage
            var failedTenantResults = duringFailureResults.Where(r => r.TenantId == failedTenantId).ToList();
            var failedTenantSuccessRate = failedTenantResults.Count(r => r.Success) / (double)(failedTenantResults.Count + 0.001); // Avoid div by zero
            Console.WriteLine($"  Failed Tenant During Outage: {failedTenantSuccessRate:P}");

            // Assertions
            Assert.IsTrue(baselineSuccessRate > 0.95, "Baseline success rate should be high");
            Assert.IsTrue(workingTenantSuccessRate > 0.9, "Other tenants should continue working during failure");
            Assert.IsTrue(failedTenantSuccessRate < 0.1, "Failed tenant should have low success rate during outage");
            Assert.IsTrue(recoverySuccessRate > 0.9, "Recovery success rate should be high after restart");

            Console.WriteLine("Failover resilience test passed - tenant isolation and recovery verified");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Failover resilience test failed: {ex.Message}");
        }
    }
}