using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;

namespace ACS.Infrastructure.Configuration;

/// <summary>
/// Service for handling configuration hot-reload functionality
/// </summary>
public interface IConfigurationHotReloadService
{
    void RegisterReloadHandler<TOptions>(Action<TOptions> handler) where TOptions : class;
    void RegisterReloadHandler(string configSection, Action<IConfiguration> handler);
    void UnregisterReloadHandler<TOptions>() where TOptions : class;
    void TriggerReload();
    bool IsHotReloadEnabled { get; }
}

public class ConfigurationHotReloadService : IConfigurationHotReloadService, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ConfigurationHotReloadService> _logger;
    private readonly ConcurrentDictionary<Type, List<Delegate>> _typeHandlers;
    private readonly ConcurrentDictionary<string, List<Action<IConfiguration>>> _sectionHandlers;
    private readonly List<IDisposable> _changeTokenRegistrations;
    private readonly bool _hotReloadEnabled;

    public bool IsHotReloadEnabled => _hotReloadEnabled;

    public ConfigurationHotReloadService(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<ConfigurationHotReloadService> logger)
    {
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _typeHandlers = new ConcurrentDictionary<Type, List<Delegate>>();
        _sectionHandlers = new ConcurrentDictionary<string, List<Action<IConfiguration>>>();
        _changeTokenRegistrations = new List<IDisposable>();
        
        // Check if hot-reload is enabled
        _hotReloadEnabled = configuration.GetValue<bool>("Configuration:EnableHotReload", true);
        
        if (_hotReloadEnabled)
        {
            RegisterChangeCallbacks();
            _logger.LogInformation("Configuration hot-reload service initialized and enabled");
        }
        else
        {
            _logger.LogInformation("Configuration hot-reload is disabled");
        }
    }

    public void RegisterReloadHandler<TOptions>(Action<TOptions> handler) where TOptions : class
    {
        if (!_hotReloadEnabled)
        {
            _logger.LogDebug("Hot-reload is disabled, handler not registered for {Type}", typeof(TOptions).Name);
            return;
        }

        var handlers = _typeHandlers.GetOrAdd(typeof(TOptions), _ => new List<Delegate>());
        lock (handlers)
        {
            handlers.Add(handler);
        }
        
        _logger.LogDebug("Registered hot-reload handler for {Type}", typeof(TOptions).Name);
    }

    public void RegisterReloadHandler(string configSection, Action<IConfiguration> handler)
    {
        if (!_hotReloadEnabled)
        {
            _logger.LogDebug("Hot-reload is disabled, handler not registered for section {Section}", configSection);
            return;
        }

        var handlers = _sectionHandlers.GetOrAdd(configSection, _ => new List<Action<IConfiguration>>());
        lock (handlers)
        {
            handlers.Add(handler);
        }
        
        _logger.LogDebug("Registered hot-reload handler for section {Section}", configSection);
    }

    public void UnregisterReloadHandler<TOptions>() where TOptions : class
    {
        if (_typeHandlers.TryRemove(typeof(TOptions), out var handlers))
        {
            _logger.LogDebug("Unregistered all hot-reload handlers for {Type}", typeof(TOptions).Name);
        }
    }

    public void TriggerReload()
    {
        if (!_hotReloadEnabled)
        {
            _logger.LogDebug("Hot-reload is disabled, reload trigger ignored");
            return;
        }

        _logger.LogInformation("Manually triggering configuration reload");
        OnConfigurationChanged();
    }

    private void RegisterChangeCallbacks()
    {
        // Register for configuration changes
        var changeToken = _configuration.GetReloadToken();
        
        var registration = ChangeToken.OnChange(
            () => _configuration.GetReloadToken(),
            OnConfigurationChanged);
        
        _changeTokenRegistrations.Add(registration);
    }

    private void OnConfigurationChanged()
    {
        _logger.LogInformation("Configuration changed, reloading handlers");
        
        // Reload type-based handlers
        foreach (var kvp in _typeHandlers)
        {
            var optionsType = kvp.Key;
            var handlers = kvp.Value;
            
            try
            {
                // Get the options monitor for this type
                var monitorType = typeof(IOptionsMonitor<>).MakeGenericType(optionsType);
                var monitor = _serviceProvider.GetService(monitorType);
                
                if (monitor != null)
                {
                    var currentValueProperty = monitorType.GetProperty("CurrentValue");
                    var currentValue = currentValueProperty?.GetValue(monitor);
                    
                    if (currentValue != null)
                    {
                        lock (handlers)
                        {
                            foreach (var handler in handlers)
                            {
                                try
                                {
                                    handler.DynamicInvoke(currentValue);
                                    _logger.LogDebug("Executed hot-reload handler for {Type}", optionsType.Name);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error executing hot-reload handler for {Type}", optionsType.Name);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing hot-reload for type {Type}", optionsType.Name);
            }
        }
        
        // Reload section-based handlers
        foreach (var kvp in _sectionHandlers)
        {
            var section = kvp.Key;
            var handlers = kvp.Value;
            var sectionConfig = _configuration.GetSection(section);
            
            lock (handlers)
            {
                foreach (var handler in handlers)
                {
                    try
                    {
                        handler(sectionConfig);
                        _logger.LogDebug("Executed hot-reload handler for section {Section}", section);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing hot-reload handler for section {Section}", section);
                    }
                }
            }
        }
        
        _logger.LogInformation("Configuration reload completed");
    }

    public void Dispose()
    {
        foreach (var registration in _changeTokenRegistrations)
        {
            registration?.Dispose();
        }
        
        _changeTokenRegistrations.Clear();
        _typeHandlers.Clear();
        _sectionHandlers.Clear();
    }
}

/// <summary>
/// Background service for monitoring configuration changes
/// </summary>
public class ConfigurationMonitorService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IConfigurationHotReloadService _hotReloadService;
    private readonly ILogger<ConfigurationMonitorService> _logger;
    private readonly List<IDisposable> _changeRegistrations;

    public ConfigurationMonitorService(
        IConfiguration configuration,
        IConfigurationHotReloadService hotReloadService,
        ILogger<ConfigurationMonitorService> logger)
    {
        _configuration = configuration;
        _hotReloadService = hotReloadService;
        _logger = logger;
        _changeRegistrations = new List<IDisposable>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_hotReloadService.IsHotReloadEnabled)
        {
            _logger.LogInformation("Configuration hot-reload is disabled, monitoring service will not run");
            return;
        }

        _logger.LogInformation("Configuration monitoring service started");
        
        // Monitor specific critical configuration sections
        MonitorCriticalSections();
        
        // Keep the service running
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        
        _logger.LogInformation("Configuration monitoring service stopped");
    }

    private void MonitorCriticalSections()
    {
        // Monitor JWT configuration changes
        var jwtToken = ChangeToken.OnChange(
            () => _configuration.GetSection("Authentication:Jwt").GetReloadToken(),
            () =>
            {
                _logger.LogWarning("JWT configuration changed - authentication may be affected");
                // Could trigger additional actions like clearing JWT cache
            });
        _changeRegistrations.Add(jwtToken);
        
        // Monitor database connection string changes
        var dbToken = ChangeToken.OnChange(
            () => _configuration.GetSection("ConnectionStrings").GetReloadToken(),
            () =>
            {
                _logger.LogWarning("Database connection strings changed - connections will be recycled");
                // Database connections will automatically be recycled by EF Core
            });
        _changeRegistrations.Add(dbToken);
        
        // Monitor rate limiting configuration
        var rateLimitToken = ChangeToken.OnChange(
            () => _configuration.GetSection("RateLimit").GetReloadToken(),
            () =>
            {
                _logger.LogInformation("Rate limiting configuration changed");
            });
        _changeRegistrations.Add(rateLimitToken);
        
        // Monitor feature flags
        var featureFlagToken = ChangeToken.OnChange(
            () => _configuration.GetSection("FeatureFlags").GetReloadToken(),
            () =>
            {
                _logger.LogInformation("Feature flags changed");
            });
        _changeRegistrations.Add(featureFlagToken);
    }

    public override void Dispose()
    {
        foreach (var registration in _changeRegistrations)
        {
            registration?.Dispose();
        }
        
        base.Dispose();
    }
}

/// <summary>
/// Options for configuration hot-reload
/// </summary>
public class ConfigurationHotReloadOptions
{
    public bool Enabled { get; set; } = true;
    public List<string> MonitoredSections { get; set; } = new();
    public int DebounceMilliseconds { get; set; } = 1000;
    public bool LogChanges { get; set; } = true;
    public bool ValidateOnReload { get; set; } = true;
}

/// <summary>
/// Extension methods for configuration hot-reload
/// </summary>
public static class ConfigurationHotReloadExtensions
{
    public static IServiceCollection AddConfigurationHotReload(
        this IServiceCollection services,
        Action<ConfigurationHotReloadOptions>? configureOptions = null)
    {
        var options = new ConfigurationHotReloadOptions();
        configureOptions?.Invoke(options);
        
        services.AddSingleton<IConfigurationHotReloadService, ConfigurationHotReloadService>();
        
        if (options.Enabled)
        {
            services.AddHostedService<ConfigurationMonitorService>();
        }
        
        services.Configure<ConfigurationHotReloadOptions>(opt =>
        {
            opt.Enabled = options.Enabled;
            opt.MonitoredSections = options.MonitoredSections;
            opt.DebounceMilliseconds = options.DebounceMilliseconds;
            opt.LogChanges = options.LogChanges;
            opt.ValidateOnReload = options.ValidateOnReload;
        });
        
        return services;
    }

    public static IServiceCollection ConfigureWithHotReload<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<TOptions>? onChange = null) where TOptions : class
    {
        services.Configure<TOptions>(configuration);
        
        if (onChange != null)
        {
            services.PostConfigure<TOptions>(options =>
            {
                var hotReloadService = services.BuildServiceProvider()
                    .GetService<IConfigurationHotReloadService>();
                
                hotReloadService?.RegisterReloadHandler(onChange);
            });
        }
        
        return services;
    }
}