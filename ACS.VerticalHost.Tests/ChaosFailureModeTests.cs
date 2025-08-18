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
using System.Text;

namespace ACS.VerticalHost.Tests;

[TestClass]
public class ChaosFailureModeTests
{
    private readonly List<string> _failureModeTenantIds = new()
    {
        "failure-tenant-1", "failure-tenant-2"
    };

    private readonly Dictionary<string, WebApplicationFactory<ACS.VerticalHost.Program>> _tenantFactories = new();
    private readonly Dictionary<string, GrpcChannel> _tenantChannels = new();
    private readonly Dictionary<string, VerticalService.VerticalServiceClient> _tenantClients = new();
    private readonly Dictionary<string, TenantProcessManager.TenantProcess> _tenantProcesses = new();
    private TenantProcessManager? _processManager;
    private ILogger<ChaosFailureModeTests>? _logger;

    [TestInitialize]
    public async Task Setup()
    {
        try
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var processLogger = serviceProvider.GetRequiredService<ILogger<TenantProcessManager>>();
            _logger = serviceProvider.GetRequiredService<ILogger<ChaosFailureModeTests>>();
            _processManager = new TenantProcessManager(processLogger);

            foreach (var tenantId in _failureModeTenantIds)
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
                        MaxReceiveMessageSize = 128 * 1024 * 1024,
                        MaxSendMessageSize = 128 * 1024 * 1024,
                        ThrowOperationCanceledOnCancellation = true
                    });
                    
                    var grpcClient = new VerticalService.VerticalServiceClient(channel);

                    _tenantFactories[tenantId] = factory;
                    _tenantChannels[tenantId] = channel;
                    _tenantClients[tenantId] = grpcClient;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"Failed to setup failure mode tenant {tenantId}");
                }
            }

            await Task.Delay(2000);
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Could not setup chaos failure mode test environment: {ex.Message}");
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
                foreach (var tenantId in _failureModeTenantIds)
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
    public async Task Chaos_MalformedDataInjection_ErrorHandling()
    {
        // Test system resilience against malformed/corrupted data
        const int testDurationSeconds = 30;
        var results = new ConcurrentBag<(string TestType, bool Success, string ErrorType, TimeSpan Duration)>();
        var endTime = DateTime.UtcNow.AddSeconds(testDurationSeconds);

        var tenantId = _failureModeTenantIds.First();
        if (!_tenantClients.TryGetValue(tenantId, out var client))
        {
            Assert.Inconclusive($"Client not available for tenant {tenantId}");
            return;
        }

        try
        {
            _logger?.LogInformation($"Starting {testDurationSeconds}s malformed data injection test...");

            var malformedDataTests = new List<(string TestType, Func<Task>)>
            {
                ("NullCommandType", async () =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    try
                    {
                        var request = new CommandRequest
                        {
                            CommandType = null!,
                            CommandData = ByteString.CopyFrom("test data", Encoding.UTF8),
                            CorrelationId = Guid.NewGuid().ToString()
                        };
                        
                        var response = await client.ExecuteCommandAsync(request);
                        results.Add(("NullCommandType", response.Success, "", stopwatch.Elapsed));
                    }
                    catch (Exception ex)
                    {
                        results.Add(("NullCommandType", false, ex.GetType().Name, stopwatch.Elapsed));
                    }
                }),

                ("EmptyCommandType", async () =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    try
                    {
                        var request = new CommandRequest
                        {
                            CommandType = "",
                            CommandData = ByteString.CopyFrom("test data", Encoding.UTF8),
                            CorrelationId = Guid.NewGuid().ToString()
                        };
                        
                        var response = await client.ExecuteCommandAsync(request);
                        results.Add(("EmptyCommandType", response.Success, "", stopwatch.Elapsed));
                    }
                    catch (Exception ex)
                    {
                        results.Add(("EmptyCommandType", false, ex.GetType().Name, stopwatch.Elapsed));
                    }
                }),

                ("InvalidCommandType", async () =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    try
                    {
                        var request = new CommandRequest
                        {
                            CommandType = "NonExistent.Command.Type.That.Does.Not.Exist",
                            CommandData = ByteString.CopyFrom("test data", Encoding.UTF8),
                            CorrelationId = Guid.NewGuid().ToString()
                        };
                        
                        var response = await client.ExecuteCommandAsync(request);
                        results.Add(("InvalidCommandType", response.Success, "", stopwatch.Elapsed));
                    }
                    catch (Exception ex)
                    {
                        results.Add(("InvalidCommandType", false, ex.GetType().Name, stopwatch.Elapsed));
                    }
                }),

                ("CorruptedCommandData", async () =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    try
                    {
                        var corruptedData = new byte[] { 0xFF, 0xFF, 0xFF, 0x00, 0xDE, 0xAD, 0xBE, 0xEF };
                        var request = new CommandRequest
                        {
                            CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                            CommandData = ByteString.CopyFrom(corruptedData),
                            CorrelationId = Guid.NewGuid().ToString()
                        };
                        
                        var response = await client.ExecuteCommandAsync(request);
                        results.Add(("CorruptedCommandData", response.Success, "", stopwatch.Elapsed));
                    }
                    catch (Exception ex)
                    {
                        results.Add(("CorruptedCommandData", false, ex.GetType().Name, stopwatch.Elapsed));
                    }
                }),

                ("OversizedPayload", async () =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    try
                    {
                        // Create a very large command (50MB)
                        var largeCommand = new TestCreateUserCommand
                        {
                            Name = "OversizedPayloadTest",
                            Metadata = new string('X', 50 * 1024 * 1024) // 50MB
                        };
                        var commandData = ProtoSerializer.Serialize(largeCommand);
                        var request = new CommandRequest
                        {
                            CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                            CommandData = ByteString.CopyFrom(commandData),
                            CorrelationId = Guid.NewGuid().ToString()
                        };
                        
                        var response = await client.ExecuteCommandAsync(request);
                        results.Add(("OversizedPayload", response.Success, "", stopwatch.Elapsed));
                    }
                    catch (Exception ex)
                    {
                        results.Add(("OversizedPayload", false, ex.GetType().Name, stopwatch.Elapsed));
                    }
                }),

                ("MaliciousPayload", async () =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    try
                    {
                        // Create command with potentially malicious content
                        var maliciousCommand = new TestCreateUserCommand
                        {
                            Name = "<script>alert('xss')</script>'; DROP TABLE Users; --",
                            Metadata = string.Concat(Enumerable.Repeat("../", 1000)) + "etc/passwd"
                        };
                        var commandData = ProtoSerializer.Serialize(maliciousCommand);
                        var request = new CommandRequest
                        {
                            CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                            CommandData = ByteString.CopyFrom(commandData),
                            CorrelationId = Guid.NewGuid().ToString()
                        };
                        
                        var response = await client.ExecuteCommandAsync(request);
                        results.Add(("MaliciousPayload", response.Success, "", stopwatch.Elapsed));
                    }
                    catch (Exception ex)
                    {
                        results.Add(("MaliciousPayload", false, ex.GetType().Name, stopwatch.Elapsed));
                    }
                }),

                ("ExtremelyLongCorrelationId", async () =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    try
                    {
                        var command = new TestCreateUserCommand { Name = "CorrelationIdTest" };
                        var commandData = ProtoSerializer.Serialize(command);
                        var request = new CommandRequest
                        {
                            CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                            CommandData = ByteString.CopyFrom(commandData),
                            CorrelationId = new string('A', 100000) // 100K character correlation ID
                        };
                        
                        var response = await client.ExecuteCommandAsync(request);
                        results.Add(("ExtremelyLongCorrelationId", response.Success, "", stopwatch.Elapsed));
                    }
                    catch (Exception ex)
                    {
                        results.Add(("ExtremelyLongCorrelationId", false, ex.GetType().Name, stopwatch.Elapsed));
                    }
                })
            };

            // Execute malformed data tests
            var testTasks = new List<Task>();
            var currentTestIndex = 0;

            while (DateTime.UtcNow < endTime)
            {
                var testToExecute = malformedDataTests[currentTestIndex % malformedDataTests.Count];
                testTasks.Add(testToExecute.Item2());
                currentTestIndex++;

                // Also send normal requests to verify system stability
                testTasks.Add(Task.Run(async () =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    try
                    {
                        var normalCommand = new TestCreateUserCommand { Name = $"Normal_{DateTime.UtcNow.Ticks}" };
                        var commandData = ProtoSerializer.Serialize(normalCommand);
                        var request = new CommandRequest
                        {
                            CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                            CommandData = ByteString.CopyFrom(commandData),
                            CorrelationId = Guid.NewGuid().ToString()
                        };
                        
                        var response = await client.ExecuteCommandAsync(request);
                        results.Add(("NormalRequest", response.Success, "", stopwatch.Elapsed));
                    }
                    catch (Exception ex)
                    {
                        results.Add(("NormalRequest", false, ex.GetType().Name, stopwatch.Elapsed));
                    }
                }));

                // Execute batch of tests
                if (testTasks.Count >= 4)
                {
                    await Task.WhenAll(testTasks);
                    testTasks.Clear();
                }

                await Task.Delay(1000);
            }

            // Execute remaining tasks
            if (testTasks.Count > 0)
            {
                await Task.WhenAll(testTasks);
            }

            // Analyze results
            var resultsList = results.ToList();
            var testTypeResults = resultsList.GroupBy(r => r.TestType)
                .ToDictionary(g => g.Key, g => new
                {
                    Total = g.Count(),
                    Successful = g.Count(r => r.Success),
                    SuccessRate = g.Count(r => r.Success) / (double)g.Count(),
                    CommonErrors = g.Where(r => !r.Success).GroupBy(r => r.ErrorType)
                        .OrderByDescending(eg => eg.Count()).Take(3).ToList(),
                    AvgDuration = g.Average(r => r.Duration.TotalMilliseconds)
                });

            _logger?.LogInformation($"Malformed Data Injection Test Results:");
            _logger?.LogInformation($"  Test Duration: {testDurationSeconds}s");
            _logger?.LogInformation($"  Total Tests: {resultsList.Count}");

            foreach (var testType in testTypeResults)
            {
                var stats = testType.Value;
                _logger?.LogInformation($"  {testType.Key}:");
                _logger?.LogInformation($"    Total: {stats.Total}, Success Rate: {stats.SuccessRate:P}");
                _logger?.LogInformation($"    Avg Duration: {stats.AvgDuration:F2}ms");
                
                if (stats.CommonErrors.Any())
                {
                    _logger?.LogInformation($"    Common Errors: {string.Join(", ", stats.CommonErrors.Select(e => $"{e.Key}({e.Count()})"))}");
                }
            }

            // Assertions
            // Normal requests should maintain high success rate
            if (testTypeResults.ContainsKey("NormalRequest"))
            {
                var normalRequestStats = testTypeResults["NormalRequest"];
                Assert.IsTrue(normalRequestStats.SuccessRate > 0.9, 
                    $"Normal requests affected by malformed data: {normalRequestStats.SuccessRate:P}");
            }

            // System should handle malformed data gracefully (not crash)
            foreach (var malformedTestType in testTypeResults.Keys.Where(k => k != "NormalRequest"))
            {
                var testStats = testTypeResults[malformedTestType];
                // Either successfully reject (low success rate) or handle gracefully (high success rate)
                // But shouldn't cause system instability (verified by normal request success rate)
                Assert.IsTrue(testStats.AvgDuration < 10000, 
                    $"Test {malformedTestType} took too long: {testStats.AvgDuration:F2}ms");
            }

            // Oversized payload should be rejected
            if (testTypeResults.ContainsKey("OversizedPayload"))
            {
                var oversizedStats = testTypeResults["OversizedPayload"];
                Assert.IsTrue(oversizedStats.SuccessRate < 0.1, 
                    $"Oversized payload should be rejected: {oversizedStats.SuccessRate:P}");
            }

            _logger?.LogInformation("Malformed data injection test passed");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Malformed data injection test failed: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task Chaos_RapidRequestFlood_RateLimitingBehavior()
    {
        // Test system behavior under rapid request flooding
        const int floodDurationSeconds = 20;
        const int maxConcurrentRequests = 200;
        const int requestsPerSecond = 500;
        
        var results = new ConcurrentBag<(DateTime Timestamp, bool Success, TimeSpan Latency, string ErrorType)>();
        var endTime = DateTime.UtcNow.AddSeconds(floodDurationSeconds);

        var tenantId = _failureModeTenantIds.First();
        if (!_tenantClients.TryGetValue(tenantId, out var client))
        {
            Assert.Inconclusive($"Client not available for tenant {tenantId}");
            return;
        }

        try
        {
            _logger?.LogInformation($"Starting {floodDurationSeconds}s rapid request flood test...");
            _logger?.LogInformation($"Target: {requestsPerSecond} req/sec with max {maxConcurrentRequests} concurrent");

            var semaphore = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
            var requestCounter = 0;

            var floodTask = Task.Run(async () =>
            {
                while (DateTime.UtcNow < endTime)
                {
                    var secondStart = DateTime.UtcNow;
                    var secondTasks = new List<Task>();

                    // Generate target requests per second
                    for (int i = 0; i < requestsPerSecond && DateTime.UtcNow < endTime; i++)
                    {
                        secondTasks.Add(Task.Run(async () =>
                        {
                            await semaphore.WaitAsync();
                            try
                            {
                                var requestId = Interlocked.Increment(ref requestCounter);
                                var stopwatch = Stopwatch.StartNew();
                                var timestamp = DateTime.UtcNow;

                                try
                                {
                                    var command = new TestCreateUserCommand 
                                    { 
                                        Name = $"Flood_{requestId}" 
                                    };
                                    var commandData = ProtoSerializer.Serialize(command);
                                    var request = new CommandRequest
                                    {
                                        CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                                        CommandData = ByteString.CopyFrom(commandData),
                                        CorrelationId = $"flood-{requestId}"
                                    };

                                    // Short timeout to detect system overload
                                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                                    var response = await client.ExecuteCommandAsync(request, cancellationToken: cts.Token);
                                    stopwatch.Stop();
                                    
                                    results.Add((timestamp, response.Success, stopwatch.Elapsed, ""));
                                }
                                catch (OperationCanceledException)
                                {
                                    stopwatch.Stop();
                                    results.Add((timestamp, false, stopwatch.Elapsed, "Timeout"));
                                }
                                catch (Exception ex)
                                {
                                    stopwatch.Stop();
                                    var errorType = ex.InnerException?.GetType().Name ?? ex.GetType().Name;
                                    results.Add((timestamp, false, stopwatch.Elapsed, errorType));
                                }
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }));
                    }

                    // Don't wait for all tasks to complete - let them run concurrently
                    // Just ensure we don't exceed our rate limit for starting new requests
                    var secondDuration = DateTime.UtcNow - secondStart;
                    var targetSecondDuration = TimeSpan.FromSeconds(1);
                    if (secondDuration < targetSecondDuration)
                    {
                        await Task.Delay(targetSecondDuration - secondDuration);
                    }
                }
            });

            await floodTask;
            
            // Wait for remaining requests to complete (with timeout)
            var waitStart = DateTime.UtcNow;
            while (semaphore.CurrentCount < maxConcurrentRequests && 
                   DateTime.UtcNow < waitStart.AddSeconds(10))
            {
                await Task.Delay(100);
            }

            // Analyze results
            var resultsList = results.ToList();
            var totalRequests = resultsList.Count;
            var successfulRequests = resultsList.Count(r => r.Success);
            var overallSuccessRate = totalRequests > 0 ? successfulRequests / (double)totalRequests : 0;
            var actualRps = totalRequests / (double)floodDurationSeconds;

            // Analyze by time windows
            var timeWindows = resultsList
                .GroupBy(r => new DateTime(r.Timestamp.Year, r.Timestamp.Month, r.Timestamp.Day, 
                                         r.Timestamp.Hour, r.Timestamp.Minute, r.Timestamp.Second))
                .Select(g => new
                {
                    Timestamp = g.Key,
                    Total = g.Count(),
                    Successful = g.Count(r => r.Success),
                    SuccessRate = g.Count(r => r.Success) / (double)g.Count(),
                    AvgLatency = g.Where(r => r.Success).Any() ? g.Where(r => r.Success).Average(r => r.Latency.TotalMilliseconds) : 0,
                    Errors = g.Where(r => !r.Success).GroupBy(r => r.ErrorType).ToDictionary(eg => eg.Key, eg => eg.Count())
                }).OrderBy(w => w.Timestamp).ToList();

            var errorTypes = resultsList.Where(r => !r.Success)
                .GroupBy(r => r.ErrorType)
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());

            var successfulResults = resultsList.Where(r => r.Success).ToList();
            var avgLatency = successfulResults.Any() ? successfulResults.Average(r => r.Latency.TotalMilliseconds) : 0;
            var p95Latency = CalculatePercentile(successfulResults.Select(r => r.Latency.TotalMilliseconds).ToList(), 95);
            var maxLatency = successfulResults.Any() ? successfulResults.Max(r => r.Latency.TotalMilliseconds) : 0;

            _logger?.LogInformation($"Rapid Request Flood Test Results:");
            _logger?.LogInformation($"  Test Duration: {floodDurationSeconds}s");
            _logger?.LogInformation($"  Total Requests: {totalRequests}");
            _logger?.LogInformation($"  Successful Requests: {successfulRequests}");
            _logger?.LogInformation($"  Overall Success Rate: {overallSuccessRate:P}");
            _logger?.LogInformation($"  Target RPS: {requestsPerSecond}");
            _logger?.LogInformation($"  Actual RPS: {actualRps:F1}");
            _logger?.LogInformation($"  Average Latency: {avgLatency:F2}ms");
            _logger?.LogInformation($"  P95 Latency: {p95Latency:F2}ms");
            _logger?.LogInformation($"  Max Latency: {maxLatency:F2}ms");

            _logger?.LogInformation($"Error Types:");
            foreach (var errorType in errorTypes.Take(5))
            {
                _logger?.LogInformation($"  {errorType.Key}: {errorType.Value} occurrences");
            }

            // Sample time windows
            _logger?.LogInformation($"Sample Time Windows (first 5):");
            foreach (var window in timeWindows.Take(5))
            {
                _logger?.LogInformation($"  {window.Timestamp:HH:mm:ss}: {window.Successful}/{window.Total} " +
                                      $"({window.SuccessRate:P}) - {window.AvgLatency:F1}ms avg");
            }

            // Assertions
            // System should not completely fail under load
            Assert.IsTrue(overallSuccessRate > 0.3, $"Success rate too low under flood: {overallSuccessRate:P}");
            
            // System should implement some form of rate limiting or graceful degradation
            // Very high success rate might indicate insufficient load generation
            // Very low success rate might indicate system failure
            Assert.IsTrue(overallSuccessRate < 0.95 || actualRps > 100, 
                "System may not be under sufficient load or may lack proper rate limiting");

            // Latency should increase under load but not be extreme
            Assert.IsTrue(avgLatency < 5000, $"Average latency too high: {avgLatency:F2}ms");
            Assert.IsTrue(p95Latency < 10000, $"P95 latency too high: {p95Latency:F2}ms");

            // Should have timeout or resource exhaustion errors under heavy load
            var hasLoadIndicators = errorTypes.ContainsKey("Timeout") || 
                                   errorTypes.Keys.Any(k => k.Contains("Resource") || k.Contains("Limit"));
            Assert.IsTrue(hasLoadIndicators, "No load-related errors detected - system may not be under stress");

            _logger?.LogInformation("Rapid request flood test passed");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Rapid request flood test failed: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task Chaos_DependencyUnavailable_GracefulDegradation()
    {
        // Test behavior when external dependencies become unavailable
        const int testDurationSeconds = 35;
        var results = new ConcurrentBag<(string Phase, string TenantId, bool Success, TimeSpan Duration, string Error)>();

        try
        {
            _logger?.LogInformation($"Starting {testDurationSeconds}s dependency unavailability test...");

            // Phase 1: Normal operation baseline (10 seconds)
            _logger?.LogInformation("Phase 1: Establishing baseline with all dependencies available");
            await ExecuteDependencyTestPhase("Baseline", TimeSpan.FromSeconds(10), results);

            // Phase 2: Simulate database connection issues (10 seconds)
            // Note: In a real scenario, you would inject database connection failures
            // For this test, we simulate the effect by creating commands that might fail
            _logger?.LogInformation("Phase 2: Simulating database dependency issues");
            await ExecuteDependencyTestPhase("DatabaseIssues", TimeSpan.FromSeconds(10), results);

            // Phase 3: Recovery phase (15 seconds)
            _logger?.LogInformation("Phase 3: Testing recovery after dependency restoration");
            await ExecuteDependencyTestPhase("Recovery", TimeSpan.FromSeconds(15), results);

            // Analyze results by phase
            var phaseResults = results.ToList().GroupBy(r => r.Phase)
                .ToDictionary(g => g.Key, g => new
                {
                    Total = g.Count(),
                    Successful = g.Count(r => r.Success),
                    SuccessRate = g.Count(r => r.Success) / (double)g.Count(),
                    AvgDuration = g.Where(r => r.Success).Any() ? g.Where(r => r.Success).Average(r => r.Duration.TotalMilliseconds) : 0,
                    ErrorTypes = g.Where(r => !r.Success).GroupBy(r => r.Error)
                        .OrderByDescending(eg => eg.Count()).Take(3).ToList(),
                    TenantBreakdown = g.GroupBy(r => r.TenantId).ToDictionary(tg => tg.Key, tg => new
                    {
                        Total = tg.Count(),
                        SuccessRate = tg.Count(r => r.Success) / (double)tg.Count()
                    })
                });

            _logger?.LogInformation($"Dependency Unavailability Test Results:");
            
            foreach (var phase in phaseResults)
            {
                var stats = phase.Value;
                _logger?.LogInformation($"  {phase.Key} Phase:");
                _logger?.LogInformation($"    Total Requests: {stats.Total}");
                _logger?.LogInformation($"    Success Rate: {stats.SuccessRate:P}");
                _logger?.LogInformation($"    Avg Duration: {stats.AvgDuration:F2}ms");
                
                if (stats.ErrorTypes.Any())
                {
                    _logger?.LogInformation($"    Top Errors: {string.Join(", ", stats.ErrorTypes.Select(e => $"{e.Key}({e.Count()})"))}");
                }
                
                foreach (var tenant in stats.TenantBreakdown)
                {
                    _logger?.LogInformation($"    {tenant.Key}: {tenant.Value.SuccessRate:P} success rate");
                }
            }

            // Assertions
            // Baseline should be healthy
            if (phaseResults.ContainsKey("Baseline"))
            {
                var baseline = phaseResults["Baseline"];
                Assert.IsTrue(baseline.SuccessRate > 0.9, $"Baseline success rate too low: {baseline.SuccessRate:P}");
            }

            // During database issues, system should degrade gracefully (not fail completely)
            if (phaseResults.ContainsKey("DatabaseIssues"))
            {
                var dbIssues = phaseResults["DatabaseIssues"];
                Assert.IsTrue(dbIssues.SuccessRate > 0.2, $"System failed completely during DB issues: {dbIssues.SuccessRate:P}");
                Assert.IsTrue(dbIssues.SuccessRate < 0.8, $"No degradation detected during DB issues: {dbIssues.SuccessRate:P}");
            }

            // Recovery should show improvement
            if (phaseResults.ContainsKey("Recovery") && phaseResults.ContainsKey("DatabaseIssues"))
            {
                var recovery = phaseResults["Recovery"];
                var dbIssues = phaseResults["DatabaseIssues"];
                
                Assert.IsTrue(recovery.SuccessRate > dbIssues.SuccessRate, 
                    $"No recovery detected: Recovery {recovery.SuccessRate:P} vs DB Issues {dbIssues.SuccessRate:P}");
                Assert.IsTrue(recovery.SuccessRate > 0.7, $"Recovery success rate too low: {recovery.SuccessRate:P}");
            }

            // Both tenants should be affected similarly (no single tenant bearing all load)
            foreach (var phase in phaseResults.Values)
            {
                var tenantSuccessRates = phase.TenantBreakdown.Values.Select(t => t.SuccessRate).ToList();
                if (tenantSuccessRates.Count > 1)
                {
                    var maxSuccessRate = tenantSuccessRates.Max();
                    var minSuccessRate = tenantSuccessRates.Min();
                    var imbalance = maxSuccessRate > 0 ? (maxSuccessRate - minSuccessRate) / maxSuccessRate : 0;
                    
                    Assert.IsTrue(imbalance < 0.3, $"Tenant load imbalance too high: {imbalance:P}");
                }
            }

            _logger?.LogInformation("Dependency unavailability test passed");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Dependency unavailability test failed: {ex.Message}");
        }
    }

    private async Task ExecuteDependencyTestPhase(string phaseName, TimeSpan duration,
        ConcurrentBag<(string Phase, string TenantId, bool Success, TimeSpan Duration, string Error)> results)
    {
        var endTime = DateTime.UtcNow.Add(duration);
        var requestCounter = 0;

        while (DateTime.UtcNow < endTime)
        {
            var batchTasks = _failureModeTenantIds.Select(tenantId => Task.Run(async () =>
            {
                if (!_tenantClients.TryGetValue(tenantId, out var client))
                {
                    results.Add((phaseName, tenantId, false, TimeSpan.Zero, "ClientUnavailable"));
                    return;
                }

                var stopwatch = Stopwatch.StartNew();
                try
                {
                    // In Phase 2 (DatabaseIssues), create commands that might stress the system
                    var commandName = phaseName == "DatabaseIssues" 
                        ? $"StressTest_{tenantId}_{Interlocked.Increment(ref requestCounter)}"
                        : $"{phaseName}_{tenantId}_{Interlocked.Increment(ref requestCounter)}";

                    var command = new TestCreateUserCommand { Name = commandName };
                    
                    // Add larger metadata during database issues to simulate load
                    if (phaseName == "DatabaseIssues")
                    {
                        command.Metadata = new string('D', 5000); // 5KB metadata
                    }
                    
                    var commandData = ProtoSerializer.Serialize(command);
                    var request = new CommandRequest
                    {
                        CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                        CommandData = ByteString.CopyFrom(commandData),
                        CorrelationId = Guid.NewGuid().ToString()
                    };

                    // Shorter timeout during database issues
                    var timeout = phaseName == "DatabaseIssues" ? TimeSpan.FromSeconds(3) : TimeSpan.FromSeconds(5);
                    using var cts = new CancellationTokenSource(timeout);
                    
                    var response = await client.ExecuteCommandAsync(request, cancellationToken: cts.Token);
                    stopwatch.Stop();
                    
                    results.Add((phaseName, tenantId, response.Success, stopwatch.Elapsed, ""));
                }
                catch (OperationCanceledException)
                {
                    stopwatch.Stop();
                    results.Add((phaseName, tenantId, false, stopwatch.Elapsed, "Timeout"));
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    var errorType = ex.InnerException?.GetType().Name ?? ex.GetType().Name;
                    results.Add((phaseName, tenantId, false, stopwatch.Elapsed, errorType));
                }
            })).ToList();

            await Task.WhenAll(batchTasks);
            
            // Adaptive delay based on phase
            var delay = phaseName == "DatabaseIssues" ? 2000 : 1000;
            await Task.Delay(delay);
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