using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

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
    /// Meter for custom metrics in the WebApi layer
    /// </summary>
    public static readonly Meter Meter = new(ServiceName, ServiceVersion);
    
    // Performance metrics instruments
    public static readonly Counter<long> RequestCounter = Meter.CreateCounter<long>(
        "acs_webapi_requests_total", 
        description: "Total number of HTTP requests");
        
    public static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
        "acs_webapi_request_duration_seconds", 
        description: "HTTP request duration in seconds");
        
    public static readonly Counter<long> GrpcCallCounter = Meter.CreateCounter<long>(
        "acs_webapi_grpc_calls_total", 
        description: "Total number of gRPC calls");
        
    public static readonly Histogram<double> GrpcCallDuration = Meter.CreateHistogram<double>(
        "acs_webapi_grpc_call_duration_seconds", 
        description: "gRPC call duration in seconds");
        
    public static readonly Counter<long> ErrorCounter = Meter.CreateCounter<long>(
        "acs_webapi_errors_total", 
        description: "Total number of errors");
        
    public static readonly ObservableGauge<long> ActiveTenants = Meter.CreateObservableGauge<long>(
        "acs_webapi_active_tenants", 
        description: "Number of active tenants",
        observeValue: () => 0L);
        
    public static readonly Histogram<double> TenantSwitchDuration = Meter.CreateHistogram<double>(
        "acs_webapi_tenant_switch_duration_seconds", 
        description: "Tenant context switch duration in seconds");
    
    /// <summary>
    /// Configures OpenTelemetry tracing and metrics for the WebApi application
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
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(ServiceName)
                    .AddMeter("ACS.VerticalHost.*") // Include tenant process metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddEventCountersInstrumentation(options =>
                    {
                        // Add .NET runtime and ASP.NET Core event counters
                        options.AddEventSources(
                            "System.Runtime",
                            "Microsoft.AspNetCore.Hosting",
                            "Microsoft.AspNetCore.Http.Connections",
                            "Grpc.AspNetCore.Server",
                            "Grpc.Net.Client");
                    });

                // Configure metrics exporters based on configuration
                var metricsExporter = configuration.GetValue<string>("OpenTelemetry:MetricsExporter", "Console");
                switch (metricsExporter.ToLowerInvariant())
                {
                    case "otlp":
                        var endpoint = configuration.GetValue<string>("OpenTelemetry:OtlpEndpoint") ?? "http://localhost:4317";
                        metrics.AddOtlpExporter(options =>
                        {
                            options.Endpoint = new Uri(endpoint);
                        });
                        break;
                    case "console":
                    default:
                        metrics.AddConsoleExporter();
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
    
    // Performance metrics recording methods
    
    /// <summary>
    /// Records HTTP request metrics
    /// </summary>
    public static void RecordHttpRequest(string method, string path, int statusCode, double durationSeconds, string? tenantId = null)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("http.method", method),
            new("http.route", path),
            new("http.status_code", statusCode),
            new("tenant.id", tenantId ?? "unknown")
        };

        RequestCounter.Add(1, tags);
        RequestDuration.Record(durationSeconds, tags);
    }
    
    /// <summary>
    /// Records gRPC call metrics
    /// </summary>
    public static void RecordGrpcCall(string method, string status, double durationSeconds, string tenantId)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("grpc.method", method),
            new("grpc.status", status),
            new("tenant.id", tenantId)
        };

        GrpcCallCounter.Add(1, tags);
        GrpcCallDuration.Record(durationSeconds, tags);
    }
    
    /// <summary>
    /// Records error metrics
    /// </summary>
    public static void RecordError(string errorType, string operation, string? tenantId = null)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("error.type", errorType),
            new("operation", operation),
            new("tenant.id", tenantId ?? "unknown")
        };

        ErrorCounter.Add(1, tags);
    }
    
    /// <summary>
    /// Records tenant context switch metrics
    /// </summary>
    public static void RecordTenantSwitch(string fromTenant, string toTenant, double durationSeconds)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("from_tenant", fromTenant),
            new("to_tenant", toTenant)
        };

        TenantSwitchDuration.Record(durationSeconds, tags);
    }
    
    /// <summary>
    /// Records database operation metrics
    /// </summary>
    public static void RecordDatabaseOperation(string operation, double durationMs, bool success, string? tenantId = null)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("db.operation", operation),
            new("db.success", success),
            new("tenant.id", tenantId ?? "unknown")
        };

        // Convert to histogram for database response time tracking
        var durationSeconds = durationMs / 1000.0;
        RequestDuration.Record(durationSeconds, tags);
        
        if (!success)
        {
            RecordError("database_error", operation, tenantId);
        }
    }
}