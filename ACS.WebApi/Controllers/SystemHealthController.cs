using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;

namespace ACS.WebApi.Controllers;

/// <summary>
/// Controller providing comprehensive system health check endpoints using ASP.NET Core health checks
/// </summary>
[ApiController]
[Route("api/system/[controller]")]
[ApiExplorerSettings(GroupName = "v1")]
public class SystemHealthController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;
    private readonly ILogger<SystemHealthController> _logger;

    public SystemHealthController(HealthCheckService healthCheckService, ILogger<SystemHealthController> logger)
    {
        _healthCheckService = healthCheckService;
        _logger = logger;
    }

    /// <summary>
    /// Get the overall health status of the system
    /// </summary>
    /// <returns>Overall health status</returns>
    [HttpGet]
    [HttpGet("status")]
    [ProducesResponseType(typeof(HealthResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(HealthResponse), (int)HttpStatusCode.ServiceUnavailable)]
    [ProducesResponseType(typeof(HealthResponse), (int)HttpStatusCode.Accepted)]
    public async Task<IActionResult> GetHealth()
    {
        try
        {
            var healthReport = await _healthCheckService.CheckHealthAsync(HttpContext.RequestAborted);
            var response = CreateHealthResponse(healthReport);

            return healthReport.Status switch
            {
                HealthStatus.Healthy => Ok(response),
                HealthStatus.Degraded => StatusCode((int)HttpStatusCode.Accepted, response),
                _ => StatusCode((int)HttpStatusCode.ServiceUnavailable, response)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed with exception");
            
            var errorResponse = new HealthResponse
            {
                Status = "Unhealthy",
                TotalDuration = TimeSpan.Zero,
                Entries = new Dictionary<string, HealthCheckEntry>(),
                Error = ex.Message
            };

            return StatusCode((int)HttpStatusCode.ServiceUnavailable, errorResponse);
        }
    }

    /// <summary>
    /// Get detailed health information for all checks
    /// </summary>
    /// <returns>Detailed health information</returns>
    [HttpGet("detailed")]
    [ProducesResponseType(typeof(DetailedHealthResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(DetailedHealthResponse), (int)HttpStatusCode.ServiceUnavailable)]
    public async Task<IActionResult> GetDetailedHealth()
    {
        try
        {
            var healthReport = await _healthCheckService.CheckHealthAsync(HttpContext.RequestAborted);
            var response = CreateDetailedHealthResponse(healthReport);

            return healthReport.Status switch
            {
                HealthStatus.Healthy => Ok(response),
                HealthStatus.Degraded => StatusCode((int)HttpStatusCode.Accepted, response),
                _ => StatusCode((int)HttpStatusCode.ServiceUnavailable, response)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Detailed health check failed with exception");
            
            var errorResponse = new DetailedHealthResponse
            {
                Status = "Unhealthy",
                TotalDuration = TimeSpan.Zero,
                Summary = new HealthSummary { Total = 0, Healthy = 0, Degraded = 0, Unhealthy = 1 },
                Entries = new Dictionary<string, DetailedHealthCheckEntry>(),
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            };

            return StatusCode((int)HttpStatusCode.ServiceUnavailable, errorResponse);
        }
    }

    /// <summary>
    /// Get health status for a specific check
    /// </summary>
    /// <param name="checkName">Name of the health check</param>
    /// <returns>Health status for the specified check</returns>
    [HttpGet("{checkName}")]
    [ProducesResponseType(typeof(DetailedHealthCheckEntry), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(DetailedHealthCheckEntry), (int)HttpStatusCode.ServiceUnavailable)]
    public async Task<IActionResult> GetHealthCheck(string checkName)
    {
        try
        {
            var healthReport = await _healthCheckService.CheckHealthAsync(HttpContext.RequestAborted);
            
            if (!healthReport.Entries.TryGetValue(checkName, out var entry))
            {
                return NotFound(new ErrorResponse 
                { 
                    Message = $"Health check '{checkName}' not found",
                    AvailableChecks = healthReport.Entries.Keys.ToArray()
                });
            }

            var response = CreateDetailedHealthCheckEntry(checkName, entry);
            
            return entry.Status switch
            {
                HealthStatus.Healthy => Ok(response),
                HealthStatus.Degraded => StatusCode((int)HttpStatusCode.Accepted, response),
                _ => StatusCode((int)HttpStatusCode.ServiceUnavailable, response)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check for {CheckName} failed with exception", checkName);
            
            var errorResponse = new DetailedHealthCheckEntry
            {
                Status = "Unhealthy",
                Duration = TimeSpan.Zero,
                Description = $"Health check failed: {ex.Message}",
                Data = new Dictionary<string, object> { ["Exception"] = ex.Message },
                Tags = new[] { "error" }
            };

            return StatusCode((int)HttpStatusCode.ServiceUnavailable, errorResponse);
        }
    }

    /// <summary>
    /// Get health checks filtered by tags
    /// </summary>
    /// <param name="tags">Comma-separated list of tags to filter by</param>
    /// <returns>Health status for checks matching the specified tags</returns>
    [HttpGet("tags/{tags}")]
    [ProducesResponseType(typeof(DetailedHealthResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(DetailedHealthResponse), (int)HttpStatusCode.ServiceUnavailable)]
    public async Task<IActionResult> GetHealthByTags(string tags)
    {
        try
        {
            var tagList = tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Select(t => t.Trim().ToLowerInvariant())
                             .ToHashSet();

            if (!tagList.Any())
            {
                return BadRequest(new ErrorResponse { Message = "At least one tag must be specified" });
            }

            var healthReport = await _healthCheckService.CheckHealthAsync(HttpContext.RequestAborted);
            var filteredEntries = healthReport.Entries
                .Where(kvp => kvp.Value.Tags.Any(tag => tagList.Contains(tag.ToLowerInvariant())))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            var response = CreateDetailedHealthResponse(healthReport, filteredEntries);
            response.FilteredBy = tagList.ToArray();

            var overallStatus = DetermineOverallStatus(filteredEntries.Values.Select(entry => new HealthCheckResult(entry.Status, entry.Description, entry.Exception, entry.Data)).ToList());

            return overallStatus switch
            {
                HealthStatus.Healthy => Ok(response),
                HealthStatus.Degraded => StatusCode((int)HttpStatusCode.Accepted, response),
                _ => StatusCode((int)HttpStatusCode.ServiceUnavailable, response)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tagged health check failed with exception");
            
            var errorResponse = new DetailedHealthResponse
            {
                Status = "Unhealthy",
                TotalDuration = TimeSpan.Zero,
                Summary = new HealthSummary { Total = 0, Healthy = 0, Degraded = 0, Unhealthy = 1 },
                Entries = new Dictionary<string, DetailedHealthCheckEntry>(),
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            };

            return StatusCode((int)HttpStatusCode.ServiceUnavailable, errorResponse);
        }
    }

    /// <summary>
    /// Get a simple liveness probe endpoint
    /// </summary>
    /// <returns>Simple OK response if the service is running</returns>
    [HttpGet("live")]
    [ProducesResponseType(typeof(LivenessResponse), (int)HttpStatusCode.OK)]
    public IActionResult GetLiveness()
    {
        return Ok(new LivenessResponse
        {
            Status = "Alive",
            Timestamp = DateTime.UtcNow,
            Uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime,
            Version = GetType().Assembly.GetName().Version?.ToString() ?? "Unknown"
        });
    }

    /// <summary>
    /// Get a readiness probe endpoint
    /// </summary>
    /// <returns>OK if ready to serve requests, ServiceUnavailable otherwise</returns>
    [HttpGet("ready")]
    [ProducesResponseType(typeof(ReadinessResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ReadinessResponse), (int)HttpStatusCode.ServiceUnavailable)]
    public async Task<IActionResult> GetReadiness()
    {
        try
        {
            // Check only critical systems required for readiness
            var criticalHealthReport = await _healthCheckService.CheckHealthAsync(
                check => check.Tags.Contains("database") || check.Tags.Contains("business"), 
                HttpContext.RequestAborted);

            var isReady = criticalHealthReport.Status != HealthStatus.Unhealthy;
            var response = new ReadinessResponse
            {
                Status = isReady ? "Ready" : "NotReady",
                Timestamp = DateTime.UtcNow,
                CriticalSystemsHealthy = criticalHealthReport.Status.ToString(),
                CheckedSystems = criticalHealthReport.Entries.Keys.ToArray()
            };

            return isReady 
                ? Ok(response)
                : StatusCode((int)HttpStatusCode.ServiceUnavailable, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Readiness check failed");
            
            var errorResponse = new ReadinessResponse
            {
                Status = "NotReady",
                Timestamp = DateTime.UtcNow,
                CriticalSystemsHealthy = "Error",
                Error = ex.Message
            };

            return StatusCode((int)HttpStatusCode.ServiceUnavailable, errorResponse);
        }
    }

    private static HealthResponse CreateHealthResponse(HealthReport healthReport)
    {
        return new HealthResponse
        {
            Status = healthReport.Status.ToString(),
            TotalDuration = healthReport.TotalDuration,
            Entries = healthReport.Entries.ToDictionary(
                kvp => kvp.Key,
                kvp => new HealthCheckEntry
                {
                    Status = kvp.Value.Status.ToString(),
                    Duration = kvp.Value.Duration,
                    Description = kvp.Value.Description ?? string.Empty
                })
        };
    }

    private static DetailedHealthResponse CreateDetailedHealthResponse(HealthReport healthReport, 
        Dictionary<string, HealthReportEntry>? filteredEntries = null)
    {
        var entries = filteredEntries ?? healthReport.Entries.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        
        return new DetailedHealthResponse
        {
            Status = healthReport.Status.ToString(),
            TotalDuration = healthReport.TotalDuration,
            Timestamp = DateTime.UtcNow,
            Summary = new HealthSummary
            {
                Total = entries.Count,
                Healthy = entries.Count(kvp => kvp.Value.Status == HealthStatus.Healthy),
                Degraded = entries.Count(kvp => kvp.Value.Status == HealthStatus.Degraded),
                Unhealthy = entries.Count(kvp => kvp.Value.Status == HealthStatus.Unhealthy)
            },
            Entries = entries.ToDictionary(
                kvp => kvp.Key,
                kvp => CreateDetailedHealthCheckEntry(kvp.Key, kvp.Value))
        };
    }

    private static DetailedHealthCheckEntry CreateDetailedHealthCheckEntry(string name, HealthReportEntry result)
    {
        return new DetailedHealthCheckEntry
        {
            Status = result.Status.ToString(),
            Duration = result.Duration,
            Description = result.Description ?? string.Empty,
            Data = result.Data?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, object>(),
            Tags = result.Tags.ToArray(),
            Exception = result.Exception?.Message
        };
    }

    private static HealthStatus DetermineOverallStatus(IEnumerable<HealthCheckResult> results)
    {
        var resultList = results.ToList();
        
        if (resultList.Any(r => r.Status == HealthStatus.Unhealthy))
            return HealthStatus.Unhealthy;
        
        if (resultList.Any(r => r.Status == HealthStatus.Degraded))
            return HealthStatus.Degraded;
        
        return HealthStatus.Healthy;
    }
}

#region Response Models

public class HealthResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("totalDuration")]
    public TimeSpan TotalDuration { get; set; }

    [JsonPropertyName("entries")]
    public Dictionary<string, HealthCheckEntry> Entries { get; set; } = new();

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }
}

public class DetailedHealthResponse : HealthResponse
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("summary")]
    public HealthSummary Summary { get; set; } = new();

    [JsonPropertyName("entries")]
    public new Dictionary<string, DetailedHealthCheckEntry> Entries { get; set; } = new();

    [JsonPropertyName("filteredBy")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? FilteredBy { get; set; }
}

public class HealthCheckEntry
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("duration")]
    public TimeSpan Duration { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public class DetailedHealthCheckEntry : HealthCheckEntry
{
    [JsonPropertyName("data")]
    public Dictionary<string, object> Data { get; set; } = new();

    [JsonPropertyName("tags")]
    public string[] Tags { get; set; } = Array.Empty<string>();

    [JsonPropertyName("exception")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Exception { get; set; }
}

public class HealthSummary
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("healthy")]
    public int Healthy { get; set; }

    [JsonPropertyName("degraded")]
    public int Degraded { get; set; }

    [JsonPropertyName("unhealthy")]
    public int Unhealthy { get; set; }
}

public class LivenessResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("uptime")]
    public TimeSpan Uptime { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
}

public class ReadinessResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("criticalSystemsHealthy")]
    public string CriticalSystemsHealthy { get; set; } = string.Empty;

    [JsonPropertyName("checkedSystems")]
    public string[] CheckedSystems { get; set; } = Array.Empty<string>();

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }
}

public class ErrorResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("availableChecks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? AvailableChecks { get; set; }
}

#endregion