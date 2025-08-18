using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ACS.WebApi.Services;

/// <summary>
/// Service for configuring OpenTelemetry tracing and metrics for the ACS WebApi
/// </summary>
public static class TelemetryService
{
    public static readonly string ServiceName = "ACS.WebApi";
    public static readonly string ServiceVersion = "1.0.0";
    
    /// <summary>
    /// Activity source for custom spans in the WebApi layer
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ServiceName);
    
    /// <summary>
    /// Configures OpenTelemetry tracing for the WebApi application
    /// </summary>
    public static void ConfigureOpenTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(ServiceName, ServiceVersion)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["service.instance.id"] = Environment.MachineName,
                    ["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"
                }))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(ServiceName)
                    .AddSource("ACS.TenantGrpcClient") // Custom source for gRPC client operations
                    .AddSource("ACS.CircuitBreaker") // Custom source for circuit breaker operations
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = httpContext => 
                        {
                            // Skip health check endpoints from tracing to reduce noise
                            var path = httpContext.Request.Path.Value;
                            return !path?.StartsWith("/health") == true;
                        };
                        options.EnrichWithHttpRequest = (activity, httpRequest) =>
                        {
                            // Add tenant ID to traces for multi-tenant correlation
                            if (httpRequest.Headers.TryGetValue("X-Tenant-ID", out var tenantId))
                            {
                                activity.SetTag("tenant.id", tenantId.FirstOrDefault());
                            }
                        };
                        options.EnrichWithHttpResponse = (activity, httpResponse) =>
                        {
                            activity.SetTag("http.response.status_code", httpResponse.StatusCode);
                        };
                        options.EnrichWithException = (activity, exception) =>
                        {
                            activity.SetTag("error", true);
                            activity.SetTag("error.type", exception.GetType().Name);
                            activity.SetTag("error.message", exception.Message);
                        };
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnrichWithHttpRequestMessage = (activity, httpRequest) =>
                        {
                            activity.SetTag("http.client.operation", "tenant_process_discovery");
                        };
                    })
                    .AddGrpcClientInstrumentation();

                // Configure exporters based on configuration
                var exporterType = configuration.GetValue<string>("OpenTelemetry:Exporter") ?? "Console";
                
                switch (exporterType.ToLowerInvariant())
                {
                    case "otlp":
                        var endpoint = configuration.GetValue<string>("OpenTelemetry:OtlpEndpoint") ?? "http://localhost:4317";
                        tracing.AddOtlpExporter(options => 
                        {
                            options.Endpoint = new Uri(endpoint);
                        });
                        break;
                    
                    case "console":
                    default:
                        tracing.AddConsoleExporter();
                        break;
                }
            });
    }
    
    /// <summary>
    /// Creates a new activity for tenant operations
    /// </summary>
    public static Activity? StartTenantActivity(string operationName, string tenantId)
    {
        var activity = ActivitySource.StartActivity($"tenant.{operationName}");
        activity?.SetTag("tenant.id", tenantId);
        activity?.SetTag("operation.type", operationName);
        return activity;
    }
    
    /// <summary>
    /// Creates a new activity for gRPC client operations
    /// </summary>
    public static Activity? StartGrpcClientActivity(string method, string tenantId)
    {
        var activity = ActivitySource.StartActivity($"grpc.client.{method}");
        activity?.SetTag("tenant.id", tenantId);
        activity?.SetTag("rpc.service", "VerticalService");
        activity?.SetTag("rpc.method", method);
        activity?.SetTag("rpc.system", "grpc");
        return activity;
    }
    
    /// <summary>
    /// Creates a new activity for circuit breaker operations
    /// </summary>
    public static Activity? StartCircuitBreakerActivity(string state, string tenantId)
    {
        var activity = ActivitySource.StartActivity($"circuit_breaker.{state}");
        activity?.SetTag("tenant.id", tenantId);
        activity?.SetTag("circuit_breaker.state", state);
        return activity;
    }
    
    /// <summary>
    /// Adds error information to the current activity
    /// </summary>
    public static void RecordError(Activity? activity, Exception exception)
    {
        if (activity == null) return;
        
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.SetTag("error", true);
        activity.SetTag("error.type", exception.GetType().Name);
        activity.SetTag("error.message", exception.Message);
        
        if (exception.StackTrace != null)
        {
            activity.SetTag("error.stack", exception.StackTrace);
        }
    }
    
    /// <summary>
    /// Records command processing information
    /// </summary>
    public static void RecordCommandProcessing(Activity? activity, string commandType, string requestId)
    {
        if (activity == null) return;
        
        activity.SetTag("command.type", commandType);
        activity.SetTag("command.request_id", requestId);
        activity.SetTag("operation.category", "command_processing");
    }
}