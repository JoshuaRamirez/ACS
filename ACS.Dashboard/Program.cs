using ACS.Infrastructure.DependencyInjection;
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

        // Configure all services using centralized registration
        using var loggerFactory = LoggerFactory.Create(options => options.AddConsole());
        var logger = loggerFactory.CreateLogger<Program>();
        services.ConfigureServices(configuration, logger, "Dashboard");

        // Add dashboard-specific services
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
        try
        {
            // Discover running tenant processes
            var processes = System.Diagnostics.Process.GetProcessesByName("ACS.VerticalHost");
            
            var discoveredTenants = new List<string>();
            
            foreach (var process in processes)
            {
                try
                {
                    // Extract tenant ID from process arguments or environment variables
                    var tenantId = ExtractTenantIdFromProcess(process);
                    if (!string.IsNullOrEmpty(tenantId))
                    {
                        discoveredTenants.Add(tenantId);
                        _logger.LogTrace("Discovered tenant process: {TenantId} (PID: {ProcessId})", 
                            tenantId, process.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to extract tenant ID from process {ProcessId}", process.Id);
                }
            }
            
            if (discoveredTenants.Any())
            {
                _logger.LogDebug("Discovered {TenantCount} active tenant processes: {TenantIds}", 
                    discoveredTenants.Count, string.Join(", ", discoveredTenants));
                
                // Update internal tenant tracking
                await UpdateTenantRegistryAsync(discoveredTenants);
            }
            else
            {
                _logger.LogTrace("No active tenant processes found");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during tenant process discovery");
        }
    }
    
    private string? ExtractTenantIdFromProcess(System.Diagnostics.Process process)
    {
        try
        {
            // In a real implementation, you might:
            // 1. Read from process command line arguments
            // 2. Check environment variables
            // 3. Query via gRPC health check endpoint
            // 4. Read from shared configuration
            
            // For now, simulate by using process ID as base
            // This would be replaced with actual tenant identification logic
            return $"tenant-{process.Id % 1000}";
        }
        catch
        {
            return null;
        }
    }
    
    private async Task UpdateTenantRegistryAsync(List<string> activeTenantIds)
    {
        try
        {
            // Update tenant registry with discovered processes
            // This could involve:
            // 1. Updating a shared cache (Redis)
            // 2. Notifying other services
            // 3. Updating dashboard state
            
            _logger.LogTrace("Updated tenant registry with {Count} active tenants", activeTenantIds.Count);
            await Task.Delay(10); // Simulate registry update
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update tenant registry");
        }
    }
}