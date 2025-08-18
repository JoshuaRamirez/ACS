using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ACS.VerticalHost.Services;

/// <summary>
/// Service for configuring OpenTelemetry tracing and metrics for the ACS VerticalHost
/// </summary>
public static class VerticalHostTelemetryService
{
    public static readonly string ServiceName = "ACS.VerticalHost";
    public static readonly string ServiceVersion = "1.0.0";
    
    /// <summary>
    /// Activity source for custom spans in the VerticalHost layer
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ServiceName);
    
    /// <summary>
    /// Configures OpenTelemetry tracing for the VerticalHost application
    /// </summary>
    public static void ConfigureOpenTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(ServiceName, ServiceVersion)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["service.instance.id"] = Environment.MachineName,
                    ["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
                    ["tenant.id"] = Environment.GetEnvironmentVariable("TENANT_ID") ?? "unknown"
                }))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(ServiceName)
                    .AddSource("ACS.CommandProcessor") // Custom source for command processing
                    .AddSource("ACS.DomainNormalizer") // Custom source for normalizer operations
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
                            // Add tenant ID from environment
                            var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
                            if (!string.IsNullOrEmpty(tenantId))
                            {
                                activity.SetTag("tenant.id", tenantId);
                            }
                        };
                        options.EnrichWithException = (activity, exception) =>
                        {
                            activity.SetTag("error", true);
                            activity.SetTag("error.type", exception.GetType().Name);
                            activity.SetTag("error.message", exception.Message);
                        };
                    })
                    .AddEntityFrameworkCoreInstrumentation(options =>
                    {
                        options.SetDbStatementForText = true;
                        options.SetDbStatementForStoredProcedure = true;
                        options.EnrichWithIDbCommand = (activity, command) =>
                        {
                            // Add tenant ID to database operations
                            var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
                            if (!string.IsNullOrEmpty(tenantId))
                            {
                                activity.SetTag("tenant.id", tenantId);
                            }
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
    /// Creates a new activity for command processing operations
    /// </summary>
    public static Activity? StartCommandProcessingActivity(string commandType, string requestId)
    {
        var activity = ActivitySource.StartActivity($"command.process.{commandType}");
        activity?.SetTag("command.type", commandType);
        activity?.SetTag("command.request_id", requestId);
        activity?.SetTag("operation.category", "command_processing");
        
        var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
        if (!string.IsNullOrEmpty(tenantId))
        {
            activity?.SetTag("tenant.id", tenantId);
        }
        
        return activity;
    }
    
    /// <summary>
    /// Creates a new activity for domain normalizer operations
    /// </summary>
    public static Activity? StartNormalizerActivity(string normalizerType)
    {
        var activity = ActivitySource.StartActivity($"normalizer.{normalizerType}");
        activity?.SetTag("normalizer.type", normalizerType);
        activity?.SetTag("operation.category", "domain_normalization");
        
        var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
        if (!string.IsNullOrEmpty(tenantId))
        {
            activity?.SetTag("tenant.id", tenantId);
        }
        
        return activity;
    }
    
    /// <summary>
    /// Creates a new activity for gRPC service operations
    /// </summary>
    public static Activity? StartGrpcServiceActivity(string method)
    {
        var activity = ActivitySource.StartActivity($"grpc.service.{method}");
        activity?.SetTag("rpc.service", "VerticalService");
        activity?.SetTag("rpc.method", method);
        activity?.SetTag("rpc.system", "grpc");
        
        var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
        if (!string.IsNullOrEmpty(tenantId))
        {
            activity?.SetTag("tenant.id", tenantId);
        }
        
        return activity;
    }
    
    /// <summary>
    /// Creates a new activity for database operations
    /// </summary>
    public static Activity? StartDatabaseActivity(string operation, string? entityType = null)
    {
        var activity = ActivitySource.StartActivity($"database.{operation}");
        activity?.SetTag("db.operation.type", operation);
        activity?.SetTag("operation.category", "database");
        
        if (!string.IsNullOrEmpty(entityType))
        {
            activity?.SetTag("db.entity.type", entityType);
        }
        
        var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
        if (!string.IsNullOrEmpty(tenantId))
        {
            activity?.SetTag("tenant.id", tenantId);
        }
        
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
    /// Records performance metrics for command processing
    /// </summary>
    public static void RecordCommandMetrics(Activity? activity, TimeSpan processingTime, bool successful)
    {
        if (activity == null) return;
        
        activity.SetTag("command.processing_time_ms", processingTime.TotalMilliseconds);
        activity.SetTag("command.successful", successful);
        
        if (processingTime.TotalMilliseconds > 1000) // Log slow commands
        {
            activity.SetTag("command.slow", true);
        }
    }
    
    /// <summary>
    /// Records database operation metrics
    /// </summary>
    public static void RecordDatabaseMetrics(Activity? activity, int recordsAffected, TimeSpan queryTime)
    {
        if (activity == null) return;
        
        activity.SetTag("db.records_affected", recordsAffected);
        activity.SetTag("db.query_time_ms", queryTime.TotalMilliseconds);
        
        if (queryTime.TotalMilliseconds > 500) // Log slow queries
        {
            activity.SetTag("db.slow_query", true);
        }
    }
}