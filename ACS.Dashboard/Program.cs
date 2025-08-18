using ACS.Infrastructure.Extensions;
using ACS.Infrastructure.Services;
using ACS.Service.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ACS.Dashboard;

/// <summary>
/// Standalone dashboard application for monitoring ACS tenant processes
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.Title = "ACS Tenant Process Monitor";
        
        try
        {
            var builder = Host.CreateApplicationBuilder(args);

            // Configure services
            ConfigureServices(builder.Services, builder.Configuration);

            // Build and run the host
            using var host = builder.Build();
            
            Console.WriteLine("Starting ACS Tenant Process Monitor...");
            Console.WriteLine("Press ESC or Ctrl+Q to exit");
            Console.WriteLine();
            
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Dashboard startup failed: {ex.Message}");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Add logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddConsole(options =>
            {
                options.LogToStandardErrorThreshold = LogLevel.Error;
            });
        });

        // Add tenant configuration (for demo purposes, use a default tenant)
        services.AddSingleton<TenantConfiguration>(_ => new TenantConfiguration 
        { 
            TenantId = "dashboard-monitor" 
        });

        // Add performance metrics (simplified for dashboard)
        services.AddPerformanceMetrics(configuration);

        // Add console dashboard
        services.AddConsoleDashboard(configuration);

        // Add multi-tenant discovery service (mock for now)
        services.AddSingleton<MultiTenantDiscoveryService>();
        services.AddHostedService<MultiTenantDiscoveryService>(provider => 
            provider.GetRequiredService<MultiTenantDiscoveryService>());
    }
}

/// <summary>
/// Service that discovers and monitors multiple tenant processes
/// </summary>
public class MultiTenantDiscoveryService : BackgroundService
{
    private readonly ILogger<MultiTenantDiscoveryService> _logger;
    private readonly IConfiguration _configuration;

    public MultiTenantDiscoveryService(
        ILogger<MultiTenantDiscoveryService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Multi-tenant discovery service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // In a real implementation, this would discover tenant processes
                // For now, we'll just simulate monitoring activity
                await DiscoverTenantProcessesAsync();
                await Task.Delay(5000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in tenant discovery service");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("Multi-tenant discovery service stopped");
    }

    private async Task DiscoverTenantProcessesAsync()
    {
        // Simulate tenant process discovery
        var processes = System.Diagnostics.Process.GetProcessesByName("ACS.VerticalHost");
        
        if (processes.Length > 0)
        {
            _logger.LogDebug("Found {ProcessCount} ACS.VerticalHost processes", processes.Length);
        }

        await Task.CompletedTask;
    }
}