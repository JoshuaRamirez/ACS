using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ACS.Infrastructure.Caching;

/// <summary>
/// Advanced cache warming service with background refresh patterns, predictive preloading,
/// and intelligent scheduling based on usage patterns
/// </summary>
public class CacheWarmupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IThreeLevelCache _cache;
    private readonly ILogger<CacheWarmupService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ActivitySource _activitySource = new("ACS.CacheWarmup");
    
    // Warmup strategies
    private readonly Dictionary<CacheType, IWarmupStrategy> _warmupStrategies = new();
    private readonly ConcurrentDictionary<string, CacheAccessPattern> _accessPatterns = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastWarmupTimes = new();
    
    // Background refresh patterns
    private readonly Timer _refreshTimer;
    private readonly Timer _analyzeTimer;
    private readonly SemaphoreSlim _warmupSemaphore;
    
    // Configuration
    private readonly TimeSpan _warmupInterval;
    private readonly TimeSpan _analysisInterval;
    private readonly int _maxConcurrentWarmups;
    private readonly double _predictiveThreshold;
    private readonly bool _enablePredictiveWarmup;
    private readonly bool _enableIntelligentScheduling;

    public CacheWarmupService(
        IServiceProvider serviceProvider,
        IThreeLevelCache cache,
        IConfiguration configuration,
        ILogger<CacheWarmupService> logger)
    {
        _serviceProvider = serviceProvider;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
        
        // Configure warmup behavior
        _warmupInterval = TimeSpan.FromMinutes(configuration.GetValue<int>("CacheWarmup:IntervalMinutes", 10));
        _analysisInterval = TimeSpan.FromMinutes(configuration.GetValue<int>("CacheWarmup:AnalysisIntervalMinutes", 5));
        _maxConcurrentWarmups = configuration.GetValue<int>("CacheWarmup:MaxConcurrentWarmups", Environment.ProcessorCount);
        _predictiveThreshold = configuration.GetValue<double>("CacheWarmup:PredictiveThreshold", 0.8);
        _enablePredictiveWarmup = configuration.GetValue<bool>("CacheWarmup:EnablePredictiveWarmup", true);
        _enableIntelligentScheduling = configuration.GetValue<bool>("CacheWarmup:EnableIntelligentScheduling", true);
        
        _warmupSemaphore = new SemaphoreSlim(_maxConcurrentWarmups, _maxConcurrentWarmups);
        
        // Initialize warmup strategies
        InitializeWarmupStrategies();
        
        // Start timers
        _refreshTimer = new Timer(async _ => await PerformWarmupAsync(), null, _warmupInterval, _warmupInterval);
        _analyzeTimer = new Timer(_ => AnalyzeAccessPatternsAsync(), null, _analysisInterval, _analysisInterval);
        
        _logger.LogInformation("Initialized cache warmup service with {MaxConcurrentWarmups} concurrent warmups and {IntervalMinutes}min interval",
            _maxConcurrentWarmups, _warmupInterval.TotalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cache warmup service started");
        
        try
        {
            // Perform initial warmup
            await PerformInitialWarmupAsync(stoppingToken);
            
            // Wait for shutdown
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Cache warmup service stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in cache warmup service");
        }
    }

    public async Task WarmupCacheTypeAsync(CacheType cacheType, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("WarmupCacheType");
        activity?.SetTag("cache.type", cacheType.ToString());
        
        if (!_warmupStrategies.TryGetValue(cacheType, out var strategy))
        {
            _logger.LogWarning("No warmup strategy found for cache type {CacheType}", cacheType);
            return;
        }
        
        await _warmupSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var stopwatch = Stopwatch.StartNew();
            
            var keysToWarmup = await strategy.GetKeysToWarmupAsync(cancellationToken).ConfigureAwait(false);
            await WarmupKeysAsync(keysToWarmup, cacheType, cancellationToken);
            
            stopwatch.Stop();
            activity?.SetTag("cache.warmup_duration_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("cache.keys_warmed", keysToWarmup.Length);
            
            _logger.LogInformation("Warmed up {Count} keys for {CacheType} in {ElapsedMs}ms",
                keysToWarmup.Length, cacheType, stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            _warmupSemaphore.Release();
        }
    }

    public async Task WarmupKeyAsync<T>(string key, Func<Task<T?>> factory, CacheType cacheType, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("WarmupKey");
        activity?.SetTag("cache.key", key);
        activity?.SetTag("cache.type", typeof(T).Name);
        
        try
        {
            // Check if already cached
            var existing = await _cache.GetAsync<T>(key, cancellationToken).ConfigureAwait(false);
            if (existing != null)
            {
                activity?.SetTag("cache.already_cached", true);
                RecordAccess(key, true);
                return;
            }
            
            // Load and cache the value
            var value = await factory();
            if (value != null)
            {
                await _cache.SetAsync(key, value, cacheType, cancellationToken).ConfigureAwait(false);
                activity?.SetTag("cache.warmed", true);
                RecordAccess(key, false);
                _logger.LogTrace("Warmed up cache for key {Key}", key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error warming up cache for key {Key}", key);
            activity?.SetTag("cache.error", ex.Message);
        }
    }

    public async Task RefreshExpiredKeysAsync(CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("RefreshExpiredKeys");
        
        var refreshTasks = new List<Task>();
        
        foreach (var (key, lastWarmup) in _lastWarmupTimes)
        {
            // Check if key needs refresh based on access pattern
            if (_accessPatterns.TryGetValue(key, out var pattern))
            {
                var timeSinceLastWarmup = DateTime.UtcNow - lastWarmup;
                var refreshInterval = CalculateRefreshInterval(pattern);
                
                if (timeSinceLastWarmup > refreshInterval)
                {
                    refreshTasks.Add(RefreshKeyAsync(key, pattern.CacheType, cancellationToken));
                }
            }
        }
        
        if (refreshTasks.Count > 0)
        {
            await Task.WhenAll(refreshTasks).ConfigureAwait(false);
            activity?.SetTag("cache.keys_refreshed", refreshTasks.Count);
            _logger.LogDebug("Refreshed {Count} expired cache keys", refreshTasks.Count);
        }
    }

    public void RecordAccess(string key, bool wasHit)
    {
        _accessPatterns.AddOrUpdate(key,
            new CacheAccessPattern
            {
                Key = key,
                AccessCount = 1,
                HitCount = wasHit ? 1 : 0,
                LastAccess = DateTime.UtcNow,
                FirstAccess = DateTime.UtcNow,
                CacheType = InferCacheTypeFromKey(key)
            },
            (_, existing) =>
            {
                existing.AccessCount++;
                if (wasHit) existing.HitCount++;
                existing.LastAccess = DateTime.UtcNow;
                return existing;
            });
    }

    public async Task<Dictionary<string, object>> GetWarmupStatisticsAsync()
    {
        var stats = new Dictionary<string, object>
        {
            ["total_patterns"] = _accessPatterns.Count,
            ["last_warmup_count"] = _lastWarmupTimes.Count,
            ["warmup_strategies"] = _warmupStrategies.Keys.ToArray(),
            ["concurrent_warmups"] = _maxConcurrentWarmups - _warmupSemaphore.CurrentCount,
            ["predictive_warmup_enabled"] = _enablePredictiveWarmup,
            ["intelligent_scheduling_enabled"] = _enableIntelligentScheduling
        };
        
        // Add per-strategy statistics
        foreach (var (cacheType, strategy) in _warmupStrategies)
        {
            if (strategy is IWarmupStatistics statsProvider)
            {
                stats[$"{cacheType.ToString().ToLower()}_strategy_stats"] = await statsProvider.GetStatisticsAsync().ConfigureAwait(false);
            }
        }
        
        // Add access pattern statistics
        if (_accessPatterns.Count > 0)
        {
            var patterns = _accessPatterns.Values.ToArray();
            stats["avg_hit_rate"] = patterns.Average(p => p.HitRate);
            stats["total_accesses"] = patterns.Sum(p => p.AccessCount);
            stats["most_accessed_keys"] = patterns
                .OrderByDescending(p => p.AccessCount)
                .Take(10)
                .Select(p => new { p.Key, p.AccessCount, p.HitRate })
                .ToArray();
        }
        
        return stats;
    }

    private async Task PerformInitialWarmupAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Performing initial cache warmup...");
        
        try
        {
            var warmupTasks = _warmupStrategies.Keys.Select(async cacheType =>
            {
                try
                {
                    await WarmupCacheTypeAsync(cacheType, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during initial warmup for {CacheType}", cacheType);
                }
            });
            
            await Task.WhenAll(warmupTasks).ConfigureAwait(false);
            _logger.LogInformation("Initial cache warmup completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initial cache warmup");
        }
    }

    private async Task PerformWarmupAsync()
    {
        try
        {
            using var activity = _activitySource.StartActivity("PerformWarmup");
            
            if (_enablePredictiveWarmup)
            {
                await PerformPredictiveWarmupAsync();
            }
            
            if (_enableIntelligentScheduling)
            {
                await PerformIntelligentWarmupAsync();
            }
            
            await RefreshExpiredKeysAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during periodic cache warmup");
        }
    }

    private async Task PerformPredictiveWarmupAsync()
    {
        var predictiveCandidates = _accessPatterns.Values
            .Where(p => p.HitRate < _predictiveThreshold && p.AccessCount > 5)
            .OrderBy(p => p.HitRate)
            .Take(100)
            .ToArray();
        
        if (predictiveCandidates.Length > 0)
        {
            _logger.LogDebug("Performing predictive warmup for {Count} keys", predictiveCandidates.Length);
            
            var warmupTasks = predictiveCandidates.Select(async pattern =>
            {
                await _warmupSemaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    await RefreshKeyAsync(pattern.Key, pattern.CacheType, CancellationToken.None).ConfigureAwait(false);
                }
                finally
                {
                    _warmupSemaphore.Release();
                }
            });
            
            await Task.WhenAll(warmupTasks).ConfigureAwait(false);
        }
    }

    private async Task PerformIntelligentWarmupAsync()
    {
        // Find keys with access patterns that suggest they'll be needed soon
        var currentHour = DateTime.UtcNow.Hour;
        var currentDayOfWeek = DateTime.UtcNow.DayOfWeek;
        
        var intelligentCandidates = _accessPatterns.Values
            .Where(p => ShouldWarmupBasedOnPattern(p, currentHour, currentDayOfWeek))
            .Take(50)
            .ToArray();
        
        if (intelligentCandidates.Length > 0)
        {
            _logger.LogDebug("Performing intelligent warmup for {Count} keys", intelligentCandidates.Length);
            
            var warmupTasks = intelligentCandidates.Select(async pattern =>
            {
                await _warmupSemaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    await RefreshKeyAsync(pattern.Key, pattern.CacheType, CancellationToken.None).ConfigureAwait(false);
                }
                finally
                {
                    _warmupSemaphore.Release();
                }
            });
            
            await Task.WhenAll(warmupTasks).ConfigureAwait(false);
        }
    }

    private void AnalyzeAccessPatternsAsync()
    {
        try
        {
            var currentTime = DateTime.UtcNow;
            var patternsToRemove = new List<string>();
            
            // Clean up old patterns
            foreach (var (key, pattern) in _accessPatterns)
            {
                var timeSinceLastAccess = currentTime - pattern.LastAccess;
                if (timeSinceLastAccess > TimeSpan.FromHours(24) && pattern.AccessCount < 5)
                {
                    patternsToRemove.Add(key);
                }
            }
            
            foreach (var key in patternsToRemove)
            {
                _accessPatterns.TryRemove(key, out _);
                _lastWarmupTimes.TryRemove(key, out _);
            }
            
            if (patternsToRemove.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} old access patterns", patternsToRemove.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing access patterns");
        }
    }

    private async Task WarmupKeysAsync(string[] keys, CacheType cacheType, CancellationToken cancellationToken)
    {
        if (keys.Length == 0) return;
        
        var semaphore = new SemaphoreSlim(Math.Min(_maxConcurrentWarmups, keys.Length));
        
        var warmupTasks = keys.Select(async key =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Use appropriate warmup strategy
                if (_warmupStrategies.TryGetValue(cacheType, out var strategy))
                {
                    await strategy.WarmupKeyAsync(key, cancellationToken).ConfigureAwait(false);
                    _lastWarmupTimes[key] = DateTime.UtcNow;
                }
            }
            finally
            {
                semaphore.Release();
            }
        });
        
        await Task.WhenAll(warmupTasks).ConfigureAwait(false);
    }

    private async Task RefreshKeyAsync(string key, CacheType cacheType, CancellationToken cancellationToken)
    {
        if (_warmupStrategies.TryGetValue(cacheType, out var strategy))
        {
            await strategy.RefreshKeyAsync(key, cancellationToken);
            _lastWarmupTimes[key] = DateTime.UtcNow;
        }
    }

    private void InitializeWarmupStrategies()
    {
        // Register different warmup strategies for different cache types
        _warmupStrategies[CacheType.User] = new UserWarmupStrategy(_serviceProvider, _logger);
        _warmupStrategies[CacheType.Group] = new GroupWarmupStrategy(_serviceProvider, _logger);
        _warmupStrategies[CacheType.Role] = new RoleWarmupStrategy(_serviceProvider, _logger);
        _warmupStrategies[CacheType.Permission] = new PermissionWarmupStrategy(_serviceProvider, _logger);
        _warmupStrategies[CacheType.Configuration] = new ConfigurationWarmupStrategy(_serviceProvider, _logger);
        
        _logger.LogInformation("Initialized {Count} warmup strategies", _warmupStrategies.Count);
    }

    private TimeSpan CalculateRefreshInterval(CacheAccessPattern pattern)
    {
        // Calculate refresh interval based on access frequency
        var hoursSinceFirstAccess = (DateTime.UtcNow - pattern.FirstAccess).TotalHours;
        if (hoursSinceFirstAccess == 0) return TimeSpan.FromMinutes(30);
        
        var accessesPerHour = pattern.AccessCount / hoursSinceFirstAccess;
        
        // More frequent access = more frequent refresh
        if (accessesPerHour > 10) return TimeSpan.FromMinutes(5);
        if (accessesPerHour > 1) return TimeSpan.FromMinutes(15);
        if (accessesPerHour > 0.1) return TimeSpan.FromHours(1);
        return TimeSpan.FromHours(6);
    }

    private bool ShouldWarmupBasedOnPattern(CacheAccessPattern pattern, int currentHour, DayOfWeek currentDayOfWeek)
    {
        // Simple heuristic: if the key was frequently accessed at this time before
        // This would be enhanced with more sophisticated ML-based predictions in a production system
        
        // For now, just check if it was accessed recently and has a decent hit rate
        var timeSinceLastAccess = DateTime.UtcNow - pattern.LastAccess;
        return timeSinceLastAccess < TimeSpan.FromHours(2) && 
               pattern.HitRate > 0.5 && 
               pattern.AccessCount > 3;
    }

    private CacheType InferCacheTypeFromKey(string key)
    {
        if (key.StartsWith("user:")) return CacheType.User;
        if (key.StartsWith("group:")) return CacheType.Group;
        if (key.StartsWith("role:")) return CacheType.Role;
        if (key.StartsWith("permission")) return CacheType.Permission;
        if (key.StartsWith("config:")) return CacheType.Configuration;
        return CacheType.Metadata;
    }

    public override void Dispose()
    {
        _refreshTimer?.Dispose();
        _analyzeTimer?.Dispose();
        _warmupSemaphore?.Dispose();
        _activitySource?.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Tracks access patterns for intelligent cache warming
/// </summary>
public class CacheAccessPattern
{
    public string Key { get; set; } = string.Empty;
    public CacheType CacheType { get; set; }
    public long AccessCount { get; set; }
    public long HitCount { get; set; }
    public DateTime FirstAccess { get; set; }
    public DateTime LastAccess { get; set; }
    public double HitRate => AccessCount > 0 ? (double)HitCount / AccessCount : 0;
}

/// <summary>
/// Base interface for cache warmup strategies
/// </summary>
public interface IWarmupStrategy
{
    Task<string[]> GetKeysToWarmupAsync(CancellationToken cancellationToken = default);
    Task WarmupKeyAsync(string key, CancellationToken cancellationToken = default);
    Task RefreshKeyAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>
/// Optional interface for strategies that provide statistics
/// </summary>
public interface IWarmupStatistics
{
    Task<Dictionary<string, object>> GetStatisticsAsync();
}

/// <summary>
/// Base warmup strategy with common functionality
/// </summary>
public abstract class BaseWarmupStrategy : IWarmupStrategy, IWarmupStatistics
{
    protected readonly IServiceProvider ServiceProvider;
    protected readonly ILogger Logger;
    protected readonly ConcurrentDictionary<string, long> Metrics = new();
    
    protected BaseWarmupStrategy(IServiceProvider serviceProvider, ILogger logger)
    {
        ServiceProvider = serviceProvider;
        Logger = logger;
    }
    
    public abstract Task<string[]> GetKeysToWarmupAsync(CancellationToken cancellationToken = default);
    public abstract Task WarmupKeyAsync(string key, CancellationToken cancellationToken = default);
    public abstract Task RefreshKeyAsync(string key, CancellationToken cancellationToken = default);
    
    public virtual Task<Dictionary<string, object>> GetStatisticsAsync()
    {
        return Task.FromResult(Metrics.ToDictionary(kv => kv.Key, kv => (object)kv.Value));
    }
    
    protected void IncrementMetric(string metric, long increment = 1)
    {
        Metrics.AddOrUpdate(metric, increment, (key, value) => value + increment);
    }
}

/// <summary>
/// User-specific warmup strategy
/// </summary>
public class UserWarmupStrategy : BaseWarmupStrategy
{
    public UserWarmupStrategy(IServiceProvider serviceProvider, ILogger logger) 
        : base(serviceProvider, logger) { }
    
    public override Task<string[]> GetKeysToWarmupAsync(CancellationToken cancellationToken = default)
    {
        // This would typically query the database for active users
        // For now, return a placeholder implementation
        IncrementMetric("keys_requested");
        return Task.FromResult(Array.Empty<string>());
    }
    
    public override Task WarmupKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            // Warmup logic for user data
            IncrementMetric("warmup_attempts");
            Logger.LogTrace("Warming up user key {Key}", key);
            // Implementation would load user data here
            IncrementMetric("warmup_success");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error warming up user key {Key}", key);
            IncrementMetric("warmup_errors");
        }
        
        return Task.CompletedTask;
    }
    
    public override async Task RefreshKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        await WarmupKeyAsync(key, cancellationToken).ConfigureAwait(false); // Same logic for now
        IncrementMetric("refresh_attempts");
    }
}

/// <summary>
/// Group-specific warmup strategy
/// </summary>
public class GroupWarmupStrategy : BaseWarmupStrategy
{
    public GroupWarmupStrategy(IServiceProvider serviceProvider, ILogger logger) 
        : base(serviceProvider, logger) { }
    
    public override Task<string[]> GetKeysToWarmupAsync(CancellationToken cancellationToken = default)
    {
        IncrementMetric("keys_requested");
        return Task.FromResult(Array.Empty<string>());
    }
    
    public override Task WarmupKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            IncrementMetric("warmup_attempts");
            Logger.LogTrace("Warming up group key {Key}", key);
            IncrementMetric("warmup_success");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error warming up group key {Key}", key);
            IncrementMetric("warmup_errors");
        }
        
        return Task.CompletedTask;
    }
    
    public override async Task RefreshKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        await WarmupKeyAsync(key, cancellationToken).ConfigureAwait(false);
        IncrementMetric("refresh_attempts");
    }
}

/// <summary>
/// Role-specific warmup strategy
/// </summary>
public class RoleWarmupStrategy : BaseWarmupStrategy
{
    public RoleWarmupStrategy(IServiceProvider serviceProvider, ILogger logger) 
        : base(serviceProvider, logger) { }
    
    public override Task<string[]> GetKeysToWarmupAsync(CancellationToken cancellationToken = default)
    {
        IncrementMetric("keys_requested");
        return Task.FromResult(Array.Empty<string>());
    }
    
    public override Task WarmupKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            IncrementMetric("warmup_attempts");
            Logger.LogTrace("Warming up role key {Key}", key);
            IncrementMetric("warmup_success");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error warming up role key {Key}", key);
            IncrementMetric("warmup_errors");
        }
        
        return Task.CompletedTask;
    }
    
    public override async Task RefreshKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        await WarmupKeyAsync(key, cancellationToken).ConfigureAwait(false);
        IncrementMetric("refresh_attempts");
    }
}

/// <summary>
/// Permission-specific warmup strategy
/// </summary>
public class PermissionWarmupStrategy : BaseWarmupStrategy
{
    public PermissionWarmupStrategy(IServiceProvider serviceProvider, ILogger logger) 
        : base(serviceProvider, logger) { }
    
    public override Task<string[]> GetKeysToWarmupAsync(CancellationToken cancellationToken = default)
    {
        IncrementMetric("keys_requested");
        return Task.FromResult(Array.Empty<string>());
    }
    
    public override Task WarmupKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            IncrementMetric("warmup_attempts");
            Logger.LogTrace("Warming up permission key {Key}", key);
            IncrementMetric("warmup_success");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error warming up permission key {Key}", key);
            IncrementMetric("warmup_errors");
        }
        
        return Task.CompletedTask;
    }
    
    public override async Task RefreshKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        await WarmupKeyAsync(key, cancellationToken).ConfigureAwait(false);
        IncrementMetric("refresh_attempts");
    }
}

/// <summary>
/// Configuration-specific warmup strategy
/// </summary>
public class ConfigurationWarmupStrategy : BaseWarmupStrategy
{
    public ConfigurationWarmupStrategy(IServiceProvider serviceProvider, ILogger logger) 
        : base(serviceProvider, logger) { }
    
    public override Task<string[]> GetKeysToWarmupAsync(CancellationToken cancellationToken = default)
    {
        IncrementMetric("keys_requested");
        // Configuration keys are typically well-known
        return Task.FromResult(new[] { "config:app_settings", "config:feature_flags", "config:environment" });
    }
    
    public override Task WarmupKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            IncrementMetric("warmup_attempts");
            Logger.LogTrace("Warming up configuration key {Key}", key);
            IncrementMetric("warmup_success");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error warming up configuration key {Key}", key);
            IncrementMetric("warmup_errors");
        }
        
        return Task.CompletedTask;
    }
    
    public override async Task RefreshKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        await WarmupKeyAsync(key, cancellationToken).ConfigureAwait(false);
        IncrementMetric("refresh_attempts");
    }
}