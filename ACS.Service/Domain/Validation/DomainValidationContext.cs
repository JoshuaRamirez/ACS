using ACS.Service.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace ACS.Service.Domain.Validation;

/// <summary>
/// Implementation of domain validation context
/// </summary>
public class DomainValidationContext : IDomainValidationContext
{
    public ApplicationDbContext DbContext { get; }
    public ILogger Logger { get; }
    public IUserContext? UserContext { get; }
    public ValidationConfiguration Configuration { get; }
    public IValidationCache ValidationCache { get; }
    public IServiceProvider ServiceProvider { get; }
    public ValidationOperationContext OperationContext { get; }

    public DomainValidationContext(
        ApplicationDbContext dbContext,
        ILogger<DomainValidationContext> logger,
        ValidationConfiguration configuration,
        IValidationCache validationCache,
        IServiceProvider serviceProvider,
        IUserContext? userContext = null)
    {
        DbContext = dbContext;
        Logger = logger;
        UserContext = userContext;
        Configuration = configuration;
        ValidationCache = validationCache;
        ServiceProvider = serviceProvider;
        OperationContext = new ValidationOperationContext();
    }
}

/// <summary>
/// Implementation of user context for validation
/// </summary>
public class UserContext : IUserContext
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public Dictionary<string, object> Claims { get; set; } = new();
}

/// <summary>
/// Memory-based implementation of validation cache
/// </summary>
public class MemoryValidationCache : IValidationCache
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryValidationCache> _logger;
    private readonly ValidationCacheStatistics _statistics;
    private readonly object _statsLock = new();

    public MemoryValidationCache(IMemoryCache cache, ILogger<MemoryValidationCache> logger)
    {
        _cache = cache;
        _logger = logger;
        _statistics = new ValidationCacheStatistics { LastReset = DateTime.UtcNow };
    }

    public Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            if (_cache.TryGetValue(key, out var value))
            {
                lock (_statsLock)
                {
                    _statistics.HitCount++;
                }
                return Task.FromResult(value as T);
            }
            
            lock (_statsLock)
            {
                _statistics.MissCount++;
            }
            return Task.FromResult<T?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving validation cache entry for key: {Key}", key);
            return Task.FromResult<T?>(null);
        }
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        try
        {
            var options = new MemoryCacheEntryOptions();
            
            if (expiration.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = expiration.Value;
            }
            else
            {
                options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
            }

            // Add size limit
            options.Size = EstimateSize(value);
            
            _cache.Set(key, value, options);

            lock (_statsLock)
            {
                _statistics.TotalEntries++;
                
                var typeName = typeof(T).Name;
                if (_statistics.EntriesByType.ContainsKey(typeName))
                    _statistics.EntriesByType[typeName]++;
                else
                    _statistics.EntriesByType[typeName] = 1;
            }
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting validation cache entry for key: {Key}", key);
            return Task.CompletedTask;
        }
    }

    public Task RemoveAsync(string key)
    {
        try
        {
            _cache.Remove(key);
            _logger.LogDebug("Removed validation cache entry: {Key}", key);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing validation cache entry for key: {Key}", key);
            return Task.CompletedTask;
        }
    }

    public Task RemovePatternAsync(string pattern)
    {
        // Note: IMemoryCache doesn't support pattern-based removal natively
        // In a production system, you might use a different caching solution
        // or maintain a list of keys to support this functionality
        
        _logger.LogWarning("Pattern-based cache removal not fully implemented for MemoryCache. Pattern: {Pattern}", pattern);
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        try
        {
            if (_cache is MemoryCache memoryCache)
            {
                // This is a hack since MemoryCache doesn't have a public Clear method
                // In production, consider using a cache wrapper that tracks keys
                var field = typeof(MemoryCache).GetField("_coherentState", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (field?.GetValue(memoryCache) is object coherentState)
                {
                    var entriesCollection = coherentState.GetType()
                        .GetProperty("EntriesCollection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (entriesCollection?.GetValue(coherentState) is System.Collections.IDictionary entries)
                    {
                        entries.Clear();
                    }
                }
            }

            lock (_statsLock)
            {
                _statistics.TotalEntries = 0;
                _statistics.EntriesByType.Clear();
                _statistics.LastReset = DateTime.UtcNow;
            }

            _logger.LogInformation("Validation cache cleared");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing validation cache");
            return Task.CompletedTask;
        }
    }

    public Task<ValidationCacheStatistics> GetStatisticsAsync()
    {
        lock (_statsLock)
        {
            return Task.FromResult(new ValidationCacheStatistics
            {
                TotalEntries = _statistics.TotalEntries,
                HitCount = _statistics.HitCount,
                MissCount = _statistics.MissCount,
                TotalMemoryUsage = _statistics.TotalMemoryUsage,
                EntriesByType = new Dictionary<string, int>(_statistics.EntriesByType),
                LastReset = _statistics.LastReset
            });
        }
    }

    private long EstimateSize<T>(T value) where T : class
    {
        // Rough estimate of object size for cache sizing
        // In production, you might use more sophisticated size estimation
        
        if (value is string str)
            return str.Length * sizeof(char);
            
        if (value is byte[] bytes)
            return bytes.Length;
            
        // Default estimate for complex objects
        return 1024; // 1KB default
    }
}

/// <summary>
/// Validation context factory for dependency injection
/// </summary>
public interface IValidationContextFactory
{
    IDomainValidationContext CreateContext(IUserContext? userContext = null);
}

public class ValidationContextFactory : IValidationContextFactory
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DomainValidationContext> _logger;
    private readonly ValidationConfiguration _configuration;
    private readonly IValidationCache _validationCache;
    private readonly IServiceProvider _serviceProvider;

    public ValidationContextFactory(
        ApplicationDbContext dbContext,
        ILogger<DomainValidationContext> logger,
        ValidationConfiguration configuration,
        IValidationCache validationCache,
        IServiceProvider serviceProvider)
    {
        _dbContext = dbContext;
        _logger = logger;
        _configuration = configuration;
        _validationCache = validationCache;
        _serviceProvider = serviceProvider;
    }

    public IDomainValidationContext CreateContext(IUserContext? userContext = null)
    {
        return new DomainValidationContext(
            _dbContext,
            _logger,
            _configuration,
            _validationCache,
            _serviceProvider,
            userContext);
    }
}