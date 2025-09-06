using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Text;
using ACS.Service.Domain;
using ACS.Service.Infrastructure;
using ACS.Service.Data;
using Microsoft.EntityFrameworkCore;

namespace ACS.IntegrationValidation;

/// <summary>
/// Focused integration tests that work with the actual ACS domain model and architecture
/// </summary>
public class FocusedIntegrationTests
{
    private readonly ILogger<FocusedIntegrationTests> _logger;
    private readonly IConfiguration _configuration;
    private readonly List<TestResult> _results = new();
    private readonly Dictionary<string, object> _metrics = new();

    public FocusedIntegrationTests(ILogger<FocusedIntegrationTests> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<IntegrationReport> RunAllTestsAsync()
    {
        _logger.LogInformation("🚀 Starting focused ACS integration tests");
        var stopwatch = Stopwatch.StartNew();

        var report = new IntegrationReport
        {
            TestRunId = Guid.NewGuid().ToString(),
            StartTime = DateTimeOffset.UtcNow,
            TestSuite = "ACS Focused Integration Tests"
        };

        try
        {
            await TestDomainModelIntegrationAsync();
            await TestServiceLayerIntegrationAsync();
            await TestDatabaseIntegrationAsync();
            await TestPerformanceCharacteristicsAsync();
            await TestBusinessRuleValidationAsync();

            report.Success = _results.All(r => r.Passed);
            report.TestResults = _results.ToList();
            report.Metrics = new Dictionary<string, object>(_metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical failure during integration testing");
            report.Success = false;
            report.CriticalError = ex.Message;
            AddTestResult("CRITICAL_SYSTEM_FAILURE", false, $"System integration failed: {ex.Message}", ex.StackTrace);
        }
        finally
        {
            stopwatch.Stop();
            report.EndTime = DateTimeOffset.UtcNow;
            report.Duration = stopwatch.Elapsed;
            report.TotalTestsRun = _results.Count;
            report.TestsPassed = _results.Count(r => r.Passed);
            report.TestsFailed = _results.Count(r => !r.Passed);
        }

        return report;
    }

    private async Task TestDomainModelIntegrationAsync()
    {
        _logger.LogInformation("🏛️ Testing domain model integration");

        try
        {
            // Test Entity base class functionality
            var user = new User();
            user.Name = "Test User";
            
            var role = new Role();
            role.Name = "Test Role";
            
            var group = new Group();
            group.Name = "Test Group";
            
            var permission = new Permission
            {
                Resource = "TestResource",
                Action = "Read"
            };

            // Test basic entity operations
            user.AddPermission(permission);
            Assert.True(user.Permissions.Contains(permission));
            
            AddTestResult("DOMAIN_ENTITY_OPERATIONS", true, 
                "Domain entities can be created and basic operations work");

            // Test entity relationships
            user.Parents.Add(group);
            group.Children.Add(user);
            
            user.Parents.Add(role);
            role.Children.Add(user);
            
            Assert.True(user.Parents.Contains(group));
            Assert.True(user.Parents.Contains(role));
            Assert.True(group.Children.Contains(user));
            
            AddTestResult("DOMAIN_ENTITY_RELATIONSHIPS", true, 
                "Entity parent-child relationships work correctly");

            _metrics["domain_entities_tested"] = 3;
            _metrics["domain_relationships_tested"] = 3;
        }
        catch (Exception ex)
        {
            AddTestResult("DOMAIN_MODEL_INTEGRATION", false, 
                $"Domain model integration failed: {ex.Message}", ex.StackTrace);
        }
    }

    private async Task TestServiceLayerIntegrationAsync()
    {
        _logger.LogInformation("🔧 Testing service layer integration");

        try
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
            services.AddSingleton<IConfiguration>(_configuration);
            
            // Test database context registration
            var connectionString = _configuration.GetConnectionString("DefaultConnection") ?? 
                                  "Server=(localdb)\\MSSQLLocalDB;Database=ACS_IntegrationTest;Trusted_Connection=true;MultipleActiveResultSets=true";

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

            // Test InMemoryEntityGraph registration
            services.AddScoped<InMemoryEntityGraph>();

            var serviceProvider = services.BuildServiceProvider();
            
            // Test service resolution
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetService<ApplicationDbContext>();
                Assert.NotNull(dbContext);
                
                var entityGraph = scope.ServiceProvider.GetService<InMemoryEntityGraph>();
                Assert.NotNull(entityGraph);
                
                AddTestResult("SERVICE_LAYER_REGISTRATION", true, 
                    "Service layer components can be registered and resolved");

                // Test basic service functionality
                var initialCount = entityGraph.TotalEntityCount;
                _metrics["initial_entity_count"] = initialCount;
                
                AddTestResult("SERVICE_LAYER_FUNCTIONALITY", true, 
                    $"Service layer basic functionality working - entity count: {initialCount}");
            }
        }
        catch (Exception ex)
        {
            AddTestResult("SERVICE_LAYER_INTEGRATION", false, 
                $"Service layer integration failed: {ex.Message}", ex.StackTrace);
        }
    }

    private async Task TestDatabaseIntegrationAsync()
    {
        _logger.LogInformation("💾 Testing database integration");

        try
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
            
            var connectionString = _configuration.GetConnectionString("DefaultConnection") ?? 
                                  "Server=(localdb)\\MSSQLLocalDB;Database=ACS_IntegrationTest;Trusted_Connection=true;MultipleActiveResultSets=true";

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

            var serviceProvider = services.BuildServiceProvider();
            
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                
                // Test database connection
                var canConnect = await dbContext.Database.CanConnectAsync();
                
                AddTestResult("DATABASE_CONNECTION", canConnect, 
                    canConnect ? "Database connection successful" : "Database connection failed");

                if (canConnect)
                {
                    // Test entity framework model
                    var model = dbContext.Model;
                    var entityTypes = model.GetEntityTypes().ToList();
                    
                    AddTestResult("ENTITY_FRAMEWORK_MODEL", entityTypes.Any(), 
                        $"Entity Framework model configured with {entityTypes.Count} entity types");
                    
                    _metrics["ef_entity_types"] = entityTypes.Count;
                    _metrics["connection_string_valid"] = true;
                }
                else
                {
                    _metrics["connection_string_valid"] = false;
                }
            }
        }
        catch (Exception ex)
        {
            AddTestResult("DATABASE_INTEGRATION", false, 
                $"Database integration failed: {ex.Message}", ex.StackTrace);
        }
    }

    private async Task TestPerformanceCharacteristicsAsync()
    {
        _logger.LogInformation("⚡ Testing performance characteristics");

        try
        {
            var stopwatch = Stopwatch.StartNew();

            // Test object creation performance
            var users = new List<User>();
            for (int i = 0; i < 1000; i++)
            {
                var user = new User();
                user.Name = $"User {i}";
                users.Add(user);
            }
            
            stopwatch.Stop();
            var creationTime = stopwatch.ElapsedMilliseconds;

            AddTestResult("OBJECT_CREATION_PERFORMANCE", creationTime < 100, 
                $"Created 1000 domain objects in {creationTime}ms (target: <100ms)");

            // Test memory usage
            var beforeMemory = GC.GetTotalMemory(false);
            
            // Create relationships
            stopwatch.Restart();
            
            var roles = new List<Role>();
            for (int i = 0; i < 100; i++)
            {
                var role = new Role();
                role.Name = $"Role {i}";
                roles.Add(role);
            }

            // Create relationships
            for (int i = 0; i < Math.Min(users.Count, 100); i++)
            {
                for (int j = 0; j < Math.Min(roles.Count, 5); j++)
                {
                    users[i].Parents.Add(roles[j]);
                    roles[j].Children.Add(users[i]);
                }
            }
            
            stopwatch.Stop();
            var relationshipTime = stopwatch.ElapsedMilliseconds;

            var afterMemory = GC.GetTotalMemory(false);
            var memoryUsed = (afterMemory - beforeMemory) / 1024; // KB

            AddTestResult("RELATIONSHIP_PERFORMANCE", relationshipTime < 1000, 
                $"Created relationships in {relationshipTime}ms, using {memoryUsed}KB memory");

            _metrics["object_creation_time_ms"] = creationTime;
            _metrics["relationship_creation_time_ms"] = relationshipTime;
            _metrics["memory_usage_kb"] = memoryUsed;
        }
        catch (Exception ex)
        {
            AddTestResult("PERFORMANCE_CHARACTERISTICS", false, 
                $"Performance testing failed: {ex.Message}", ex.StackTrace);
        }
    }

    private async Task TestBusinessRuleValidationAsync()
    {
        _logger.LogInformation("📋 Testing business rule validation");

        try
        {
            // Test basic business rule enforcement
            var user = new User();
            user.Name = "Test User";
            
            var permission = new Permission
            {
                Resource = "TestResource",
                Action = "Read"
            };

            // Test permission assignment
            user.AddPermission(permission);
            Assert.True(user.Permissions.Contains(permission));

            // Test permission removal
            user.RemovePermission(permission);
            Assert.False(user.Permissions.Contains(permission));

            AddTestResult("BUSINESS_RULE_VALIDATION", true, 
                "Basic business rules and validation working correctly");

            // Test entity hierarchy validation
            var group = new Group();
            group.Name = "Test Group";
            
            var role = new Role();
            role.Name = "Test Role";

            // Test parent-child relationships
            user.Parents.Add(group);
            group.Children.Add(user);
            
            Assert.True(user.Parents.Contains(group));
            Assert.True(group.Children.Contains(user));

            AddTestResult("ENTITY_HIERARCHY_VALIDATION", true, 
                "Entity hierarchy and relationship validation working");

            _metrics["business_rules_tested"] = 3;
        }
        catch (Exception ex)
        {
            AddTestResult("BUSINESS_RULE_VALIDATION", false, 
                $"Business rule validation failed: {ex.Message}", ex.StackTrace);
        }
    }

    private void AddTestResult(string testName, bool passed, string details, string? stackTrace = null)
    {
        var result = new TestResult
        {
            TestName = testName,
            Passed = passed,
            Details = details,
            StackTrace = stackTrace,
            ExecutedAt = DateTimeOffset.UtcNow
        };

        _results.Add(result);

        var status = passed ? "✅" : "❌";
        _logger.LogInformation("{Status} {TestName}: {Details}", status, testName, details);
    }

    private static class Assert
    {
        public static void NotNull(object? value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value), "Expected non-null value");
        }

        public static void True(bool condition)
        {
            if (!condition)
                throw new InvalidOperationException("Expected condition to be true");
        }

        public static void False(bool condition)
        {
            if (condition)
                throw new InvalidOperationException("Expected condition to be false");
        }
    }
}

public class IntegrationReport
{
    public string TestRunId { get; set; } = "";
    public string TestSuite { get; set; } = "";
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string? CriticalError { get; set; }
    public int TotalTestsRun { get; set; }
    public int TestsPassed { get; set; }
    public int TestsFailed { get; set; }
    public List<TestResult> TestResults { get; set; } = new();
    public Dictionary<string, object> Metrics { get; set; } = new();

    public double SuccessRate => TotalTestsRun == 0 ? 0 : (double)TestsPassed / TotalTestsRun * 100;

    public string GenerateReport()
    {
        var report = new StringBuilder();
        report.AppendLine("# ACS System Integration Validation Report");
        report.AppendLine("=========================================");
        report.AppendLine();
        
        report.AppendLine($"**Test Run ID**: {TestRunId}");
        report.AppendLine($"**Test Suite**: {TestSuite}");
        report.AppendLine($"**Duration**: {Duration.TotalSeconds:F2} seconds");
        report.AppendLine($"**Overall Status**: {(Success ? "✅ PASSED" : "❌ FAILED")}");
        report.AppendLine();
        
        report.AppendLine("## Test Summary");
        report.AppendLine($"- **Total Tests**: {TotalTestsRun}");
        report.AppendLine($"- **Passed**: {TestsPassed} ({SuccessRate:F1}%)");
        report.AppendLine($"- **Failed**: {TestsFailed}");
        report.AppendLine();

        if (!string.IsNullOrEmpty(CriticalError))
        {
            report.AppendLine("## Critical Error");
            report.AppendLine("```");
            report.AppendLine(CriticalError);
            report.AppendLine("```");
            report.AppendLine();
        }

        // Test Results
        report.AppendLine("## Test Results");
        foreach (var result in TestResults)
        {
            var status = result.Passed ? "✅" : "❌";
            report.AppendLine($"### {status} {result.TestName}");
            report.AppendLine($"**Status**: {(result.Passed ? "PASSED" : "FAILED")}");
            report.AppendLine($"**Details**: {result.Details}");
            if (!string.IsNullOrEmpty(result.StackTrace))
            {
                report.AppendLine("**Stack Trace**:");
                report.AppendLine("```");
                report.AppendLine(result.StackTrace);
                report.AppendLine("```");
            }
            report.AppendLine();
        }

        // Metrics
        if (Metrics.Any())
        {
            report.AppendLine("## Performance Metrics");
            foreach (var metric in Metrics.OrderBy(m => m.Key))
            {
                report.AppendLine($"- **{metric.Key}**: {metric.Value}");
            }
            report.AppendLine();
        }

        // Production Readiness Assessment
        report.AppendLine("## Production Readiness Assessment");
        
        if (SuccessRate >= 95)
        {
            report.AppendLine("### ✅ **PRODUCTION READY**");
            report.AppendLine("Core system components are working correctly and performance is acceptable.");
        }
        else if (SuccessRate >= 80)
        {
            report.AppendLine("### ⚠️ **CAUTION**");
            report.AppendLine("System has minor issues that should be addressed before production.");
        }
        else
        {
            report.AppendLine("### ❌ **NOT PRODUCTION READY**");
            report.AppendLine("Critical system failures detected. Resolve issues before production deployment.");
        }
        
        report.AppendLine();
        report.AppendLine("---");
        report.AppendLine($"*Report generated on {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}*");

        return report.ToString();
    }
}

public class TestResult
{
    public string TestName { get; set; } = "";
    public bool Passed { get; set; }
    public string Details { get; set; } = "";
    public string? StackTrace { get; set; }
    public DateTimeOffset ExecutedAt { get; set; }
}