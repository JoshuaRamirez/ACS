using ACS.Alerting;
using ACS.Alerting.Channels;

namespace ACS.WebApi.Extensions;

/// <summary>
/// Extension methods for registering alerting services
/// </summary>
public static class AlertingServiceExtensions
{
    /// <summary>
    /// Register alerting and notification services
    /// </summary>
    public static IServiceCollection AddAlerting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Core alerting services
        services.AddScoped<IAlertingService, AlertingService>();
        services.AddSingleton<IAlertThrottler, InMemoryAlertThrottler>();
        services.AddSingleton<IAlertStorage, InMemoryAlertStorage>();
        
        // Notification routing
        services.AddScoped<INotificationRouter, NotificationRouter>();
        
        // Notification channels
        services.AddScoped<INotificationChannel, EmailNotificationChannel>();
        services.AddScoped<INotificationChannel, SlackNotificationChannel>();
        
        // Register notification channels by type
        services.AddScoped<EmailNotificationChannel>();
        services.AddScoped<SlackNotificationChannel>();
        
        // Configure HttpClient for external API calls (Slack, webhooks, etc.)
        services.AddHttpClient<SlackNotificationChannel>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "ACS-AlertSystem/1.0");
        });
        
        // Health check alerting integration
        services.Configure<HealthCheckAlertingOptions>(
            configuration.GetSection(HealthCheckAlertingOptions.SectionName));
        services.AddHostedService<HealthCheckAlertingService>();
        
        return services;
    }
}