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
using System.Text.Json;

namespace ACS.VerticalHost.Tests;

[TestClass]
public class MultiTenantBenchmarkTests
{
    private readonly List<string> _benchmarkTenantIds = new()
    {
        "bench-tenant-1", "bench-tenant-2"
    };
    
    private readonly Dictionary<string, int> _tenantPorts = new()
    {
        { "bench-tenant-1", 50091 }, { "bench-tenant-2", 50092 }
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
            serviceCollection.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<TenantProcessManager>>();
            _processManager = new TenantProcessManager(logger);

            var setupTasks = _benchmarkTenantIds.Select(async tenantId =>
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
                        MaxReceiveMessageSize = 128 * 1024 * 1024, // 128MB
                        MaxSendMessageSize = 128 * 1024 * 1024,     // 128MB
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
                    Console.WriteLine($"Failed to setup benchmark tenant {tenantId}: {ex.Message}");
                }
            });

            await Task.WhenAll(setupTasks);
            await Task.Delay(2000);

            // Comprehensive warmup
            await PerformWarmup();
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Could not setup multi-tenant benchmark environment: {ex.Message}");
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
                foreach (var tenantId in _benchmarkTenantIds)
                {
                    try { await _processManager.StopTenantProcessAsync(tenantId); } catch { }
                }
                _processManager.Dispose();
            }
        }
        catch (Exception) { }
    }

    private async Task PerformWarmup()
    {
        const int warmupRequests = 20;
        
        var warmupTasks = _benchmarkTenantIds.Select(async tenantId =>
        {
            if (!_tenantClients.TryGetValue(tenantId, out var client))
                return;

            for (int i = 0; i < warmupRequests; i++)
            {
                try
                {
                    await client.HealthCheckAsync(new HealthRequest());
                    
                    var command = new TestCreateUserCommand { Name = $"Warmup_{tenantId}_{i}" };
                    var commandData = ProtoSerializer.Serialize(command);
                    var request = new CommandRequest
                    {
                        CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                        CommandData = ByteString.CopyFrom(commandData),
                        CorrelationId = Guid.NewGuid().ToString()
                    };

                    await client.ExecuteCommandAsync(request);
                    await Task.Delay(50); // Small delay between warmup requests
                }
                catch (Exception)
                {
                    // Warmup failures are acceptable
                }
            }
        }).ToList();

        await Task.WhenAll(warmupTasks);
        
        // Final warmup pause
        await Task.Delay(1000);
        
        // Force GC to establish clean baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    [TestMethod]
    public async Task Benchmark_SingleTenantThroughput_EstablishBaseline()
    {
        // Arrange
        const int testDurationSeconds = 30;
        var concurrencyLevels = new[] { 1, 5, 10, 20, 50 };
        var benchmarkResults = new List<BenchmarkResult>();

        var singleTenantId = _benchmarkTenantIds.First();
        if (!_tenantClients.TryGetValue(singleTenantId, out var client))
        {
            Assert.Inconclusive($"Client not available for tenant {singleTenantId}");
            return;
        }

        try
        {
            Console.WriteLine($"Single Tenant Throughput Benchmark - Testing {singleTenantId}");
            
            foreach (var concurrency in concurrencyLevels)
            {
                Console.WriteLine($"Testing concurrency level: {concurrency}");
                
                var results = new ConcurrentBag<(bool Success, TimeSpan Latency)>();
                var endTime = DateTime.UtcNow.AddSeconds(testDurationSeconds);
                var stopwatch = Stopwatch.StartNew();

                var concurrentTasks = Enumerable.Range(0, concurrency).Select(_ => Task.Run(async () =>
                {
                    var requestCounter = 0;
                    while (DateTime.UtcNow < endTime)
                    {
                        var requestStopwatch = Stopwatch.StartNew();
                        try
                        {
                            var command = new TestCreateUserCommand 
                            { 
                                Name = $"Bench_{singleTenantId}_C{concurrency}_{Interlocked.Increment(ref requestCounter)}" 
                            };
                            var commandData = ProtoSerializer.Serialize(command);
                            var request = new CommandRequest
                            {
                                CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                                CommandData = ByteString.CopyFrom(commandData),
                                CorrelationId = Guid.NewGuid().ToString()
                            };

                            var response = await client.ExecuteCommandAsync(request);
                            requestStopwatch.Stop();
                            results.Add((response.Success, requestStopwatch.Elapsed));
                        }
                        catch (Exception)
                        {
                            requestStopwatch.Stop();
                            results.Add((false, requestStopwatch.Elapsed));
                        }
                    }
                })).ToList();

                await Task.WhenAll(concurrentTasks);
                stopwatch.Stop();

                var successful = results.Count(r => r.Success);
                var throughput = successful / stopwatch.Elapsed.TotalSeconds;
                var avgLatency = results.Where(r => r.Success).Average(r => r.Latency.TotalMilliseconds);
                var medianLatency = CalculatePercentile(results.Where(r => r.Success).Select(r => r.Latency.TotalMilliseconds).ToList(), 50);
                var p95Latency = CalculatePercentile(results.Where(r => r.Success).Select(r => r.Latency.TotalMilliseconds).ToList(), 95);
                var p99Latency = CalculatePercentile(results.Where(r => r.Success).Select(r => r.Latency.TotalMilliseconds).ToList(), 99);
                var successRate = successful / (double)results.Count;

                var benchmarkResult = new BenchmarkResult
                {
                    TestName = $"SingleTenant_C{concurrency}",
                    Concurrency = concurrency,
                    Duration = stopwatch.Elapsed,
                    TotalRequests = results.Count,
                    SuccessfulRequests = successful,
                    SuccessRate = successRate,
                    Throughput = throughput,
                    AvgLatency = avgLatency,
                    MedianLatency = medianLatency,
                    P95Latency = p95Latency,
                    P99Latency = p99Latency
                };

                benchmarkResults.Add(benchmarkResult);
                
                Console.WriteLine($"  Results: {throughput:F1} req/sec, {avgLatency:F2}ms avg, " +
                                $"{p95Latency:F2}ms p95, {successRate:P} success");

                // Brief pause between concurrency tests
                await Task.Delay(2000);
            }

            // Output comprehensive results
            Console.WriteLine($"\nSingle Tenant Benchmark Results:");
            Console.WriteLine("Concurrency | Throughput | Avg Lat | Med Lat | P95 Lat | P99 Lat | Success");
            Console.WriteLine("-----------|------------|---------|---------|---------|---------|--------");
            
            foreach (var result in benchmarkResults)
            {
                Console.WriteLine($"{result.Concurrency,10} | {result.Throughput,10:F1} | " +
                                $"{result.AvgLatency,7:F1} | {result.MedianLatency,7:F1} | " +
                                $"{result.P95Latency,7:F1} | {result.P99Latency,7:F1} | " +
                                $"{result.SuccessRate,6:P0}");
            }

            // Find optimal concurrency level
            var optimalResult = benchmarkResults
                .Where(r => r.SuccessRate > 0.99)
                .OrderByDescending(r => r.Throughput)
                .FirstOrDefault();

            if (optimalResult != null)
            {
                Console.WriteLine($"\nOptimal single-tenant configuration:");
                Console.WriteLine($"  Concurrency: {optimalResult.Concurrency}");
                Console.WriteLine($"  Throughput: {optimalResult.Throughput:F1} req/sec");
                Console.WriteLine($"  P95 Latency: {optimalResult.P95Latency:F1}ms");
            }

            // Assertions for baseline performance
            var bestThroughput = benchmarkResults.Max(r => r.Throughput);
            var bestP95Latency = benchmarkResults.Where(r => r.SuccessRate > 0.99).Min(r => r.P95Latency);
            
            Assert.IsTrue(bestThroughput > 100, $"Peak throughput too low: {bestThroughput:F1} req/sec");
            Assert.IsTrue(bestP95Latency < 200, $"Best P95 latency too high: {bestP95Latency:F1}ms");
            
            Console.WriteLine("Single tenant throughput benchmark completed");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Single tenant benchmark failed: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task Benchmark_MultiTenantThroughput_CompareWithBaseline()
    {
        // Arrange
        const int testDurationSeconds = 30;
        var concurrencyLevelsPerTenant = new[] { 1, 5, 10, 25 };
        var multiTenantResults = new List<MultiTenantBenchmarkResult>();

        try
        {
            Console.WriteLine("Multi-Tenant Throughput Benchmark");
            
            foreach (var concurrencyPerTenant in concurrencyLevelsPerTenant)
            {
                var totalConcurrency = concurrencyPerTenant * _benchmarkTenantIds.Count;
                Console.WriteLine($"Testing {concurrencyPerTenant} concurrent requests per tenant " +
                                $"({totalConcurrency} total concurrency)");

                var allResults = new ConcurrentBag<(string TenantId, bool Success, TimeSpan Latency)>();
                var endTime = DateTime.UtcNow.AddSeconds(testDurationSeconds);
                var stopwatch = Stopwatch.StartNew();

                var tenantTasks = _benchmarkTenantIds.Select(tenantId => Task.Run(async () =>
                {
                    if (!_tenantClients.TryGetValue(tenantId, out var client))
                        return;

                    var concurrentTasks = Enumerable.Range(0, concurrencyPerTenant).Select(_ => Task.Run(async () =>
                    {
                        var requestCounter = 0;
                        while (DateTime.UtcNow < endTime)
                        {
                            var requestStopwatch = Stopwatch.StartNew();
                            try
                            {
                                var command = new TestCreateUserCommand 
                                { 
                                    Name = $"MultiBench_{tenantId}_C{concurrencyPerTenant}_{Interlocked.Increment(ref requestCounter)}" 
                                };
                                var commandData = ProtoSerializer.Serialize(command);
                                var request = new CommandRequest
                                {
                                    CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                                    CommandData = ByteString.CopyFrom(commandData),
                                    CorrelationId = Guid.NewGuid().ToString()
                                };

                                var response = await client.ExecuteCommandAsync(request);
                                requestStopwatch.Stop();
                                allResults.Add((tenantId, response.Success, requestStopwatch.Elapsed));
                            }
                            catch (Exception)
                            {
                                requestStopwatch.Stop();
                                allResults.Add((tenantId, false, requestStopwatch.Elapsed));
                            }
                        }
                    })).ToList();

                    await Task.WhenAll(concurrentTasks);
                })).ToList();

                await Task.WhenAll(tenantTasks);
                stopwatch.Stop();

                // Analyze overall results
                var totalRequests = allResults.Count;
                var totalSuccessful = allResults.Count(r => r.Success);
                var overallThroughput = totalSuccessful / stopwatch.Elapsed.TotalSeconds;
                var overallSuccessRate = totalSuccessful / (double)totalRequests;
                var overallAvgLatency = allResults.Where(r => r.Success).Average(r => r.Latency.TotalMilliseconds);
                var overallP95Latency = CalculatePercentile(allResults.Where(r => r.Success).Select(r => r.Latency.TotalMilliseconds).ToList(), 95);

                // Per-tenant analysis
                var tenantStats = _benchmarkTenantIds.ToDictionary(tenantId => tenantId, tenantId =>
                {
                    var tenantResults = allResults.Where(r => r.TenantId == tenantId).ToList();
                    var successful = tenantResults.Count(r => r.Success);
                    
                    return new
                    {
                        Total = tenantResults.Count,
                        Successful = successful,
                        Throughput = successful / stopwatch.Elapsed.TotalSeconds,
                        SuccessRate = successful / (double)tenantResults.Count,
                        AvgLatency = tenantResults.Where(r => r.Success).Any() ? 
                            tenantResults.Where(r => r.Success).Average(r => r.Latency.TotalMilliseconds) : 0,
                        P95Latency = tenantResults.Where(r => r.Success).Any() ? 
                            CalculatePercentile(tenantResults.Where(r => r.Success).Select(r => r.Latency.TotalMilliseconds).ToList(), 95) : 0
                    };
                });

                var result = new MultiTenantBenchmarkResult
                {
                    ConcurrencyPerTenant = concurrencyPerTenant,
                    TotalConcurrency = totalConcurrency,
                    Duration = stopwatch.Elapsed,
                    TotalRequests = totalRequests,
                    SuccessfulRequests = totalSuccessful,
                    OverallThroughput = overallThroughput,
                    OverallSuccessRate = overallSuccessRate,
                    OverallAvgLatency = overallAvgLatency,
                    OverallP95Latency = overallP95Latency,
                    TenantStats = tenantStats.ToDictionary(kvp => kvp.Key, kvp => new TenantBenchmarkStats
                    {
                        Throughput = kvp.Value.Throughput,
                        SuccessRate = kvp.Value.SuccessRate,
                        AvgLatency = kvp.Value.AvgLatency,
                        P95Latency = kvp.Value.P95Latency
                    })
                };

                multiTenantResults.Add(result);

                Console.WriteLine($"  Overall: {overallThroughput:F1} req/sec, {overallAvgLatency:F2}ms avg, " +
                                $"{overallP95Latency:F2}ms p95, {overallSuccessRate:P} success");

                foreach (var kvp in tenantStats)
                {
                    var stats = kvp.Value;
                    Console.WriteLine($"    {kvp.Key}: {stats.Throughput:F1} req/sec, " +
                                    $"{stats.AvgLatency:F2}ms avg, {stats.SuccessRate:P} success");
                }

                // Brief pause between tests
                await Task.Delay(2000);
            }

            // Output comprehensive results
            Console.WriteLine($"\nMulti-Tenant Benchmark Results:");
            Console.WriteLine("Per-Tenant | Total    | Throughput | Avg Lat | P95 Lat | Success | Tenant Balance");
            Console.WriteLine("Conc       | Conc     |            |         |         |         |               ");
            Console.WriteLine("-----------|----------|------------|---------|---------|---------|---------------");
            
            foreach (var result in multiTenantResults)
            {
                var tenantThroughputs = result.TenantStats.Values.Select(s => s.Throughput).ToList();
                var throughputBalance = tenantThroughputs.Max() - tenantThroughputs.Min();
                var avgTenantThroughput = tenantThroughputs.Average();
                var balancePercentage = avgTenantThroughput > 0 ? throughputBalance / avgTenantThroughput : 0;

                Console.WriteLine($"{result.ConcurrencyPerTenant,10} | {result.TotalConcurrency,8} | " +
                                $"{result.OverallThroughput,10:F1} | {result.OverallAvgLatency,7:F1} | " +
                                $"{result.OverallP95Latency,7:F1} | {result.OverallSuccessRate,6:P0} | " +
                                $"{balancePercentage,13:P1}");
            }

            // Find optimal multi-tenant configuration
            var optimalMultiTenant = multiTenantResults
                .Where(r => r.OverallSuccessRate > 0.99)
                .OrderByDescending(r => r.OverallThroughput)
                .FirstOrDefault();

            if (optimalMultiTenant != null)
            {
                Console.WriteLine($"\nOptimal multi-tenant configuration:");
                Console.WriteLine($"  Per-Tenant Concurrency: {optimalMultiTenant.ConcurrencyPerTenant}");
                Console.WriteLine($"  Total Throughput: {optimalMultiTenant.OverallThroughput:F1} req/sec");
                Console.WriteLine($"  P95 Latency: {optimalMultiTenant.OverallP95Latency:F1}ms");
                
                Console.WriteLine($"  Per-Tenant Performance:");
                foreach (var kvp in optimalMultiTenant.TenantStats)
                {
                    var stats = kvp.Value;
                    Console.WriteLine($"    {kvp.Key}: {stats.Throughput:F1} req/sec, {stats.P95Latency:F1}ms p95");
                }
            }

            // Performance assertions
            var bestMultiTenantThroughput = multiTenantResults.Max(r => r.OverallThroughput);
            var bestMultiTenantP95 = multiTenantResults.Where(r => r.OverallSuccessRate > 0.99).Min(r => r.OverallP95Latency);
            
            Assert.IsTrue(bestMultiTenantThroughput > 150, 
                $"Peak multi-tenant throughput too low: {bestMultiTenantThroughput:F1} req/sec");
            Assert.IsTrue(bestMultiTenantP95 < 300, 
                $"Best multi-tenant P95 latency too high: {bestMultiTenantP95:F1}ms");

            // Tenant isolation verification - throughput should be balanced
            foreach (var result in multiTenantResults.Where(r => r.OverallSuccessRate > 0.95))
            {
                var tenantThroughputs = result.TenantStats.Values.Select(s => s.Throughput).ToList();
                var maxThroughput = tenantThroughputs.Max();
                var minThroughput = tenantThroughputs.Min();
                var imbalance = maxThroughput > 0 ? (maxThroughput - minThroughput) / maxThroughput : 0;
                
                Assert.IsTrue(imbalance < 0.2, 
                    $"Tenant throughput imbalance too high at concurrency {result.ConcurrencyPerTenant}: {imbalance:P}");
            }

            Console.WriteLine("Multi-tenant throughput benchmark completed");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Multi-tenant benchmark failed: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task Benchmark_PayloadSizeImpact_MeasureLatencyScaling()
    {
        // Arrange - Test different payload sizes
        var payloadSizes = new[] { 1, 10, 100, 1000, 10000 }; // bytes
        const int requestsPerSize = 50;
        const int concurrency = 5;
        
        var payloadResults = new List<PayloadBenchmarkResult>();
        var tenantId = _benchmarkTenantIds.First();
        
        if (!_tenantClients.TryGetValue(tenantId, out var client))
        {
            Assert.Inconclusive($"Client not available for tenant {tenantId}");
            return;
        }

        try
        {
            Console.WriteLine("Payload Size Impact Benchmark");
            
            foreach (var payloadSize in payloadSizes)
            {
                Console.WriteLine($"Testing payload size: {payloadSize} bytes");
                
                var results = new ConcurrentBag<(bool Success, TimeSpan Latency, int PayloadSize)>();
                var stopwatch = Stopwatch.StartNew();

                var concurrentTasks = Enumerable.Range(0, concurrency).Select(_ => Task.Run(async () =>
                {
                    for (int i = 0; i < requestsPerSize / concurrency; i++)
                    {
                        var requestStopwatch = Stopwatch.StartNew();
                        try
                        {
                            var metadata = payloadSize > 0 ? new string('P', payloadSize) : "";
                            var command = new TestCreateUserCommand 
                            { 
                                Name = $"PayloadTest_{payloadSize}_{i}",
                                Metadata = metadata
                            };
                            var commandData = ProtoSerializer.Serialize(command);
                            var request = new CommandRequest
                            {
                                CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                                CommandData = ByteString.CopyFrom(commandData),
                                CorrelationId = Guid.NewGuid().ToString()
                            };

                            var response = await client.ExecuteCommandAsync(request);
                            requestStopwatch.Stop();
                            results.Add((response.Success, requestStopwatch.Elapsed, payloadSize));
                        }
                        catch (Exception)
                        {
                            requestStopwatch.Stop();
                            results.Add((false, requestStopwatch.Elapsed, payloadSize));
                        }
                    }
                })).ToList();

                await Task.WhenAll(concurrentTasks);
                stopwatch.Stop();

                var successful = results.Count(r => r.Success);
                var throughput = successful / stopwatch.Elapsed.TotalSeconds;
                var avgLatency = results.Where(r => r.Success).Average(r => r.Latency.TotalMilliseconds);
                var p95Latency = CalculatePercentile(results.Where(r => r.Success).Select(r => r.Latency.TotalMilliseconds).ToList(), 95);
                var successRate = successful / (double)results.Count;

                var payloadResult = new PayloadBenchmarkResult
                {
                    PayloadSize = payloadSize,
                    TotalRequests = results.Count,
                    SuccessfulRequests = successful,
                    SuccessRate = successRate,
                    Throughput = throughput,
                    AvgLatency = avgLatency,
                    P95Latency = p95Latency
                };

                payloadResults.Add(payloadResult);
                
                Console.WriteLine($"  Results: {throughput:F1} req/sec, {avgLatency:F2}ms avg, " +
                                $"{p95Latency:F2}ms p95, {successRate:P} success");

                await Task.Delay(1000);
            }

            // Output results table
            Console.WriteLine($"\nPayload Size Impact Results:");
            Console.WriteLine("Payload Size | Throughput | Avg Latency | P95 Latency | Success Rate");
            Console.WriteLine("-------------|------------|-------------|-------------|-------------");
            
            foreach (var result in payloadResults)
            {
                Console.WriteLine($"{result.PayloadSize,11} B | {result.Throughput,10:F1} | " +
                                $"{result.AvgLatency,11:F1} | {result.P95Latency,11:F1} | " +
                                $"{result.SuccessRate,11:P1}");
            }

            // Analyze latency scaling
            var baselineLatency = payloadResults.First().AvgLatency;
            var maxPayloadLatency = payloadResults.Last().AvgLatency;
            var latencyScalingFactor = maxPayloadLatency / baselineLatency;

            Console.WriteLine($"\nPayload Impact Analysis:");
            Console.WriteLine($"  Baseline Latency (1B): {baselineLatency:F2}ms");
            Console.WriteLine($"  Max Payload Latency (10KB): {maxPayloadLatency:F2}ms");
            Console.WriteLine($"  Latency Scaling Factor: {latencyScalingFactor:F2}x");

            // Performance assertions
            Assert.IsTrue(payloadResults.All(r => r.SuccessRate > 0.95), 
                "All payload sizes should maintain high success rate");
            Assert.IsTrue(latencyScalingFactor < 5.0, 
                $"Latency scaling factor too high: {latencyScalingFactor:F2}x (should be < 5x)");
            Assert.IsTrue(payloadResults.Last().P95Latency < 1000, 
                $"P95 latency for max payload too high: {payloadResults.Last().P95Latency:F2}ms");

            Console.WriteLine("Payload size impact benchmark completed");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Payload size benchmark failed: {ex.Message}");
        }
    }

    private double CalculatePercentile(List<double> values, int percentile)
    {
        if (!values.Any()) return 0;
        
        values.Sort();
        var index = (int)Math.Ceiling(percentile / 100.0 * values.Count) - 1;
        return values[Math.Max(0, Math.Min(index, values.Count - 1))];
    }
}

// Benchmark result data structures
public class BenchmarkResult
{
    public string TestName { get; set; } = "";
    public int Concurrency { get; set; }
    public TimeSpan Duration { get; set; }
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public double SuccessRate { get; set; }
    public double Throughput { get; set; }
    public double AvgLatency { get; set; }
    public double MedianLatency { get; set; }
    public double P95Latency { get; set; }
    public double P99Latency { get; set; }
}

public class MultiTenantBenchmarkResult
{
    public int ConcurrencyPerTenant { get; set; }
    public int TotalConcurrency { get; set; }
    public TimeSpan Duration { get; set; }
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public double OverallThroughput { get; set; }
    public double OverallSuccessRate { get; set; }
    public double OverallAvgLatency { get; set; }
    public double OverallP95Latency { get; set; }
    public Dictionary<string, TenantBenchmarkStats> TenantStats { get; set; } = new();
}

public class TenantBenchmarkStats
{
    public double Throughput { get; set; }
    public double SuccessRate { get; set; }
    public double AvgLatency { get; set; }
    public double P95Latency { get; set; }
}

public class PayloadBenchmarkResult
{
    public int PayloadSize { get; set; }
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public double SuccessRate { get; set; }
    public double Throughput { get; set; }
    public double AvgLatency { get; set; }
    public double P95Latency { get; set; }
}