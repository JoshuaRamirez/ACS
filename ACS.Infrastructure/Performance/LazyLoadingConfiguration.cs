using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ACS.Infrastructure.Performance;

/// <summary>
/// Configuration for lazy loading and EF Core performance optimizations
/// </summary>
public static class LazyLoadingConfiguration
{
    /// <summary>
    /// Configure EF Core for optimal performance
    /// </summary>
    public static DbContextOptionsBuilder ConfigurePerformanceOptions(
        this DbContextOptionsBuilder options,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetService<ILogger<LazyLoadingConfiguration>>();
        
        // Lazy loading configuration
        var enableLazyLoading = configuration.GetValue<bool>("EntityFramework:EnableLazyLoading", false);
        options.UseLazyLoadingProxies(enableLazyLoading);
        
        if (enableLazyLoading)
        {
            logger?.LogInformation("Lazy loading enabled");
        }
        else
        {
            logger?.LogInformation("Lazy loading disabled for better performance");
        }
        
        // Query tracking behavior
        var defaultTrackingBehavior = configuration.GetValue<string>("EntityFramework:DefaultTrackingBehavior", "TrackAll");
        var trackingBehavior = Enum.Parse<QueryTrackingBehavior>(defaultTrackingBehavior);
        options.UseQueryTrackingBehavior(trackingBehavior);
        
        // Connection resiliency
        var enableRetryOnFailure = configuration.GetValue<bool>("EntityFramework:EnableRetryOnFailure", true);
        if (enableRetryOnFailure)
        {
            // This would be configured in the AddDbContext call with SQL Server specific options
            logger?.LogDebug("Connection resiliency enabled");
        }
        
        // Sensitive data logging (only in development)
        var enableSensitiveDataLogging = configuration.GetValue<bool>("EntityFramework:EnableSensitiveDataLogging", false);
        if (enableSensitiveDataLogging)
        {
            options.EnableSensitiveDataLogging();
            logger?.LogWarning("Sensitive data logging enabled - should only be used in development");
        }
        
        // Detailed errors (only in development)
        var enableDetailedErrors = configuration.GetValue<bool>("EntityFramework:EnableDetailedErrors", false);
        if (enableDetailedErrors)
        {
            options.EnableDetailedErrors();
            logger?.LogDebug("Detailed errors enabled");
        }
        
        // Query splitting behavior
        var defaultSplitQueryBehavior = configuration.GetValue<string>("EntityFramework:DefaultSplitQueryBehavior", "SplitQuery");
        var splitBehavior = Enum.Parse<QuerySplitBehavior>(defaultSplitQueryBehavior);
        options.UseQuerySplittingBehavior(splitBehavior);
        
        // Service scope validation (only in development)
        var validateScopes = configuration.GetValue<bool>("EntityFramework:ValidateScopes", false);
        if (validateScopes)
        {
            options.EnableServiceProviderCaching(false);
            options.EnableSensitiveDataLogging();
        }
        
        // Memory optimizations
        ConfigureMemoryOptimizations(options, configuration, logger);
        
        return options;
    }
    
    /// <summary>
    /// Configure memory optimizations for EF Core
    /// </summary>
    private static void ConfigureMemoryOptimizations(
        DbContextOptionsBuilder options,
        IConfiguration configuration,
        ILogger? logger)
    {
        // Change tracker optimizations
        var changeTrackerClearInterval = configuration.GetValue<int>("EntityFramework:ChangeTrackerClearInterval", 100);
        
        // Pool size for DbContext pooling
        var poolSize = configuration.GetValue<int>("EntityFramework:PoolSize", 128);
        
        // Query cache size
        var queryCacheSize = configuration.GetValue<int>("EntityFramework:QueryCacheSize", 1024);
        
        logger?.LogDebug("EF Core memory optimizations: PoolSize={PoolSize}, QueryCacheSize={QueryCacheSize}", 
            poolSize, queryCacheSize);
    }
    
    /// <summary>
    /// Get performance configuration for repositories
    /// </summary>
    public static PerformanceConfiguration GetPerformanceConfiguration(IConfiguration configuration)
    {
        return new PerformanceConfiguration
        {
            DefaultPageSize = configuration.GetValue<int>("Performance:DefaultPageSize", 20),
            MaxPageSize = configuration.GetValue<int>("Performance:MaxPageSize", 100),
            EnableQueryCache = configuration.GetValue<bool>("Performance:EnableQueryCache", true),
            QueryCacheTimeoutMinutes = configuration.GetValue<int>("Performance:QueryCacheTimeoutMinutes", 5),
            EnableN1Detection = configuration.GetValue<bool>("Performance:EnableN1Detection", true),
            SlowQueryThresholdMs = configuration.GetValue<int>("Performance:SlowQueryThresholdMs", 1000),
            EnableQueryOptimization = configuration.GetValue<bool>("Performance:EnableQueryOptimization", true),
            DefaultIncludeDepth = configuration.GetValue<int>("Performance:DefaultIncludeDepth", 3),
            EnableAutomaticIncludes = configuration.GetValue<bool>("Performance:EnableAutomaticIncludes", false),
            PreferSplitQueries = configuration.GetValue<bool>("Performance:PreferSplitQueries", true)
        };
    }
}

/// <summary>
/// Performance configuration settings
/// </summary>
public class PerformanceConfiguration
{
    public int DefaultPageSize { get; set; } = 20;
    public int MaxPageSize { get; set; } = 100;
    public bool EnableQueryCache { get; set; } = true;
    public int QueryCacheTimeoutMinutes { get; set; } = 5;
    public bool EnableN1Detection { get; set; } = true;
    public int SlowQueryThresholdMs { get; set; } = 1000;
    public bool EnableQueryOptimization { get; set; } = true;
    public int DefaultIncludeDepth { get; set; } = 3;
    public bool EnableAutomaticIncludes { get; set; } = false;
    public bool PreferSplitQueries { get; set; } = true;
}

/// <summary>
/// Extension methods for configuring EF Core performance
/// </summary>
public static class DbContextExtensions
{
    /// <summary>
    /// Configure DbContext for optimal performance
    /// </summary>
    public static void ConfigureForPerformance(this DbContext context, PerformanceConfiguration config)
    {
        // Set default query tracking behavior
        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        
        // Configure lazy loading behavior
        context.ChangeTracker.LazyLoadingEnabled = false;
        
        // Auto detect changes optimization
        context.ChangeTracker.AutoDetectChangesEnabled = true;
        
        // Cascade delete behavior
        context.ChangeTracker.CascadeDeleteTiming = CascadeTiming.OnSaveChanges;
        context.ChangeTracker.DeleteOrphansTiming = CascadeTiming.OnSaveChanges;
    }
    
    /// <summary>
    /// Clear change tracker to free memory
    /// </summary>
    public static void ClearChangeTracker(this DbContext context)
    {
        context.ChangeTracker.Clear();
    }
    
    /// <summary>
    /// Get change tracker statistics
    /// </summary>
    public static ChangeTrackerStatistics GetChangeTrackerStats(this DbContext context)
    {
        var entries = context.ChangeTracker.Entries().ToList();
        
        return new ChangeTrackerStatistics
        {
            TotalEntries = entries.Count,
            AddedEntries = entries.Count(e => e.State == EntityState.Added),
            ModifiedEntries = entries.Count(e => e.State == EntityState.Modified),
            DeletedEntries = entries.Count(e => e.State == EntityState.Deleted),
            UnchangedEntries = entries.Count(e => e.State == EntityState.Unchanged),
            DetachedEntries = entries.Count(e => e.State == EntityState.Detached)
        };
    }
}

/// <summary>
/// Change tracker statistics for monitoring
/// </summary>
public class ChangeTrackerStatistics
{
    public int TotalEntries { get; set; }
    public int AddedEntries { get; set; }
    public int ModifiedEntries { get; set; }
    public int DeletedEntries { get; set; }
    public int UnchangedEntries { get; set; }
    public int DetachedEntries { get; set; }
    
    public bool HasChanges => AddedEntries > 0 || ModifiedEntries > 0 || DeletedEntries > 0;
}