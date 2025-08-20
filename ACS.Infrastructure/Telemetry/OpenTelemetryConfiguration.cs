using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ACS.Infrastructure.Telemetry;

/// <summary>
/// Centralized OpenTelemetry configuration for the entire ACS system
/// </summary>
public static class OpenTelemetryConfiguration
{
    // Service Information
    public const string ServiceName = "ACS";
    public const string ServiceVersion = "1.0.0";
    public const string ServiceNamespace = "ACS.AccessControl";

    // Activity Sources for different components
    public static readonly ActivitySource WebApiActivitySource = new("ACS.WebApi");
    public static readonly ActivitySource ServiceActivitySource = new("ACS.Service");
    public static readonly ActivitySource InfrastructureActivitySource = new("ACS.Infrastructure");
    public static readonly ActivitySource PermissionActivitySource = new("ACS.PermissionEvaluation");
    public static readonly ActivitySource CommandActivitySource = new("ACS.CommandProcessor");
    public static readonly ActivitySource CacheActivitySource = new("ACS.Cache");
    public static readonly ActivitySource DatabaseActivitySource = new("ACS.Database");

    // Meters for different components
    public static readonly Meter WebApiMeter = new("ACS.WebApi", ServiceVersion);
    public static readonly Meter ServiceMeter = new("ACS.Service", ServiceVersion);
    public static readonly Meter InfrastructureMeter = new("ACS.Infrastructure", ServiceVersion);
    public static readonly Meter SecurityMeter = new("ACS.Security", ServiceVersion);

    // Custom metrics
    public static readonly Counter<long> RequestsTotal = WebApiMeter.CreateCounter<long>(
        "acs_requests_total", "Total number of HTTP requests");

    public static readonly Histogram<double> RequestDuration = WebApiMeter.CreateHistogram<double>(
        "acs_request_duration_seconds", "HTTP request duration in seconds");

    public static readonly Counter<long> PermissionChecksTotal = SecurityMeter.CreateCounter<long>(
        "acs_permission_checks_total", "Total number of permission checks");

    public static readonly Counter<long> PermissionDenialsTotal = SecurityMeter.CreateCounter<long>(
        "acs_permission_denials_total", "Total number of permission denials");

    public static readonly Histogram<double> PermissionCheckDuration = SecurityMeter.CreateHistogram<double>(
        "acs_permission_check_duration_seconds", "Permission check duration in seconds");

    public static readonly Counter<long> CacheHitsTotal = InfrastructureMeter.CreateCounter<long>(
        "acs_cache_hits_total", "Total number of cache hits");

    public static readonly Counter<long> CacheMissesTotal = InfrastructureMeter.CreateCounter<long>(
        "acs_cache_misses_total", "Total number of cache misses");

    public static readonly Counter<long> DatabaseOperationsTotal = InfrastructureMeter.CreateCounter<long>(
        "acs_database_operations_total", "Total number of database operations");

    public static readonly Histogram<double> DatabaseOperationDuration = InfrastructureMeter.CreateHistogram<double>(
        "acs_database_operation_duration_seconds", "Database operation duration in seconds");

    public static readonly Counter<long> CommandsProcessedTotal = ServiceMeter.CreateCounter<long>(
        "acs_commands_processed_total", "Total number of commands processed");

    public static readonly Histogram<double> CommandProcessingDuration = ServiceMeter.CreateHistogram<double>(
        "acs_command_processing_duration_seconds", "Command processing duration in seconds");

    public static readonly ObservableGauge<long> ActiveTenants = ServiceMeter.CreateObservableGauge<long>(
        "acs_active_tenants", "Number of active tenants");

    public static readonly ObservableGauge<long> ActiveUsers = ServiceMeter.CreateObservableGauge<long>(
        "acs_active_users", "Number of active users");

    /// <summary>
    /// Configures OpenTelemetry for ASP.NET Core applications
    /// </summary>
    public static void ConfigureOpenTelemetryForWebApi(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var resourceBuilder = CreateResourceBuilder(environment);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.Clear().Merge(resourceBuilder))
            .WithTracing(tracing =>
            {
                ConfigureTracing(tracing, configuration, "WebApi");
            })
            .WithMetrics(metrics =>
            {
                ConfigureMetrics(metrics, configuration, "WebApi");
            });

        // Configure logging with OpenTelemetry
        services.AddLogging(logging =>
        {
            logging.AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(resourceBuilder);
                ConfigureLogging(options, configuration);
            });
        });
    }

    /// <summary>
    /// Configures OpenTelemetry for VerticalHost applications
    /// </summary>
    public static void ConfigureOpenTelemetryForVerticalHost(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var resourceBuilder = CreateResourceBuilder(environment)
            .AddAttributes(new[]
            {
                new KeyValuePair<string, object>("tenant.id", Environment.GetEnvironmentVariable("TENANT_ID") ?? "unknown")
            });

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.Clear().Merge(resourceBuilder))
            .WithTracing(tracing =>
            {
                ConfigureTracing(tracing, configuration, "VerticalHost");
            })
            .WithMetrics(metrics =>
            {
                ConfigureMetrics(metrics, configuration, "VerticalHost");
            });

        // Configure logging with OpenTelemetry
        services.AddLogging(logging =>
        {
            logging.AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(resourceBuilder);
                ConfigureLogging(options, configuration);
            });
        });
    }

    /// <summary>
    /// Creates a resource builder with common attributes
    /// </summary>
    private static ResourceBuilder CreateResourceBuilder(IHostEnvironment environment)
    {
        return ResourceBuilder.CreateDefault()
            .AddService(ServiceName, ServiceNamespace, ServiceVersion)
            .AddAttributes(new Dictionary<string, object>
            {
                ["service.instance.id"] = Environment.MachineName,
                ["deployment.environment"] = environment.EnvironmentName,
                ["service.node.name"] = Environment.MachineName,
                ["process.pid"] = Environment.ProcessId,
                ["process.executable.name"] = Environment.ProcessPath ?? "unknown",
                ["host.name"] = Environment.MachineName,
                ["os.type"] = Environment.OSVersion.Platform.ToString(),
                ["runtime.name"] = ".NET",
                ["runtime.version"] = Environment.Version.ToString()
            });
    }

    /// <summary>
    /// Configures tracing with appropriate sources and exporters
    /// </summary>
    private static void ConfigureTracing(TracerProviderBuilder tracing, IConfiguration configuration, string component)
    {
        // Add activity sources
        tracing
            .AddSource(WebApiActivitySource.Name)
            .AddSource(ServiceActivitySource.Name)
            .AddSource(InfrastructureActivitySource.Name)
            .AddSource(PermissionActivitySource.Name)
            .AddSource(CommandActivitySource.Name)
            .AddSource(CacheActivitySource.Name)
            .AddSource(DatabaseActivitySource.Name);

        // Add built-in instrumentation
        tracing
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.Filter = httpContext =>
                {
                    var path = httpContext.Request.Path.Value;
                    return !path?.StartsWith("/health") == true && !path?.StartsWith("/metrics") == true;
                };
                options.EnrichWithHttpRequest = (activity, request) =>
                {
                    EnrichWithTenantContext(activity, request.Headers);
                };
                options.EnrichWithHttpResponse = (activity, response) =>
                {
                    activity.SetTag("http.response.status_code", response.StatusCode);
                };
                options.EnrichWithException = (activity, exception) =>
                {
                    RecordException(activity, exception);
                };
            })
            .AddHttpClientInstrumentation(options =>
            {
                options.RecordException = true;
                options.EnrichWithHttpRequestMessage = (activity, request) =>
                {
                    activity.SetTag("http.client.component", component);
                };
            })
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.SetDbStatementForText = configuration.GetValue<bool>("OpenTelemetry:IncludeDbStatements", false);
                options.SetDbStatementForStoredProcedure = true;
                options.EnrichWithIDbCommand = (activity, command) =>
                {
                    activity.SetTag("db.component", component);
                    var tenantId = Activity.Current?.GetTagItem("tenant.id")?.ToString();
                    if (!string.IsNullOrEmpty(tenantId))
                    {
                        activity.SetTag("tenant.id", tenantId);
                    }
                };
            })
            .AddGrpcClientInstrumentation();

        // Configure exporters
        ConfigureTracingExporters(tracing, configuration);
    }

    /// <summary>
    /// Configures metrics with appropriate meters and exporters
    /// </summary>
    private static void ConfigureMetrics(MeterProviderBuilder metrics, IConfiguration configuration, string component)
    {
        // Add meters
        metrics
            .AddMeter(WebApiMeter.Name)
            .AddMeter(ServiceMeter.Name)
            .AddMeter(InfrastructureMeter.Name)
            .AddMeter(SecurityMeter.Name);

        // Add built-in instrumentation
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddProcessInstrumentation();

        // Add custom views for better histogram buckets
        metrics
            .AddView("acs_request_duration_seconds", new ExplicitBucketHistogramConfiguration
            {
                Boundaries = new[] { 0.001, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10 }
            })
            .AddView("acs_permission_check_duration_seconds", new ExplicitBucketHistogramConfiguration
            {
                Boundaries = new[] { 0.0001, 0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25 }
            })
            .AddView("acs_database_operation_duration_seconds", new ExplicitBucketHistogramConfiguration
            {
                Boundaries = new[] { 0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5 }
            });

        // Configure exporters
        ConfigureMetricsExporters(metrics, configuration);
    }

    /// <summary>
    /// Configures logging with OpenTelemetry
    /// </summary>
    private static void ConfigureLogging(OpenTelemetryLoggerOptions options, IConfiguration configuration)
    {
        options.IncludeScopes = true;
        options.IncludeFormattedMessage = true;

        // Configure log exporters
        ConfigureLoggingExporters(options, configuration);
    }

    /// <summary>
    /// Configures tracing exporters based on configuration
    /// </summary>
    private static void ConfigureTracingExporters(TracerProviderBuilder tracing, IConfiguration configuration)
    {
        var exporters = configuration.GetSection("OpenTelemetry:Tracing:Exporters").Get<string[]>() ?? new[] { "Console" };

        foreach (var exporter in exporters)
        {
            switch (exporter.ToLowerInvariant())
            {
                case "otlp":
                    var otlpEndpoint = configuration.GetValue<string>("OpenTelemetry:OtlpEndpoint") ?? "http://localhost:4317";
                    tracing.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                        options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                        options.Headers = configuration.GetValue<string>("OpenTelemetry:OtlpHeaders") ?? string.Empty;
                    });
                    break;

                case "jaeger":
                    var jaegerEndpoint = configuration.GetValue<string>("OpenTelemetry:JaegerEndpoint") ?? "http://localhost:14268/api/traces";
                    tracing.AddJaegerExporter(options =>
                    {
                        options.Endpoint = new Uri(jaegerEndpoint);
                    });
                    break;

                case "console":
                    tracing.AddConsoleExporter();
                    break;
            }
        }
    }

    /// <summary>
    /// Configures metrics exporters based on configuration
    /// </summary>
    private static void ConfigureMetricsExporters(MeterProviderBuilder metrics, IConfiguration configuration)
    {
        var exporters = configuration.GetSection("OpenTelemetry:Metrics:Exporters").Get<string[]>() ?? new[] { "Console" };

        foreach (var exporter in exporters)
        {
            switch (exporter.ToLowerInvariant())
            {
                case "otlp":
                    var otlpEndpoint = configuration.GetValue<string>("OpenTelemetry:OtlpEndpoint") ?? "http://localhost:4317";
                    metrics.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                        options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    });
                    break;

                case "prometheus":
                    var prometheusEndpoint = configuration.GetValue<string>("OpenTelemetry:PrometheusEndpoint") ?? "/metrics";
                    metrics.AddPrometheusExporter(options =>
                    {
                        options.HttpListenerPrefixes = new[] { $"http://localhost:9090{prometheusEndpoint}" };
                    });
                    break;

                case "console":
                    metrics.AddConsoleExporter();
                    break;
            }
        }
    }

    /// <summary>
    /// Configures logging exporters based on configuration
    /// </summary>
    private static void ConfigureLoggingExporters(OpenTelemetryLoggerOptions options, IConfiguration configuration)
    {
        var exporters = configuration.GetSection("OpenTelemetry:Logging:Exporters").Get<string[]>() ?? new[] { "Console" };

        foreach (var exporter in exporters)
        {
            switch (exporter.ToLowerInvariant())
            {
                case "otlp":
                    var otlpEndpoint = configuration.GetValue<string>("OpenTelemetry:OtlpEndpoint") ?? "http://localhost:4317";
                    options.AddOtlpExporter(otlpOptions =>
                    {
                        otlpOptions.Endpoint = new Uri(otlpEndpoint);
                        otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    });
                    break;

                case "console":
                    options.AddConsoleExporter();
                    break;
            }
        }
    }

    /// <summary>
    /// Enriches activity with tenant context from HTTP headers
    /// </summary>
    private static void EnrichWithTenantContext(Activity activity, IHeaderDictionary headers)
    {
        if (headers.TryGetValue("X-Tenant-ID", out var tenantId))
        {
            activity.SetTag("tenant.id", tenantId.FirstOrDefault());
        }

        if (headers.TryGetValue("X-User-ID", out var userId))
        {
            activity.SetTag("user.id", userId.FirstOrDefault());
        }

        if (headers.TryGetValue("X-Correlation-ID", out var correlationId))
        {
            activity.SetTag("correlation.id", correlationId.FirstOrDefault());
        }
    }

    /// <summary>
    /// Records exception information in activity
    /// </summary>
    private static void RecordException(Activity activity, Exception exception)
    {
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.SetTag("error", true);
        activity.SetTag("error.type", exception.GetType().FullName);
        activity.SetTag("error.message", exception.Message);

        if (exception.StackTrace != null)
        {
            activity.SetTag("error.stack", exception.StackTrace);
        }
    }

    /// <summary>
    /// Records metrics for HTTP requests
    /// </summary>
    public static void RecordHttpRequest(string method, string route, int statusCode, double duration, string? tenantId = null)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("http.method", method),
            new KeyValuePair<string, object?>("http.route", route),
            new KeyValuePair<string, object?>("http.status_code", statusCode),
            new KeyValuePair<string, object?>("tenant.id", tenantId ?? "unknown")
        };

        RequestsTotal.Add(1, tags);
        RequestDuration.Record(duration, tags);
    }

    /// <summary>
    /// Records metrics for permission checks
    /// </summary>
    public static void RecordPermissionCheck(bool allowed, double duration, string? tenantId = null, string? reason = null)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("permission.result", allowed ? "allowed" : "denied"),
            new KeyValuePair<string, object?>("tenant.id", tenantId ?? "unknown")
        };

        PermissionChecksTotal.Add(1, tags);
        PermissionCheckDuration.Record(duration, tags);

        if (!allowed)
        {
            var denialTags = new[]
            {
                new KeyValuePair<string, object?>("denial.reason", reason ?? "insufficient_permissions"),
                new KeyValuePair<string, object?>("tenant.id", tenantId ?? "unknown")
            };
            PermissionDenialsTotal.Add(1, denialTags);
        }
    }

    /// <summary>
    /// Records cache metrics
    /// </summary>
    public static void RecordCacheOperation(bool hit, string cacheType, string? tenantId = null)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("cache.type", cacheType),
            new KeyValuePair<string, object?>("tenant.id", tenantId ?? "unknown")
        };

        if (hit)
        {
            CacheHitsTotal.Add(1, tags);
        }
        else
        {
            CacheMissesTotal.Add(1, tags);
        }
    }

    /// <summary>
    /// Records database operation metrics
    /// </summary>
    public static void RecordDatabaseOperation(string operation, double duration, bool success, string? entityType = null, string? tenantId = null)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("db.operation", operation),
            new KeyValuePair<string, object?>("db.success", success),
            new KeyValuePair<string, object?>("db.entity_type", entityType ?? "unknown"),
            new KeyValuePair<string, object?>("tenant.id", tenantId ?? "unknown")
        };

        DatabaseOperationsTotal.Add(1, tags);
        DatabaseOperationDuration.Record(duration, tags);
    }

    /// <summary>
    /// Records command processing metrics
    /// </summary>
    public static void RecordCommandProcessing(string commandType, double duration, bool success, string? tenantId = null)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("command.type", commandType),
            new KeyValuePair<string, object?>("command.success", success),
            new KeyValuePair<string, object?>("tenant.id", tenantId ?? "unknown")
        };

        CommandsProcessedTotal.Add(1, tags);
        CommandProcessingDuration.Record(duration, tags);
    }
}