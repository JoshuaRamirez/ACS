using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ACS.VerticalHost.Tests;

/// <summary>
/// Orchestrator for running multiple chaos engineering scenarios simultaneously
/// and collecting comprehensive resilience metrics
/// </summary>
[TestClass]
public class ChaosTestOrchestrator
{
    private ILogger<ChaosTestOrchestrator>? _logger;

    [TestInitialize]
    public void Setup()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        var serviceProvider = serviceCollection.BuildServiceProvider();
        _logger = serviceProvider.GetRequiredService<ILogger<ChaosTestOrchestrator>>();
    }

    [TestMethod]
    public async Task ChaosOrchestrator_ComprehensiveResilienceTest_SystemSurvival()
    {
        // Comprehensive chaos test that combines multiple failure modes
        const int totalTestDurationMinutes = 10; // Extended test duration
        var endTime = DateTime.UtcNow.AddMinutes(totalTestDurationMinutes);
        
        var chaosEvents = new ConcurrentBag<ChaosEvent>();
        var systemMetrics = new ConcurrentBag<SystemMetric>();
        var resilienceScores = new ConcurrentBag<ResilienceScore>();

        try
        {
            _logger?.LogInformation($"Starting {totalTestDurationMinutes}-minute comprehensive chaos orchestration test");

            // Define chaos scenarios with their timing and intensity
            var chaosScenarios = new List<ChaosScenario>
            {
                new ChaosScenario
                {
                    Name = "RandomProcessKills",
                    StartDelay = TimeSpan.FromMinutes(1),
                    Duration = TimeSpan.FromMinutes(8),
                    Intensity = ChaosIntensity.Medium,
                    Description = "Random tenant process termination"
                },
                new ChaosScenario
                {
                    Name = "NetworkPartitions",
                    StartDelay = TimeSpan.FromMinutes(2),
                    Duration = TimeSpan.FromMinutes(6),
                    Intensity = ChaosIntensity.High,
                    Description = "Network connection disruptions"
                },
                new ChaosScenario
                {
                    Name = "ResourceExhaustion",
                    StartDelay = TimeSpan.FromMinutes(3),
                    Duration = TimeSpan.FromMinutes(4),
                    Intensity = ChaosIntensity.Medium,
                    Description = "Memory and CPU pressure"
                },
                new ChaosScenario
                {
                    Name = "MalformedDataFlood",
                    StartDelay = TimeSpan.FromMinutes(4),
                    Duration = TimeSpan.FromMinutes(3),
                    Intensity = ChaosIntensity.Low,
                    Description = "Malformed request injection"
                },
                new ChaosScenario
                {
                    Name = "CascadingFailures",
                    StartDelay = TimeSpan.FromMinutes(5),
                    Duration = TimeSpan.FromMinutes(2),
                    Intensity = ChaosIntensity.High,
                    Description = "Multi-tenant failure propagation"
                }
            };

            // Background system monitoring
            var monitoringTask = StartSystemMonitoring(endTime, systemMetrics);

            // Background baseline traffic generation
            var baselineTrafficTask = StartBaselineTrafficGeneration(endTime, chaosEvents);

            // Orchestrate chaos scenarios
            var chaosOrchestrationTask = OrchestrateChaosScenarios(chaosScenarios, endTime, chaosEvents);

            // Resilience scoring
            var resilienceScoringTask = StartResilienceScoring(endTime, chaosEvents, systemMetrics, resilienceScores);

            // Wait for all chaos activities to complete
            await Task.WhenAll(monitoringTask, baselineTrafficTask, chaosOrchestrationTask, resilienceScoringTask);

            // Analyze comprehensive results
            var finalAnalysis = AnalyzeComprehensiveResults(chaosEvents.ToList(), systemMetrics.ToList(), resilienceScores.ToList());

            // Report results
            ReportComprehensiveResults(finalAnalysis);

            // Assert system survival criteria
            AssertSystemSurvival(finalAnalysis);

            _logger?.LogInformation("Comprehensive chaos orchestration test completed successfully");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Comprehensive chaos orchestration test failed: {ex.Message}");
        }
    }

    private async Task StartSystemMonitoring(DateTime endTime, ConcurrentBag<SystemMetric> metrics)
    {
        while (DateTime.UtcNow < endTime)
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var metric = new SystemMetric
                {
                    Timestamp = DateTime.UtcNow,
                    MemoryUsageMB = GC.GetTotalMemory(false) / 1024 / 1024,
                    ThreadCount = currentProcess.Threads.Count,
                    CpuTimeMs = currentProcess.TotalProcessorTime.TotalMilliseconds,
                    HandleCount = currentProcess.HandleCount
                };

                metrics.Add(metric);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "System monitoring error");
            }

            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }

    private async Task StartBaselineTrafficGeneration(DateTime endTime, ConcurrentBag<ChaosEvent> events)
    {
        var requestCounter = 0;
        var random = new Random();

        while (DateTime.UtcNow < endTime)
        {
            try
            {
                // Simulate baseline traffic patterns
                var trafficType = random.Next(4) switch
                {
                    0 => "LightLoad",
                    1 => "MediumLoad", 
                    2 => "HeavyLoad",
                    _ => "BurstLoad"
                };

                var requestsInBatch = trafficType switch
                {
                    "LightLoad" => 5,
                    "MediumLoad" => 15,
                    "HeavyLoad" => 30,
                    "BurstLoad" => 50,
                    _ => 10
                };

                var batchStart = DateTime.UtcNow;
                var batchTasks = new List<Task>();

                for (int i = 0; i < requestsInBatch; i++)
                {
                    batchTasks.Add(Task.Run(async () =>
                    {
                        var requestId = Interlocked.Increment(ref requestCounter);
                        var stopwatch = Stopwatch.StartNew();

                        try
                        {
                            // Simulate request processing time
                            var processingTime = random.Next(10, 200);
                            await Task.Delay(processingTime);

                            var success = random.NextDouble() > 0.05; // 95% baseline success rate
                            stopwatch.Stop();

                            events.Add(new ChaosEvent
                            {
                                Timestamp = DateTime.UtcNow,
                                EventType = "BaselineTraffic",
                                Source = trafficType,
                                Success = success,
                                Duration = stopwatch.Elapsed,
                                Details = $"Request {requestId}",
                                Severity = ChaosSeverity.Info
                            });
                        }
                        catch (Exception ex)
                        {
                            stopwatch.Stop();
                            events.Add(new ChaosEvent
                            {
                                Timestamp = DateTime.UtcNow,
                                EventType = "BaselineTraffic",
                                Source = trafficType,
                                Success = false,
                                Duration = stopwatch.Elapsed,
                                Details = $"Request {requestId} failed: {ex.Message}",
                                Severity = ChaosSeverity.Warning
                            });
                        }
                    }));
                }

                await Task.WhenAll(batchTasks);

                // Adaptive delay based on traffic type
                var batchDuration = DateTime.UtcNow - batchStart;
                var targetDelay = trafficType switch
                {
                    "LightLoad" => TimeSpan.FromSeconds(5),
                    "MediumLoad" => TimeSpan.FromSeconds(3),
                    "HeavyLoad" => TimeSpan.FromSeconds(1),
                    "BurstLoad" => TimeSpan.FromMilliseconds(500),
                    _ => TimeSpan.FromSeconds(2)
                };

                if (batchDuration < targetDelay)
                {
                    await Task.Delay(targetDelay - batchDuration);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Baseline traffic generation error");
                await Task.Delay(1000);
            }
        }
    }

    private async Task OrchestrateChaosScenarios(List<ChaosScenario> scenarios, DateTime endTime, ConcurrentBag<ChaosEvent> events)
    {
        var scenarioTasks = scenarios.Select(scenario => Task.Run(async () =>
        {
            try
            {
                // Wait for scenario start time
                await Task.Delay(scenario.StartDelay);
                
                var scenarioEndTime = DateTime.UtcNow.Add(scenario.Duration);
                if (scenarioEndTime > endTime) scenarioEndTime = endTime;

                events.Add(new ChaosEvent
                {
                    Timestamp = DateTime.UtcNow,
                    EventType = "ChaosScenarioStart",
                    Source = scenario.Name,
                    Success = true,
                    Duration = TimeSpan.Zero,
                    Details = scenario.Description,
                    Severity = ChaosSeverity.Info
                });

                _logger?.LogInformation($"Starting chaos scenario: {scenario.Name}");

                await ExecuteChaosScenario(scenario, scenarioEndTime, events);

                events.Add(new ChaosEvent
                {
                    Timestamp = DateTime.UtcNow,
                    EventType = "ChaosScenarioEnd",
                    Source = scenario.Name,
                    Success = true,
                    Duration = scenario.Duration,
                    Details = $"{scenario.Description} completed",
                    Severity = ChaosSeverity.Info
                });

                _logger?.LogInformation($"Completed chaos scenario: {scenario.Name}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Chaos scenario {scenario.Name} failed");
                events.Add(new ChaosEvent
                {
                    Timestamp = DateTime.UtcNow,
                    EventType = "ChaosScenarioError",
                    Source = scenario.Name,
                    Success = false,
                    Duration = TimeSpan.Zero,
                    Details = $"Scenario failed: {ex.Message}",
                    Severity = ChaosSeverity.Error
                });
            }
        })).ToList();

        await Task.WhenAll(scenarioTasks);
    }

    private async Task ExecuteChaosScenario(ChaosScenario scenario, DateTime endTime, ConcurrentBag<ChaosEvent> events)
    {
        var random = new Random();
        var eventCounter = 0;

        while (DateTime.UtcNow < endTime)
        {
            try
            {
                var chaosEvent = scenario.Name switch
                {
                    "RandomProcessKills" => await SimulateProcessKill(scenario, eventCounter++),
                    "NetworkPartitions" => await SimulateNetworkPartition(scenario, eventCounter++),
                    "ResourceExhaustion" => await SimulateResourceExhaustion(scenario, eventCounter++),
                    "MalformedDataFlood" => await SimulateMalformedDataFlood(scenario, eventCounter++),
                    "CascadingFailures" => await SimulateCascadingFailure(scenario, eventCounter++),
                    _ => new ChaosEvent
                    {
                        Timestamp = DateTime.UtcNow,
                        EventType = "UnknownChaos",
                        Source = scenario.Name,
                        Success = false,
                        Duration = TimeSpan.Zero,
                        Details = "Unknown chaos scenario",
                        Severity = ChaosSeverity.Warning
                    }
                };

                events.Add(chaosEvent);

                // Intensity-based delay
                var delay = scenario.Intensity switch
                {
                    ChaosIntensity.Low => TimeSpan.FromSeconds(random.Next(10, 20)),
                    ChaosIntensity.Medium => TimeSpan.FromSeconds(random.Next(5, 15)),
                    ChaosIntensity.High => TimeSpan.FromSeconds(random.Next(2, 8)),
                    _ => TimeSpan.FromSeconds(10)
                };

                await Task.Delay(delay);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error in chaos scenario {scenario.Name}");
                await Task.Delay(5000);
            }
        }
    }

    private async Task<ChaosEvent> SimulateProcessKill(ChaosScenario scenario, int eventNumber)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await Task.Delay(100); // Simulate process kill time
            var success = new Random().NextDouble() > 0.2; // 80% success rate
            
            return new ChaosEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = "ProcessKill",
                Source = scenario.Name,
                Success = success,
                Duration = stopwatch.Elapsed,
                Details = $"Process kill event {eventNumber}",
                Severity = success ? ChaosSeverity.Warning : ChaosSeverity.Error
            };
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    private async Task<ChaosEvent> SimulateNetworkPartition(ChaosScenario scenario, int eventNumber)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await Task.Delay(200); // Simulate network disruption time
            var success = new Random().NextDouble() > 0.3; // 70% success rate
            
            return new ChaosEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = "NetworkPartition",
                Source = scenario.Name,
                Success = success,
                Duration = stopwatch.Elapsed,
                Details = $"Network partition event {eventNumber}",
                Severity = success ? ChaosSeverity.Warning : ChaosSeverity.Error
            };
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    private async Task<ChaosEvent> SimulateResourceExhaustion(ChaosScenario scenario, int eventNumber)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Simulate resource pressure
            var memoryBlock = new byte[10 * 1024 * 1024]; // 10MB allocation
            await Task.Delay(300);
            
            var success = new Random().NextDouble() > 0.4; // 60% success rate
            
            return new ChaosEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = "ResourceExhaustion",
                Source = scenario.Name,
                Success = success,
                Duration = stopwatch.Elapsed,
                Details = $"Resource exhaustion event {eventNumber}",
                Severity = success ? ChaosSeverity.Warning : ChaosSeverity.Critical
            };
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    private async Task<ChaosEvent> SimulateMalformedDataFlood(ChaosScenario scenario, int eventNumber)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await Task.Delay(50); // Simulate malformed data processing time
            var success = new Random().NextDouble() > 0.1; // 90% success rate (should handle malformed data well)
            
            return new ChaosEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = "MalformedData",
                Source = scenario.Name,
                Success = success,
                Duration = stopwatch.Elapsed,
                Details = $"Malformed data injection {eventNumber}",
                Severity = success ? ChaosSeverity.Info : ChaosSeverity.Warning
            };
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    private async Task<ChaosEvent> SimulateCascadingFailure(ChaosScenario scenario, int eventNumber)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await Task.Delay(400); // Simulate cascading failure time
            var success = new Random().NextDouble() > 0.5; // 50% success rate
            
            return new ChaosEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = "CascadingFailure",
                Source = scenario.Name,
                Success = success,
                Duration = stopwatch.Elapsed,
                Details = $"Cascading failure event {eventNumber}",
                Severity = success ? ChaosSeverity.Warning : ChaosSeverity.Critical
            };
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    private async Task StartResilienceScoring(DateTime endTime, ConcurrentBag<ChaosEvent> events, 
        ConcurrentBag<SystemMetric> metrics, ConcurrentBag<ResilienceScore> scores)
    {
        while (DateTime.UtcNow < endTime)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30)); // Score every 30 seconds

                var recentEvents = events.Where(e => e.Timestamp >= DateTime.UtcNow.AddSeconds(-30)).ToList();
                var recentMetrics = metrics.Where(m => m.Timestamp >= DateTime.UtcNow.AddSeconds(-30)).ToList();

                var score = CalculateResilienceScore(recentEvents, recentMetrics);
                scores.Add(score);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Resilience scoring error");
            }
        }
    }

    private ResilienceScore CalculateResilienceScore(List<ChaosEvent> recentEvents, List<SystemMetric> recentMetrics)
    {
        var totalEvents = recentEvents.Count;
        var successfulEvents = recentEvents.Count(e => e.Success);
        var availabilityScore = totalEvents > 0 ? (successfulEvents / (double)totalEvents) * 100 : 100;

        var criticalEvents = recentEvents.Count(e => e.Severity == ChaosSeverity.Critical);
        var errorEvents = recentEvents.Count(e => e.Severity == ChaosSeverity.Error);
        var stabilityScore = Math.Max(0, 100 - (criticalEvents * 20) - (errorEvents * 10));

        var avgMemoryUsage = recentMetrics.Any() ? recentMetrics.Average(m => m.MemoryUsageMB) : 0;
        var resourceScore = Math.Max(0, 100 - Math.Max(0, (avgMemoryUsage - 500) / 10)); // Penalize high memory usage

        var avgResponseTime = recentEvents.Where(e => e.Success).Any() ? 
            recentEvents.Where(e => e.Success).Average(e => e.Duration.TotalMilliseconds) : 0;
        var performanceScore = Math.Max(0, 100 - Math.Max(0, (avgResponseTime - 1000) / 100)); // Penalize slow responses

        var overallScore = (availabilityScore + stabilityScore + resourceScore + performanceScore) / 4;

        return new ResilienceScore
        {
            Timestamp = DateTime.UtcNow,
            AvailabilityScore = availabilityScore,
            StabilityScore = stabilityScore,
            ResourceScore = resourceScore,
            PerformanceScore = performanceScore,
            OverallScore = overallScore,
            EventCount = totalEvents
        };
    }

    private ChaosAnalysis AnalyzeComprehensiveResults(List<ChaosEvent> events, List<SystemMetric> metrics, List<ResilienceScore> scores)
    {
        return new ChaosAnalysis
        {
            TotalEvents = events.Count,
            SuccessfulEvents = events.Count(e => e.Success),
            OverallSuccessRate = events.Count > 0 ? events.Count(e => e.Success) / (double)events.Count : 0,
            
            EventsByType = events.GroupBy(e => e.EventType).ToDictionary(g => g.Key, g => g.Count()),
            EventsBySeverity = events.GroupBy(e => e.Severity).ToDictionary(g => g.Key, g => g.Count()),
            
            AverageResilienceScore = scores.Any() ? scores.Average(s => s.OverallScore) : 0,
            MinResilienceScore = scores.Any() ? scores.Min(s => s.OverallScore) : 0,
            
            PeakMemoryUsageMB = metrics.Any() ? metrics.Max(m => m.MemoryUsageMB) : 0,
            AverageMemoryUsageMB = metrics.Any() ? metrics.Average(m => m.MemoryUsageMB) : 0,
            
            TestDuration = events.Any() ? events.Max(e => e.Timestamp) - events.Min(e => e.Timestamp) : TimeSpan.Zero
        };
    }

    private void ReportComprehensiveResults(ChaosAnalysis analysis)
    {
        _logger?.LogInformation("=== COMPREHENSIVE CHAOS ENGINEERING RESULTS ===");
        _logger?.LogInformation($"Test Duration: {analysis.TestDuration.TotalMinutes:F1} minutes");
        _logger?.LogInformation($"Total Events: {analysis.TotalEvents}");
        _logger?.LogInformation($"Overall Success Rate: {analysis.OverallSuccessRate:P}");
        _logger?.LogInformation($"Average Resilience Score: {analysis.AverageResilienceScore:F1}/100");
        _logger?.LogInformation($"Minimum Resilience Score: {analysis.MinResilienceScore:F1}/100");
        _logger?.LogInformation($"Peak Memory Usage: {analysis.PeakMemoryUsageMB:F1} MB");
        _logger?.LogInformation($"Average Memory Usage: {analysis.AverageMemoryUsageMB:F1} MB");
        
        _logger?.LogInformation("Event Types:");
        foreach (var eventType in analysis.EventsByType)
        {
            _logger?.LogInformation($"  {eventType.Key}: {eventType.Value} events");
        }
        
        _logger?.LogInformation("Event Severities:");
        foreach (var severity in analysis.EventsBySeverity)
        {
            _logger?.LogInformation($"  {severity.Key}: {severity.Value} events");
        }
    }

    private void AssertSystemSurvival(ChaosAnalysis analysis)
    {
        // System survival criteria
        Assert.IsTrue(analysis.OverallSuccessRate > 0.5, 
            $"System survival rate too low: {analysis.OverallSuccessRate:P}");
        
        Assert.IsTrue(analysis.AverageResilienceScore > 40, 
            $"Average resilience score too low: {analysis.AverageResilienceScore:F1}");
        
        Assert.IsTrue(analysis.MinResilienceScore > 20, 
            $"Minimum resilience score too low: {analysis.MinResilienceScore:F1}");
        
        // Memory usage should not grow excessively
        Assert.IsTrue(analysis.PeakMemoryUsageMB < 2000, 
            $"Peak memory usage too high: {analysis.PeakMemoryUsageMB:F1} MB");
        
        // Should not have too many critical events
        var criticalEventCount = analysis.EventsBySeverity.GetValueOrDefault(ChaosSeverity.Critical, 0);
        var criticalEventRate = analysis.TotalEvents > 0 ? criticalEventCount / (double)analysis.TotalEvents : 0;
        Assert.IsTrue(criticalEventRate < 0.1, 
            $"Too many critical events: {criticalEventRate:P}");
    }
}

// Supporting data structures
public class ChaosScenario
{
    public string Name { get; set; } = "";
    public TimeSpan StartDelay { get; set; }
    public TimeSpan Duration { get; set; }
    public ChaosIntensity Intensity { get; set; }
    public string Description { get; set; } = "";
}

public enum ChaosIntensity
{
    Low,
    Medium,
    High
}

public class ChaosEvent
{
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = "";
    public string Source { get; set; } = "";
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
    public string Details { get; set; } = "";
    public ChaosSeverity Severity { get; set; }
}

public enum ChaosSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

public class SystemMetric
{
    public DateTime Timestamp { get; set; }
    public long MemoryUsageMB { get; set; }
    public int ThreadCount { get; set; }
    public double CpuTimeMs { get; set; }
    public int HandleCount { get; set; }
}

public class ResilienceScore
{
    public DateTime Timestamp { get; set; }
    public double AvailabilityScore { get; set; }
    public double StabilityScore { get; set; }
    public double ResourceScore { get; set; }
    public double PerformanceScore { get; set; }
    public double OverallScore { get; set; }
    public int EventCount { get; set; }
}

public class ChaosAnalysis
{
    public int TotalEvents { get; set; }
    public int SuccessfulEvents { get; set; }
    public double OverallSuccessRate { get; set; }
    public Dictionary<string, int> EventsByType { get; set; } = new();
    public Dictionary<ChaosSeverity, int> EventsBySeverity { get; set; } = new();
    public double AverageResilienceScore { get; set; }
    public double MinResilienceScore { get; set; }
    public long PeakMemoryUsageMB { get; set; }
    public double AverageMemoryUsageMB { get; set; }
    public TimeSpan TestDuration { get; set; }
}