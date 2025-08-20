using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;

namespace ACS.Infrastructure.HealthChecks;

/// <summary>
/// Health check for external HTTP/HTTPS services
/// </summary>
public class ExternalServiceHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ExternalServiceHealthCheck> _logger;
    private readonly string _serviceName;
    private readonly string _endpoint;
    private readonly TimeSpan _timeout;
    private readonly int? _expectedStatusCode;

    public ExternalServiceHealthCheck(
        IHttpClientFactory httpClientFactory,
        ILogger<ExternalServiceHealthCheck> logger,
        string serviceName,
        string endpoint,
        TimeSpan? timeout = null,
        int? expectedStatusCode = null)
    {
        _httpClient = httpClientFactory.CreateClient($"HealthCheck_{serviceName}");
        _logger = logger;
        _serviceName = serviceName;
        _endpoint = endpoint;
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
        _expectedStatusCode = expectedStatusCode ?? 200;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);

            using var request = new HttpRequestMessage(HttpMethod.Get, _endpoint);
            request.Headers.Add("User-Agent", "ACS-HealthCheck/1.0");
            
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            
            stopwatch.Stop();

            var data = new Dictionary<string, object>
            {
                ["Service"] = _serviceName,
                ["Endpoint"] = _endpoint,
                ["StatusCode"] = (int)response.StatusCode,
                ["ReasonPhrase"] = response.ReasonPhrase ?? string.Empty,
                ["ResponseTime"] = stopwatch.ElapsedMilliseconds,
                ["Protocol"] = response.Version.ToString()
            };

            // Add response headers if available
            if (response.Headers.TryGetValues("X-Response-Time", out var responseTime))
            {
                data["ServerResponseTime"] = responseTime.FirstOrDefault() ?? "N/A";
            }

            if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var rateLimitRemaining))
            {
                data["RateLimitRemaining"] = rateLimitRemaining.FirstOrDefault() ?? "N/A";
            }

            if (response.Headers.TryGetValues("X-RateLimit-Limit", out var rateLimitLimit))
            {
                data["RateLimitLimit"] = rateLimitLimit.FirstOrDefault() ?? "N/A";
            }

            // Check if status code matches expected
            if (_expectedStatusCode.HasValue && response.StatusCode != (HttpStatusCode)_expectedStatusCode.Value)
            {
                return HealthCheckResult.Unhealthy(
                    $"Service '{_serviceName}' returned unexpected status code: {response.StatusCode}",
                    null,
                    data);
            }

            // Check response time
            if (stopwatch.Elapsed > TimeSpan.FromSeconds(3))
            {
                return HealthCheckResult.Degraded(
                    $"Service '{_serviceName}' response is slow: {stopwatch.ElapsedMilliseconds}ms",
                    null,
                    data);
            }

            return HealthCheckResult.Healthy(
                $"Service '{_serviceName}' is healthy ({stopwatch.ElapsedMilliseconds}ms)",
                data);
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("External service health check timed out for {Service} at {Endpoint}", 
                _serviceName, _endpoint);
            
            return HealthCheckResult.Unhealthy(
                $"Service '{_serviceName}' health check timed out after {_timeout.TotalSeconds} seconds",
                null,
                new Dictionary<string, object>
                {
                    ["Service"] = _serviceName,
                    ["Endpoint"] = _endpoint,
                    ["Timeout"] = _timeout.TotalSeconds
                });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "External service health check failed for {Service} at {Endpoint}", 
                _serviceName, _endpoint);
            
            return HealthCheckResult.Unhealthy(
                $"Service '{_serviceName}' is unreachable: {ex.Message}",
                ex,
                new Dictionary<string, object>
                {
                    ["Service"] = _serviceName,
                    ["Endpoint"] = _endpoint,
                    ["Error"] = ex.Message
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during external service health check for {Service}", 
                _serviceName);
            
            return HealthCheckResult.Unhealthy(
                $"Service '{_serviceName}' health check failed: {ex.Message}",
                ex);
        }
    }
}

/// <summary>
/// Factory for creating external service health checks
/// </summary>
public class ExternalServiceHealthCheckFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _configuration;

    public ExternalServiceHealthCheckFactory(
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
        _configuration = configuration;
    }

    public ExternalServiceHealthCheck CreateHealthCheck(string serviceName)
    {
        var config = _configuration.GetSection($"HealthChecks:ExternalServices:{serviceName}");
        
        var endpoint = config["Endpoint"] ?? throw new ArgumentException($"Endpoint not configured for service {serviceName}");
        var timeout = config.GetValue<int?>("TimeoutSeconds");
        var expectedStatusCode = config.GetValue<int?>("ExpectedStatusCode");
        
        return new ExternalServiceHealthCheck(
            _httpClientFactory,
            _loggerFactory.CreateLogger<ExternalServiceHealthCheck>(),
            serviceName,
            endpoint,
            timeout.HasValue ? TimeSpan.FromSeconds(timeout.Value) : null,
            expectedStatusCode);
    }
}