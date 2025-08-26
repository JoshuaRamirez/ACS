using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Data.Common;

namespace ACS.Infrastructure.Performance;

/// <summary>
/// Configuration for database performance optimizations
/// </summary>
public static class DatabasePerformanceConfiguration
{
    /// <summary>
    /// Configure database connection pooling and command batching
    /// </summary>
    public static void ConfigureDatabasePerformance(
        this DbContextOptionsBuilder optionsBuilder,
        IConfiguration configuration,
        ILogger? logger = null)
    {
        var perfSettings = configuration.GetSection("Database:Performance").Get<DatabasePerformanceSettings>() 
            ?? new DatabasePerformanceSettings();

        // Configure SQL Server specific options
        optionsBuilder.UseSqlServer(options =>
        {
            // Connection resiliency
            options.EnableRetryOnFailure(
                maxRetryCount: perfSettings.MaxRetryCount,
                maxRetryDelay: TimeSpan.FromSeconds(perfSettings.MaxRetryDelaySeconds),
                errorNumbersToAdd: perfSettings.TransientErrorNumbers);

            // Command timeout
            options.CommandTimeout(perfSettings.CommandTimeoutSeconds);

            // Use row number for paging (better performance for large datasets)
            if (perfSettings.UseRowNumberForPaging)
            {
                options.UseCompatibilityLevel(120);
            }
        });

        // Configure connection pooling via connection string
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(connectionString))
        {
            var builder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            // Add pooling parameters if not already present
            if (!builder.ContainsKey("Min Pool Size"))
                builder["Min Pool Size"] = perfSettings.MinPoolSize;
            
            if (!builder.ContainsKey("Max Pool Size"))
                builder["Max Pool Size"] = perfSettings.MaxPoolSize;
            
            if (!builder.ContainsKey("Connection Lifetime"))
                builder["Connection Lifetime"] = perfSettings.ConnectionLifetimeSeconds;
            
            if (!builder.ContainsKey("Pooling"))
                builder["Pooling"] = perfSettings.EnableConnectionPooling;
            
            if (!builder.ContainsKey("MultipleActiveResultSets"))
                builder["MultipleActiveResultSets"] = perfSettings.EnableMultipleActiveResultSets;

            optionsBuilder.UseSqlServer(builder.ConnectionString);
        }

        // Configure EF Core performance options
        if (perfSettings.EnableQuerySplitting)
        {
            // TODO: UseQuerySplittingBehavior is not available in this EF Core version
            // Query splitting should be configured per query or in model configuration
            // optionsBuilder.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        }

        if (!perfSettings.EnableChangeTracking)
        {
            optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }

        if (perfSettings.EnableServiceProviderCaching)
        {
            optionsBuilder.EnableServiceProviderCaching();
        }

        if (perfSettings.EnableSensitiveDataLogging && logger != null)
        {
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.LogTo(
                message => logger.LogDebug(message),
                new[] { DbLoggerCategory.Database.Command.Name },
                LogLevel.Debug);
        }

        // Configure thread safety
        if (perfSettings.EnableThreadSafetyChecks)
        {
            optionsBuilder.EnableThreadSafetyChecks();
        }

        logger?.LogInformation(
            "Database performance configured: Pool={MinPool}-{MaxPool}, Retry={MaxRetry}, Timeout={Timeout}s",
            perfSettings.MinPoolSize,
            perfSettings.MaxPoolSize,
            perfSettings.MaxRetryCount,
            perfSettings.CommandTimeoutSeconds);
    }

    /// <summary>
    /// Configure command batching for bulk operations
    /// </summary>
    public static void ConfigureCommandBatching(
        this DbContextOptionsBuilder optionsBuilder,
        IConfiguration configuration)
    {
        var batchSettings = configuration.GetSection("Database:Batching").Get<CommandBatchingSettings>() 
            ?? new CommandBatchingSettings();

        optionsBuilder.UseSqlServer(options =>
        {
            // Configure maximum batch size
            if (batchSettings.EnableBatching)
            {
                options.MinBatchSize(batchSettings.MinBatchSize);
                options.MaxBatchSize(batchSettings.MaxBatchSize);
            }
        });
    }

    /// <summary>
    /// Add database performance monitoring
    /// </summary>
    public static IServiceCollection AddDatabasePerformanceMonitoring(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register performance interceptor
        services.AddSingleton<DatabasePerformanceInterceptor>();
        
        // Register connection pool monitor
        services.AddSingleton<IConnectionPoolMonitor, ConnectionPoolMonitor>();
        
        // Register batch processor
        services.AddScoped<IBatchProcessor, BatchProcessor>();
        
        // Configure hosted service for monitoring
        services.AddHostedService<DatabasePerformanceMonitorService>();
        
        return services;
    }
}

/// <summary>
/// Database performance settings
/// </summary>
public class DatabasePerformanceSettings
{
    // Connection pooling
    public bool EnableConnectionPooling { get; set; } = true;
    public int MinPoolSize { get; set; } = 10;
    public int MaxPoolSize { get; set; } = 100;
    public int ConnectionLifetimeSeconds { get; set; } = 300;
    
    // Command execution
    public int CommandTimeoutSeconds { get; set; } = 30;
    public bool EnableMultipleActiveResultSets { get; set; } = true;
    
    // Retry logic
    public int MaxRetryCount { get; set; } = 5;
    public int MaxRetryDelaySeconds { get; set; } = 30;
    public List<int> TransientErrorNumbers { get; set; } = new();
    
    // Query optimization
    public bool EnableQuerySplitting { get; set; } = true;
    public bool EnableChangeTracking { get; set; } = false;
    public bool UseRowNumberForPaging { get; set; } = true;
    
    // Caching
    public bool EnableServiceProviderCaching { get; set; } = true;
    public bool EnableCompiledModels { get; set; } = false;
    
    // Monitoring
    public bool EnableSensitiveDataLogging { get; set; } = false;
    public bool EnableThreadSafetyChecks { get; set; } = true;
}

/// <summary>
/// Command batching settings
/// </summary>
public class CommandBatchingSettings
{
    public bool EnableBatching { get; set; } = true;
    public int MinBatchSize { get; set; } = 2;
    public int MaxBatchSize { get; set; } = 100;
    public int BatchTimeoutMs { get; set; } = 1000;
}