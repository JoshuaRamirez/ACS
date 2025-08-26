using ACS.Service.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ACS.Service.Services;

/// <summary>
/// Health check for core business logic services and domain operations
/// </summary>
public class BusinessLogicHealthCheck : IHealthCheck
{
    private readonly IUserService _userService;
    private readonly IPermissionEvaluationService _permissionEvaluationService;
    private readonly ILogger<BusinessLogicHealthCheck> _logger;
    private readonly TimeSpan _timeout;

    public BusinessLogicHealthCheck(
        IUserService userService,
        IPermissionEvaluationService permissionEvaluationService,
        ILogger<BusinessLogicHealthCheck> logger)
    {
        _userService = userService;
        _permissionEvaluationService = permissionEvaluationService;
        _logger = logger;
        _timeout = TimeSpan.FromSeconds(5);
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var healthData = new Dictionary<string, object>();
        var issues = new List<string>();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);

            // Test core service availability
            await CheckServiceAvailabilityAsync(healthData, issues, cts.Token);

            // Test basic business operations
            await CheckBasicOperationsAsync(healthData, issues, cts.Token);

            // Test permission evaluation system
            await CheckPermissionSystemAsync(healthData, issues, cts.Token);

            // Test domain validation
            await CheckDomainValidationAsync(healthData, issues, cts.Token);

            stopwatch.Stop();
            healthData["CheckDuration"] = stopwatch.ElapsedMilliseconds;
            healthData["ServicesChecked"] = new[] { "UserService", "PermissionEvaluationService", "DomainValidation" };

            if (issues.Any())
            {
                var criticalIssues = issues.Count(i => i.Contains("CRITICAL"));
                var warningIssues = issues.Count - criticalIssues;

                healthData["CriticalIssues"] = criticalIssues;
                healthData["WarningIssues"] = warningIssues;
                healthData["Issues"] = issues;

                if (criticalIssues > 0)
                {
                    return HealthCheckResult.Unhealthy(
                        $"Business logic has {criticalIssues} critical issues and {warningIssues} warnings",
                        null,
                        healthData);
                }

                return HealthCheckResult.Degraded(
                    $"Business logic has {warningIssues} warning(s)",
                    null,
                    healthData);
            }

            return HealthCheckResult.Healthy(
                $"All business logic services are healthy ({stopwatch.ElapsedMilliseconds}ms)",
                healthData);
        }
        catch (OperationCanceledException)
        {
            healthData["TimedOut"] = true;
            _logger.LogWarning("Business logic health check timed out after {Timeout}ms", _timeout.TotalMilliseconds);
            
            return HealthCheckResult.Unhealthy(
                $"Business logic health check timed out after {_timeout.TotalSeconds} seconds",
                null,
                healthData);
        }
        catch (Exception ex)
        {
            healthData["Exception"] = ex.Message;
            _logger.LogError(ex, "Business logic health check failed");
            
            return HealthCheckResult.Unhealthy(
                $"Business logic health check failed: {ex.Message}",
                ex,
                healthData);
        }
    }

    private Task CheckServiceAvailabilityAsync(
        Dictionary<string, object> healthData,
        List<string> issues,
        CancellationToken cancellationToken)
    {
        var serviceStatuses = new Dictionary<string, bool>();

        try
        {
            // Check if services can be resolved and are responsive
            if (_userService == null)
            {
                issues.Add("CRITICAL: UserService is not available");
                serviceStatuses["UserService"] = false;
            }
            else
            {
                serviceStatuses["UserService"] = true;
            }

            if (_permissionEvaluationService == null)
            {
                issues.Add("CRITICAL: PermissionEvaluationService is not available");
                serviceStatuses["PermissionEvaluationService"] = false;
            }
            else
            {
                serviceStatuses["PermissionEvaluationService"] = true;
            }

            healthData["ServiceAvailability"] = serviceStatuses;
        }
        catch (Exception ex)
        {
            issues.Add($"CRITICAL: Service availability check failed: {ex.Message}");
            _logger.LogError(ex, "Failed to check service availability");
        }

        return Task.CompletedTask;
    }

    private async Task CheckBasicOperationsAsync(
        Dictionary<string, object> healthData,
        List<string> issues,
        CancellationToken cancellationToken)
    {
        var operationResults = new Dictionary<string, object>();

        try
        {
            // Test user service operations with a lightweight query
            var userCheckStopwatch = Stopwatch.StartNew();
            try
            {
                // This should be a very lightweight operation that doesn't actually modify data
                // In a real implementation, you might have a dedicated health check method
                var users = await _userService.GetAllAsync(new Requests.GetUsersRequest 
                { 
                    Page = 1, 
                    PageSize = 1, // Minimal for health check
                    RequestedBy = "HealthCheck" 
                });
                var userCount = users?.TotalCount ?? 0;
                
                userCheckStopwatch.Stop();
                operationResults["UserServiceResponseTime"] = userCheckStopwatch.ElapsedMilliseconds;
                operationResults["UserCount"] = userCount;

                if (userCheckStopwatch.ElapsedMilliseconds > 1000)
                {
                    issues.Add($"WARNING: UserService response time is slow: {userCheckStopwatch.ElapsedMilliseconds}ms");
                }
            }
            catch (Exception ex)
            {
                userCheckStopwatch.Stop();
                issues.Add($"CRITICAL: UserService operation failed: {ex.Message}");
                operationResults["UserServiceError"] = ex.Message;
            }

            healthData["BasicOperations"] = operationResults;
        }
        catch (Exception ex)
        {
            issues.Add($"WARNING: Basic operations check failed: {ex.Message}");
            _logger.LogWarning(ex, "Failed to check basic operations");
        }
    }

    private async Task CheckPermissionSystemAsync(
        Dictionary<string, object> healthData,
        List<string> issues,
        CancellationToken cancellationToken)
    {
        var permissionResults = new Dictionary<string, object>();

        try
        {
            var permissionCheckStopwatch = Stopwatch.StartNew();
            
            // Perform a basic permission check that shouldn't modify state
            try
            {
                // This should be a test permission check
                var hasPermission = await _permissionEvaluationService.HasPermissionAsync(
                    1, // Test entity ID
                    "/health", // Safe test URI
                    "GET");

                permissionCheckStopwatch.Stop();
                permissionResults["PermissionCheckResponseTime"] = permissionCheckStopwatch.ElapsedMilliseconds;
                permissionResults["PermissionCheckWorking"] = true;

                if (permissionCheckStopwatch.ElapsedMilliseconds > 500)
                {
                    issues.Add($"WARNING: Permission check is slow: {permissionCheckStopwatch.ElapsedMilliseconds}ms");
                }
            }
            catch (Exception ex)
            {
                permissionCheckStopwatch.Stop();
                issues.Add($"CRITICAL: Permission evaluation failed: {ex.Message}");
                permissionResults["PermissionCheckError"] = ex.Message;
                permissionResults["PermissionCheckWorking"] = false;
            }

            healthData["PermissionSystem"] = permissionResults;
        }
        catch (Exception ex)
        {
            issues.Add($"WARNING: Permission system check failed: {ex.Message}");
            _logger.LogWarning(ex, "Failed to check permission system");
        }
    }

    private Task CheckDomainValidationAsync(
        Dictionary<string, object> healthData,
        List<string> issues,
        CancellationToken cancellationToken)
    {
        var validationResults = new Dictionary<string, object>();

        try
        {
            // Test domain model creation and validation
            var validationStopwatch = Stopwatch.StartNew();
            
            try
            {
                // Create a test domain object to validate the validation system
                var testEntity = new ACS.Service.Domain.User
                {
                    Id = -1, // Use a test ID that won't conflict
                    Name = "HealthCheckTestUser"
                };

                // This tests that the domain model validation attributes are working
                var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(testEntity);
                var validationErrors = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
                
                var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
                    testEntity, validationContext, validationErrors, true);

                validationStopwatch.Stop();
                validationResults["DomainValidationResponseTime"] = validationStopwatch.ElapsedMilliseconds;
                validationResults["ValidationSystemWorking"] = true;
                validationResults["ValidationErrors"] = validationErrors.Count;

                if (validationStopwatch.ElapsedMilliseconds > 100)
                {
                    issues.Add($"WARNING: Domain validation is slow: {validationStopwatch.ElapsedMilliseconds}ms");
                }
            }
            catch (Exception ex)
            {
                validationStopwatch.Stop();
                issues.Add($"CRITICAL: Domain validation system failed: {ex.Message}");
                validationResults["ValidationSystemError"] = ex.Message;
                validationResults["ValidationSystemWorking"] = false;
            }

            healthData["DomainValidation"] = validationResults;
        }
        catch (Exception ex)
        {
            issues.Add($"WARNING: Domain validation check failed: {ex.Message}");
            _logger.LogWarning(ex, "Failed to check domain validation");
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Extension methods for registering business logic health check
/// </summary>
public static class BusinessLogicHealthCheckExtensions
{
    public static IHealthChecksBuilder AddBusinessLogicHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "business_logic",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        return builder.AddTypeActivatedCheck<BusinessLogicHealthCheck>(
            name,
            failureStatus ?? HealthStatus.Unhealthy,
            tags ?? new[] { "business", "domain", "services" },
            timeout ?? TimeSpan.FromSeconds(5));
    }
}