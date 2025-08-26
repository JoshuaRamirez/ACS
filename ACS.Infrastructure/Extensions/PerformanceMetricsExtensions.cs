using ACS.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry;

namespace ACS.Infrastructure.Extensions;

/// <summary>
/// Extension methods for configuring performance metrics collection
/// </summary>
public static class PerformanceMetricsExtensions
{
    /// <summary>
    /// Configure comprehensive performance metrics collection
    /// </summary>
    public static IServiceCollection AddPerformanceMetrics(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // TODO: Implement PerformanceMetricsService when requirements are defined
        // For now, configure performance metrics settings only
        services.Configure<PerformanceMetricsOptions>(
            configuration.GetSection("PerformanceMetrics"));
            
        // Log that performance metrics service is not yet implemented
        services.AddLogging();
        
        return services;
    }
    
    /// <summary>
    /// Configure OpenTelemetry metrics with performance counters
    /// </summary>
    public static OpenTelemetryBuilder AddPerformanceMetrics(
        this OpenTelemetryBuilder builder,
        IConfiguration configuration)
    {
        return builder.WithMetrics(metrics =>
        {
            metrics
                .AddMeter("ACS.VerticalHost.*")
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation();
                
            // Configure exporters based on configuration
            var exporterType = configuration.GetValue<string>("OpenTelemetry:MetricsExporter", "Console");
            switch (exporterType.ToLowerInvariant())
            {
                case "console":
                    metrics.AddConsoleExporter();
                    break;
                case "otlp":
                    metrics.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(configuration.GetValue<string>("OpenTelemetry:OtlpEndpoint", "http://localhost:4317")!);
                    });
                    break;
                default:
                    metrics.AddConsoleExporter();
                    break;
            }
        });
    }
}

/// <summary>
/// Configuration options for performance metrics
/// </summary>
public class PerformanceMetricsOptions
{
    /// <summary>
    /// Collection interval in milliseconds
    /// </summary>
    public int CollectionIntervalMs { get; set; } = 5000;
    
    /// <summary>
    /// Enable system-level metrics collection
    /// </summary>
    public bool EnableSystemMetrics { get; set; } = true;
    
    /// <summary>
    /// Enable application-level metrics collection
    /// </summary>
    public bool EnableApplicationMetrics { get; set; } = true;
    
    /// <summary>
    /// Enable detailed database metrics
    /// </summary>
    public bool EnableDatabaseMetrics { get; set; } = true;
    
    /// <summary>
    /// Enable cache metrics
    /// </summary>
    public bool EnableCacheMetrics { get; set; } = true;
    
    /// <summary>
    /// Enable gRPC metrics
    /// </summary>
    public bool EnableGrpcMetrics { get; set; } = true;
    
    /// <summary>
    /// Metrics retention period in days
    /// </summary>
    public int RetentionDays { get; set; } = 30;
    
    /// <summary>
    /// Custom metrics configuration
    /// </summary>
    public Dictionary<string, object> CustomMetrics { get; set; } = new();
}