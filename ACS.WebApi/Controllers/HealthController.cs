using Microsoft.AspNetCore.Mvc;
using ACS.WebApi.Resources;
using ACS.WebApi.Models.Responses;
using ACS.Infrastructure.Services;
using ACS.Infrastructure;

namespace ACS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ITenantContextService _tenantContextService;
    private readonly TenantProcessDiscoveryService _processDiscoveryService;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        ITenantContextService tenantContextService,
        TenantProcessDiscoveryService processDiscoveryService,
        ILogger<HealthController> logger)
    {
        _tenantContextService = tenantContextService;
        _processDiscoveryService = processDiscoveryService;
        _logger = logger;
    }

    /// <summary>
    /// Get health status of the WebApi and current tenant process
    /// </summary>
    [HttpGet]
    public Task<ActionResult<ApiResponse<HealthCheckResource>>> GetHealth()
    {
        try
        {
            var details = new Dictionary<string, object>
            {
                ["webApi"] = "healthy",
                ["timestamp"] = DateTime.UtcNow
            };

            try
            {
                var tenantId = _tenantContextService.GetTenantId();
                var tenantProcessInfo = _tenantContextService.GetTenantProcessInfo();
                var grpcChannel = _tenantContextService.GetGrpcChannel();

                details["tenantId"] = tenantId ?? "unknown";
                details["tenantProcess"] = tenantProcessInfo != null ? "healthy" : "unavailable";
                details["grpcChannel"] = grpcChannel != null ? "connected" : "disconnected";

                if (tenantProcessInfo != null)
                {
                    details["processId"] = tenantProcessInfo.ProcessId;
                    details["grpcEndpoint"] = tenantProcessInfo.GrpcEndpoint;
                }
            }
            catch (Exception ex)
            {
                details["tenantResolution"] = "failed";
                details["tenantError"] = ex.Message;
                _logger.LogWarning(ex, "Failed to resolve tenant information for health check");
            }

            var status = details.ContainsKey("tenantError") ? "degraded" : "healthy";
            var response = new HealthCheckResource { Status = status, Details = details };

            return Task.FromResult<ActionResult<ApiResponse<HealthCheckResource>>>(Ok(new ApiResponse<HealthCheckResource> { Success = true, Data = response }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            
            var errorDetails = new Dictionary<string, object>
            {
                ["webApi"] = "unhealthy",
                ["error"] = ex.Message,
                ["timestamp"] = DateTime.UtcNow
            };

            var errorResponse = new HealthCheckResource { Status = "unhealthy", Details = errorDetails };
            return Task.FromResult<ActionResult<ApiResponse<HealthCheckResource>>>(StatusCode(500, new ApiResponse<HealthCheckResource> { Success = false, Data = errorResponse, Message = "Health check failed" }));
        }
    }

    /// <summary>
    /// Get health status of the current tenant process via gRPC
    /// </summary>
    [HttpGet("tenant")]
    public async Task<ActionResult<ApiResponse<TenantHealthResource>>> GetTenantHealth()
    {
        try
        {
            var tenantId = _tenantContextService.GetTenantId();
            var channel = _tenantContextService.GetGrpcChannel();
            
            if (channel == null)
            {
                var unhealthyResponse = new TenantHealthResource
                {
                    TenantId = tenantId ?? string.Empty,
                    IsHealthy = false,
                    CheckTime = DateTime.UtcNow,
                    UptimeSeconds = 0,
                    ActiveConnections = 0,
                    CommandsProcessed = 0,
                    Message = $"Tenant process not available for tenant {tenantId}"
                };
                
                return Ok(new ApiResponse<TenantHealthResource> { Success = true, Data = unhealthyResponse });
            }

            var client = new ACS.Core.Grpc.VerticalService.VerticalServiceClient(channel);
            var request = new ACS.Core.Grpc.HealthRequest();
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var healthResponse = await client.HealthCheckAsync(request, cancellationToken: cts.Token);
            
            var response = new TenantHealthResource
            {
                TenantId = tenantId ?? string.Empty,
                IsHealthy = healthResponse.Healthy,
                CheckTime = DateTime.UtcNow,
                UptimeSeconds = healthResponse.UptimeSeconds,
                ActiveConnections = healthResponse.ActiveConnections,
                CommandsProcessed = healthResponse.CommandsProcessed,
                Message = healthResponse.Healthy ? "Healthy" : "Unhealthy"
            };

            return Ok(new ApiResponse<TenantHealthResource> { Success = true, Data = response });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking tenant health");
            
            var tenantId = _tenantContextService.GetTenantId();
            var unhealthyResponse = new TenantHealthResource
            {
                TenantId = tenantId ?? string.Empty,
                IsHealthy = false,
                CheckTime = DateTime.UtcNow,
                UptimeSeconds = 0,
                ActiveConnections = 0,
                CommandsProcessed = 0,
                Message = ex.Message
            };
            
            return Ok(new ApiResponse<TenantHealthResource> { Success = true, Data = unhealthyResponse });
        }
    }

    /// <summary>
    /// Get detailed health status of all tenant processes
    /// </summary>
    [HttpGet("processes")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object>>>> GetProcessesHealth()
    {
        try
        {
            var processStatuses = await _processDiscoveryService.GetAllTenantProcessesAsync();
            var processHealthChecks = new Dictionary<string, object>();
            
            // Perform actual health checks for each tenant process
            foreach (var processInfo in processStatuses)
            {
                try
                {
                    using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(processInfo.GrpcEndpoint);
                    var client = new ACS.Core.Grpc.VerticalService.VerticalServiceClient(channel);
                    
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    var healthResponse = await client.HealthCheckAsync(new ACS.Core.Grpc.HealthRequest(), cancellationToken: cts.Token);
                    
                    processHealthChecks[processInfo.TenantId] = new
                    {
                        processId = processInfo.ProcessId,
                        grpcEndpoint = processInfo.GrpcEndpoint,
                        healthy = healthResponse.Healthy,
                        uptimeSeconds = healthResponse.UptimeSeconds,
                        activeConnections = healthResponse.ActiveConnections,
                        commandsProcessed = healthResponse.CommandsProcessed,
                        status = healthResponse.Healthy ? "healthy" : "unhealthy"
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check health for tenant {TenantId}", processInfo.TenantId);
                    processHealthChecks[processInfo.TenantId] = new
                    {
                        processId = processInfo.ProcessId,
                        grpcEndpoint = processInfo.GrpcEndpoint,
                        healthy = false,
                        status = "unreachable",
                        error = ex.Message
                    };
                }
            }
            
            var details = new Dictionary<string, object>
            {
                ["totalProcesses"] = processStatuses.Count,
                ["healthyProcesses"] = processHealthChecks.Values.Count(p => 
                    p.GetType().GetProperty("healthy")?.GetValue(p) as bool? == true),
                ["timestamp"] = DateTime.UtcNow,
                ["processes"] = processHealthChecks
            };

            return Ok(new ApiResponse<Dictionary<string, object>> { Success = true, Data = details });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get processes health status");
            return StatusCode(500, new ACS.WebApi.DTOs.ApiResponse<Dictionary<string, object>>(false, null, "Failed to get processes health", new List<string>()));
        }
    }

    /// <summary>
    /// Get comprehensive health status including WebApi and all tenant processes
    /// </summary>
    [HttpGet("detailed")]
    public async Task<ActionResult<ApiResponse<DetailedHealthResource>>> GetDetailedHealth()
    {
        try
        {
            var webApiHealth = new HealthCheckResponse
            {
                Name = "WebApi",
                Status = "Healthy",
                Duration = TimeSpan.FromMilliseconds(50),
                Description = "Web API health check",
                Data = new Dictionary<string, object>
                {
                    { "version", "1.0.0" },
                    { "environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development" }
                }
            };

            var tenantHealths = new List<SystemHealthResponse>();
            
            try
            {
                var processStatuses = await _processDiscoveryService.GetAllTenantProcessesAsync();
                
                foreach (var processInfo in processStatuses)
                {
                    try
                    {
                        using var channel = Grpc.Net.Client.GrpcChannel.ForAddress(processInfo.GrpcEndpoint);
                        var client = new ACS.Core.Grpc.VerticalService.VerticalServiceClient(channel);
                        
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                        var healthResponse = await client.HealthCheckAsync(new ACS.Core.Grpc.HealthRequest(), cancellationToken: cts.Token);
                        
                        tenantHealths.Add(new SystemHealthResponse
                        {
                            OverallStatus = healthResponse.Healthy ? "Healthy" : "Unhealthy",
                            CheckedAt = DateTime.UtcNow,
                            PerformanceMetrics = new Dictionary<string, object>
                            {
                                { "uptime", healthResponse.UptimeSeconds },
                                { "activeConnections", healthResponse.ActiveConnections },
                                { "commandsProcessed", healthResponse.CommandsProcessed },
                                { "tenantId", processInfo.TenantId }
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to check health for tenant {TenantId}", processInfo.TenantId);
                        tenantHealths.Add(new SystemHealthResponse
                        {
                            OverallStatus = "Unhealthy",
                            CheckedAt = DateTime.UtcNow,
                            PerformanceMetrics = new Dictionary<string, object>
                            {
                                { "uptime", 0 },
                                { "activeConnections", 0 },
                                { "commandsProcessed", 0 },
                                { "tenantId", processInfo.TenantId },
                                { "error", ex.Message }
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get tenant processes for health check");
            }

            var overallHealthy = webApiHealth.Status == "Healthy" && tenantHealths.All(t => t.OverallStatus == "Healthy");

            var detailedHealth = new DetailedHealthResource
            {
                OverallHealthy = overallHealthy,
                CheckTime = DateTime.UtcNow,
                WebApiHealth = new HealthStatusResource
                {
                    Component = webApiHealth.Name,
                    IsHealthy = webApiHealth.Status == "Healthy",
                    CheckTime = DateTime.UtcNow,
                    Message = webApiHealth.Description
                },
                TenantHealths = tenantHealths.Select(t => new TenantHealthResource
                {
                    TenantId = t.PerformanceMetrics.GetValueOrDefault("tenantId", "")?.ToString() ?? string.Empty,
                    IsHealthy = t.OverallStatus == "Healthy",
                    CheckTime = t.CheckedAt,
                    UptimeSeconds = Convert.ToInt64(t.PerformanceMetrics.GetValueOrDefault("uptime", 0)),
                    ActiveConnections = Convert.ToInt32(t.PerformanceMetrics.GetValueOrDefault("activeConnections", 0)),
                    CommandsProcessed = Convert.ToInt64(t.PerformanceMetrics.GetValueOrDefault("commandsProcessed", 0)),
                    Message = ((IReadOnlyDictionary<string, object?>)t.PerformanceMetrics).GetValueOrDefault("error", null)?.ToString()
                }).ToList()
            };

            return Ok(new ACS.WebApi.DTOs.ApiResponse<DetailedHealthResource>(true, detailedHealth, "", new List<string>()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing detailed health check");
            return StatusCode(500, new ACS.WebApi.DTOs.ApiResponse<DetailedHealthResource>(false, null, "Detailed health check failed", new List<string>()));
        }
    }
}