using ACS.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ACS.Infrastructure.Extensions;

/// <summary>
/// Extension methods for configuring monitoring dashboards
/// </summary>
public static class DashboardExtensions
{
    /// <summary>
    /// Add console monitoring dashboard to the service collection
    /// </summary>
    public static IServiceCollection AddConsoleDashboard(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Register the dashboard service
        services.AddSingleton<ConsoleDashboardService>();
        
        // Add as hosted service if enabled
        var enableDashboard = configuration.GetValue<bool>("Dashboard:Enabled", false);
        if (enableDashboard)
        {
            services.AddHostedService<ConsoleDashboardService>(provider => 
                provider.GetRequiredService<ConsoleDashboardService>());
        }
        
        // Configure dashboard options
        services.Configure<DashboardOptions>(
            configuration.GetSection("Dashboard"));
        
        return services;
    }
}

/// <summary>
/// Configuration options for the monitoring dashboard
/// </summary>
public class DashboardOptions
{
    /// <summary>
    /// Enable the console dashboard
    /// </summary>
    public bool Enabled { get; set; } = false;
    
    /// <summary>
    /// Dashboard refresh interval in milliseconds
    /// </summary>
    public int RefreshIntervalMs { get; set; } = 2000;
    
    /// <summary>
    /// Console width for dashboard rendering
    /// </summary>
    public int ConsoleWidth { get; set; } = 120;
    
    /// <summary>
    /// Console height for dashboard rendering
    /// </summary>
    public int ConsoleHeight { get; set; } = 30;
    
    /// <summary>
    /// Show detailed tenant metrics
    /// </summary>
    public bool ShowDetailedMetrics { get; set; } = true;
    
    /// <summary>
    /// Show system-level metrics
    /// </summary>
    public bool ShowSystemMetrics { get; set; } = true;
    
    /// <summary>
    /// Enable keyboard shortcuts
    /// </summary>
    public bool EnableKeyboardShortcuts { get; set; } = true;
    
    /// <summary>
    /// Color theme for the dashboard
    /// </summary>
    public string ColorTheme { get; set; } = "Default";
    
    /// <summary>
    /// Log dashboard events
    /// </summary>
    public bool LogEvents { get; set; } = false;
}