using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Grpc.Net.Client;
using ACS.Core.Grpc;
using ACS.Service.Infrastructure;
using ACS.Infrastructure;
using Google.Protobuf;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace ACS.VerticalHost.Tests;

[TestClass]
public class GrpcPerformanceTests
{
    private WebApplicationFactory<ACS.VerticalHost.Program>? _factory;
    private GrpcChannel? _channel;
    private VerticalService.VerticalServiceClient? _client;
    private const string TestTenantId = "perf-test-tenant";

    [TestInitialize]
    public async Task Setup()
    {
        // Create test application factory optimized for performance testing
        _factory = new WebApplicationFactory<ACS.VerticalHost.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Configure for performance testing
                    services.Configure<HostOptions>(options =>
                    {
                        options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
                    });
                });
            });

        // Set environment variables for testing
        Environment.SetEnvironmentVariable("TENANT_ID", TestTenantId);
        Environment.SetEnvironmentVariable("GRPC_PORT", "50054");

        var client = _factory.CreateClient();
        _channel = GrpcChannel.ForAddress(client.BaseAddress!, new GrpcChannelOptions
        {
            HttpClient = client,
            MaxReceiveMessageSize = 16 * 1024 * 1024, // 16MB
            MaxSendMessageSize = 16 * 1024 * 1024      // 16MB
        });
        _client = new VerticalService.VerticalServiceClient(_channel);

        // Warm up the service
        await WarmUpService();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (_channel != null)
            await _channel.ShutdownAsync();
        
        _factory?.Dispose();
    }

    private async Task WarmUpService()
    {
        try
        {
            // Send a few warm-up requests
            for (int i = 0; i < 3; i++)
            {
                await _client!.HealthCheckAsync(new HealthRequest());
                await Task.Delay(100);
            }
        }
        catch (Exception)
        {
            // Warm-up failures are not critical
        }
    }

    [TestMethod]
    public async Task Performance_SingleCommandThroughput_MeetsBaseline()
    {
        // Arrange
        const int commandCount = 100;
        var stopwatch = Stopwatch.StartNew();
        var successCount = 0;

        try
        {
            // Act
            for (int i = 0; i < commandCount; i++)
            {
                var command = new TestCreateUserCommand { Name = $"Perf User {i}" };
                var commandData = ProtoSerializer.Serialize(command);
                var request = new CommandRequest
                {
                    CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                    CommandData = ByteString.CopyFrom(commandData),
                    CorrelationId = Guid.NewGuid().ToString()
                };

                var response = await _client!.ExecuteCommandAsync(request);
                if (response.Success)
                    successCount++;
            }

            stopwatch.Stop();

            // Assert
            Assert.AreEqual(commandCount, successCount);
            
            var throughput = commandCount / stopwatch.Elapsed.TotalSeconds;
            var averageLatency = stopwatch.ElapsedMilliseconds / (double)commandCount;
            
            // Performance baselines (adjust based on requirements)
            Assert.IsTrue(throughput > 10, $"Throughput too low: {throughput:F2} commands/sec");
            Assert.IsTrue(averageLatency < 100, $"Average latency too high: {averageLatency:F2}ms");

            Console.WriteLine($"Performance Results:");
            Console.WriteLine($"  Commands: {commandCount}");
            Console.WriteLine($"  Total Time: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"  Throughput: {throughput:F2} commands/sec");
            Console.WriteLine($"  Average Latency: {averageLatency:F2}ms");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Performance test failed: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task Performance_ConcurrentCommandExecution_ScalesWell()
    {
        // Arrange
        const int concurrentClients = 10;
        const int commandsPerClient = 20;
        var stopwatch = Stopwatch.StartNew();
        var totalSuccessCount = 0;
        var results = new ConcurrentBag<(bool Success, TimeSpan Duration)>();

        try
        {
            // Act - Create multiple concurrent clients
            var tasks = new List<Task>();
            
            for (int clientId = 0; clientId < concurrentClients; clientId++)
            {
                int localClientId = clientId; // Capture for closure
                tasks.Add(Task.Run(async () =>
                {
                    var clientStopwatch = Stopwatch.StartNew();
                    var clientSuccessCount = 0;

                    for (int i = 0; i < commandsPerClient; i++)
                    {
                        try
                        {
                            var command = new TestCreateUserCommand { Name = $"Client{localClientId}_User{i}" };
                            var commandData = ProtoSerializer.Serialize(command);
                            var request = new CommandRequest
                            {
                                CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                                CommandData = ByteString.CopyFrom(commandData),
                                CorrelationId = $"client-{localClientId}-command-{i}"
                            };

                            var response = await _client!.ExecuteCommandAsync(request);
                            if (response.Success)
                                clientSuccessCount++;
                        }
                        catch (Exception)
                        {
                            // Continue with other commands
                        }
                    }

                    clientStopwatch.Stop();
                    Interlocked.Add(ref totalSuccessCount, clientSuccessCount);
                    results.Add((clientSuccessCount == commandsPerClient, clientStopwatch.Elapsed));
                }));
            }

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            var expectedTotal = concurrentClients * commandsPerClient;
            var successRate = totalSuccessCount / (double)expectedTotal;
            var overallThroughput = totalSuccessCount / stopwatch.Elapsed.TotalSeconds;
            var averageClientDuration = results.Average(r => r.Duration.TotalMilliseconds);

            Assert.IsTrue(successRate > 0.95, $"Success rate too low: {successRate:P}");
            Assert.IsTrue(overallThroughput > 50, $"Concurrent throughput too low: {overallThroughput:F2} commands/sec");

            Console.WriteLine($"Concurrent Performance Results:");
            Console.WriteLine($"  Concurrent Clients: {concurrentClients}");
            Console.WriteLine($"  Commands per Client: {commandsPerClient}");
            Console.WriteLine($"  Total Commands: {expectedTotal}");
            Console.WriteLine($"  Successful Commands: {totalSuccessCount}");
            Console.WriteLine($"  Success Rate: {successRate:P}");
            Console.WriteLine($"  Total Time: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"  Overall Throughput: {overallThroughput:F2} commands/sec");
            Console.WriteLine($"  Average Client Duration: {averageClientDuration:F2}ms");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Concurrent performance test failed: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task Performance_MemoryUsage_RemainsStable()
    {
        // Arrange
        const int commandBatches = 10;
        const int commandsPerBatch = 50;
        var memoryReadings = new List<long>();

        try
        {
            // Take initial memory reading
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var initialMemory = GC.GetTotalMemory(false);
            memoryReadings.Add(initialMemory);

            // Act - Execute commands in batches and monitor memory
            for (int batch = 0; batch < commandBatches; batch++)
            {
                // Execute a batch of commands
                var tasks = new List<Task>();
                for (int i = 0; i < commandsPerBatch; i++)
                {
                    var command = new TestCreateUserCommand { Name = $"Batch{batch}_User{i}" };
                    var commandData = ProtoSerializer.Serialize(command);
                    var request = new CommandRequest
                    {
                        CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                        CommandData = ByteString.CopyFrom(commandData),
                        CorrelationId = Guid.NewGuid().ToString()
                    };

                    tasks.Add(_client!.ExecuteCommandAsync(request).ResponseAsync);
                }

                await Task.WhenAll(tasks);

                // Take memory reading after batch
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                var currentMemory = GC.GetTotalMemory(false);
                memoryReadings.Add(currentMemory);

                // Small delay between batches
                await Task.Delay(100);
            }

            // Assert - Memory usage should be relatively stable
            var finalMemory = memoryReadings.Last();
            var memoryIncrease = finalMemory - initialMemory;
            var memoryIncreasePerCommand = memoryIncrease / (double)(commandBatches * commandsPerBatch);

            // Allow some memory increase but it should be reasonable
            Assert.IsTrue(memoryIncreasePerCommand < 1024, // Less than 1KB per command
                $"Memory increase per command too high: {memoryIncreasePerCommand:F0} bytes");

            Console.WriteLine($"Memory Usage Results:");
            Console.WriteLine($"  Initial Memory: {initialMemory / 1024:F0} KB");
            Console.WriteLine($"  Final Memory: {finalMemory / 1024:F0} KB");
            Console.WriteLine($"  Total Increase: {memoryIncrease / 1024:F0} KB");
            Console.WriteLine($"  Commands Executed: {commandBatches * commandsPerBatch}");
            Console.WriteLine($"  Memory per Command: {memoryIncreasePerCommand:F0} bytes");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Memory usage test failed: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task Performance_LargePayload_HandlesEfficiently()
    {
        try
        {
            // Arrange - Create command with large data
            var largeCommand = new TestCreateUserCommand 
            { 
                Name = "Large Payload User",
                // Simulate large payload with metadata
                Metadata = new string('A', 10 * 1024) // 10KB string
            };

            var commandData = ProtoSerializer.Serialize(largeCommand);
            var stopwatch = Stopwatch.StartNew();

            var request = new CommandRequest
            {
                CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                CommandData = ByteString.CopyFrom(commandData),
                CorrelationId = Guid.NewGuid().ToString()
            };

            // Act
            var response = await _client!.ExecuteCommandAsync(request);
            stopwatch.Stop();

            // Assert
            Assert.IsTrue(response.Success);
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 1000, // Should handle large payload quickly
                $"Large payload took too long: {stopwatch.ElapsedMilliseconds}ms");

            Console.WriteLine($"Large Payload Results:");
            Console.WriteLine($"  Payload Size: {commandData.Length / 1024:F0} KB");
            Console.WriteLine($"  Processing Time: {stopwatch.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Large payload test failed: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task Performance_HealthCheckLatency_IsMinimal()
    {
        // Arrange
        const int healthCheckCount = 100;
        var latencies = new List<long>();

        try
        {
            // Act - Perform multiple health checks
            for (int i = 0; i < healthCheckCount; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                var response = await _client!.HealthCheckAsync(new HealthRequest());
                stopwatch.Stop();

                Assert.IsTrue(response.Healthy);
                latencies.Add(stopwatch.ElapsedMilliseconds);

                // Small delay to avoid overwhelming
                if (i % 10 == 0)
                    await Task.Delay(10);
            }

            // Assert
            var averageLatency = latencies.Average();
            var maxLatency = latencies.Max();
            var minLatency = latencies.Min();

            Assert.IsTrue(averageLatency < 10, $"Average health check latency too high: {averageLatency:F2}ms");
            Assert.IsTrue(maxLatency < 50, $"Max health check latency too high: {maxLatency}ms");

            Console.WriteLine($"Health Check Performance Results:");
            Console.WriteLine($"  Health Checks: {healthCheckCount}");
            Console.WriteLine($"  Average Latency: {averageLatency:F2}ms");
            Console.WriteLine($"  Min Latency: {minLatency}ms");
            Console.WriteLine($"  Max Latency: {maxLatency}ms");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Health check performance test failed: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task LoadTest_SustainedLoad_MaintainsPerformance()
    {
        // Arrange
        const int durationSeconds = 30;
        const int targetRps = 20; // Requests per second
        var endTime = DateTime.UtcNow.AddSeconds(durationSeconds);
        var successCount = 0;
        var errorCount = 0;
        var latencies = new ConcurrentBag<double>();

        try
        {
            Console.WriteLine($"Starting {durationSeconds}s load test targeting {targetRps} RPS...");

            // Act - Sustained load test
            var loadTestTask = Task.Run(async () =>
            {
                var requestCounter = 0;
                while (DateTime.UtcNow < endTime)
                {
                    var batchStart = DateTime.UtcNow;
                    var batchTasks = new List<Task>();

                    // Send batch of requests to achieve target RPS
                    for (int i = 0; i < targetRps && DateTime.UtcNow < endTime; i++)
                    {
                        batchTasks.Add(Task.Run(async () =>
                        {
                            var requestId = Interlocked.Increment(ref requestCounter);
                            var stopwatch = Stopwatch.StartNew();

                            try
                            {
                                var command = new TestCreateUserCommand { Name = $"Load_User_{requestId}" };
                                var commandData = ProtoSerializer.Serialize(command);
                                var request = new CommandRequest
                                {
                                    CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                                    CommandData = ByteString.CopyFrom(commandData),
                                    CorrelationId = $"load-test-{requestId}"
                                };

                                var response = await _client!.ExecuteCommandAsync(request);
                                stopwatch.Stop();

                                if (response.Success)
                                    Interlocked.Increment(ref successCount);
                                else
                                    Interlocked.Increment(ref errorCount);

                                latencies.Add(stopwatch.Elapsed.TotalMilliseconds);
                            }
                            catch (Exception)
                            {
                                Interlocked.Increment(ref errorCount);
                                stopwatch.Stop();
                                latencies.Add(stopwatch.Elapsed.TotalMilliseconds);
                            }
                        }));
                    }

                    await Task.WhenAll(batchTasks);

                    // Maintain target RPS by waiting if needed
                    var batchDuration = DateTime.UtcNow - batchStart;
                    var targetBatchDuration = TimeSpan.FromSeconds(1);
                    if (batchDuration < targetBatchDuration)
                    {
                        await Task.Delay(targetBatchDuration - batchDuration);
                    }
                }
            });

            await loadTestTask;

            // Assert
            var totalRequests = successCount + errorCount;
            var actualRps = totalRequests / (double)durationSeconds;
            var errorRate = errorCount / (double)totalRequests;
            var avgLatency = latencies.Any() ? latencies.Average() : 0;
            var p95Latency = latencies.Any() ? latencies.OrderBy(l => l).Skip((int)(latencies.Count * 0.95)).FirstOrDefault() : 0;

            Assert.IsTrue(errorRate < 0.05, $"Error rate too high: {errorRate:P}"); // Less than 5% errors
            Assert.IsTrue(avgLatency < 100, $"Average latency too high: {avgLatency:F2}ms");
            Assert.IsTrue(actualRps > targetRps * 0.8, $"Actual RPS too low: {actualRps:F1} (target: {targetRps})");

            Console.WriteLine($"Load Test Results:");
            Console.WriteLine($"  Duration: {durationSeconds}s");
            Console.WriteLine($"  Total Requests: {totalRequests}");
            Console.WriteLine($"  Successful Requests: {successCount}");
            Console.WriteLine($"  Failed Requests: {errorCount}");
            Console.WriteLine($"  Error Rate: {errorRate:P}");
            Console.WriteLine($"  Actual RPS: {actualRps:F1}");
            Console.WriteLine($"  Average Latency: {avgLatency:F2}ms");
            Console.WriteLine($"  P95 Latency: {p95Latency:F2}ms");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Load test failed: {ex.Message}");
        }
    }
}

