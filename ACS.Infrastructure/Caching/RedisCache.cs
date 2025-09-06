using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace ACS.Infrastructure.Caching;

/// <summary>
/// Production-ready Redis distributed cache implementation with connection pooling,
/// resilience patterns, and performance optimizations
/// </summary>
public class RedisCache : IDistributedCache, IDisposable, IAsyncDisposable
{
    private readonly ILogger<RedisCache> _logger;
    private readonly IConfiguration _configuration;
    private readonly ActivitySource _activitySource = new("ACS.RedisCache");
    
    // Connection management
    private readonly Lazy<IConnectionMultiplexer> _connectionLazy;
    private readonly string _instanceName;
    private readonly string _keyPrefix;
    
    // Resilience patterns
    private readonly AsyncCircuitBreakerPolicy _circuitBreaker;
    private readonly IAsyncPolicy _retryPolicy;
    private readonly IAsyncPolicy _combinedPolicy;
    
    // Performance tracking
    private readonly ConcurrentDictionary<string, long> _operationMetrics = new();
    private readonly Timer _metricsTimer;
    
    // Configuration
    private readonly TimeSpan _defaultExpiration;
    private readonly bool _enableCompression;
    private readonly int _compressionThreshold;
    private readonly JsonSerializerOptions _jsonOptions;

    public RedisCache(
        IConfiguration configuration,
        ILogger<RedisCache> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        var connectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Redis connection string not configured");
            
        _instanceName = configuration.GetValue<string>("Redis:InstanceName") ?? "ACS";
        _keyPrefix = configuration.GetValue<string>("Redis:KeyPrefix") ?? "acs:";
        _defaultExpiration = TimeSpan.FromMinutes(configuration.GetValue<int>("Redis:DefaultExpirationMinutes", 30));
        _enableCompression = configuration.GetValue<bool>("Redis:EnableCompression", true);
        _compressionThreshold = configuration.GetValue<int>("Redis:CompressionThreshold", 1024);

        // Configure JSON serialization options for performance
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };

        // Initialize connection with lazy loading for better startup performance
        _connectionLazy = new Lazy<IConnectionMultiplexer>(() => CreateConnection(connectionString));

        // Configure circuit breaker (using Polly v8 API)
        _circuitBreaker = Policy
            .Handle<RedisConnectionException>()
            .Or<RedisTimeoutException>()
            .Or<RedisServerException>()
            .AdvancedCircuitBreakerAsync(
                failureThreshold: configuration.GetValue<double>("Redis:CircuitBreaker:FailureThreshold", 0.5),
                samplingDuration: TimeSpan.FromSeconds(configuration.GetValue<int>("Redis:CircuitBreaker:SamplingDurationSeconds", 10)),
                minimumThroughput: configuration.GetValue<int>("Redis:CircuitBreaker:MinimumThroughput", 3),
                durationOfBreak: TimeSpan.FromSeconds(configuration.GetValue<int>("Redis:CircuitBreaker:BreakDurationSeconds", 30)),
                onBreak: (exception, duration) =>
                {
                    _logger.LogWarning("Redis circuit breaker opened for {Duration}s due to: {Exception}", 
                        duration.TotalSeconds, exception.Message);
                    IncrementMetric("circuit_breaker_opened");
                },
                onReset: () =>
                {
                    _logger.LogInformation("Redis circuit breaker reset - connection restored");
                    IncrementMetric("circuit_breaker_reset");
                });

        // Configure retry policy
        _retryPolicy = Policy
            .Handle<RedisTimeoutException>()
            .Or<RedisServerException>(ex => ex.Message.Contains("LOADING"))
            .WaitAndRetryAsync(
                retryCount: configuration.GetValue<int>("Redis:Retry:MaxAttempts", 3),
                sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(
                    Math.Pow(2, retryAttempt) * configuration.GetValue<int>("Redis:Retry:BaseDelayMs", 100)),
                onRetry: (exception, delay, retryCount, context) =>
                {
                    _logger.LogDebug("Redis operation retry {RetryCount} in {Delay}ms: {Exception}",
                        retryCount, delay.TotalMilliseconds, exception.Message);
                    IncrementMetric($"retry_attempt_{retryCount}");
                });

        // Combine policies
        _combinedPolicy = Policy.WrapAsync(_retryPolicy, _circuitBreaker);

        // Start metrics collection timer
        _metricsTimer = new Timer(LogMetrics, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        _logger.LogInformation("Initialized Redis cache with instance name '{InstanceName}' and key prefix '{KeyPrefix}'",
            _instanceName, _keyPrefix);
    }

    private IConnectionMultiplexer CreateConnection(string connectionString)
    {
        var options = ConfigurationOptions.Parse(connectionString);
        
        // Configure connection options for production
        options.AbortOnConnectFail = false;
        options.ConnectRetry = _configuration.GetValue<int>("Redis:Connection:ConnectRetry", 3);
        options.ConnectTimeout = _configuration.GetValue<int>("Redis:Connection:ConnectTimeoutMs", 5000);
        options.SyncTimeout = _configuration.GetValue<int>("Redis:Connection:SyncTimeoutMs", 5000);
        options.AsyncTimeout = _configuration.GetValue<int>("Redis:Connection:AsyncTimeoutMs", 5000);
        options.ResponseTimeout = _configuration.GetValue<int>("Redis:Connection:ResponseTimeoutMs", 5000);
        options.KeepAlive = _configuration.GetValue<int>("Redis:Connection:KeepAliveSeconds", 60);
        
        // Connection pool settings
        options.DefaultDatabase = _configuration.GetValue<int>("Redis:DefaultDatabase", 0);
        
        var connection = ConnectionMultiplexer.Connect(options);
        
        // Configure connection events
        connection.ConnectionFailed += OnConnectionFailed;
        connection.ConnectionRestored += OnConnectionRestored;
        connection.ErrorMessage += OnErrorMessage;
        connection.InternalError += OnInternalError;

        _logger.LogInformation("Successfully connected to Redis server: {EndPoints}",
            string.Join(", ", connection.GetEndPoints().Select(ep => ep.ToString())));
            
        return connection;
    }

    private void OnConnectionFailed(object? sender, ConnectionFailedEventArgs e)
    {
        _logger.LogError("Redis connection failed to {EndPoint}: {Exception}", e.EndPoint, e.Exception?.Message);
        IncrementMetric("connection_failed");
    }

    private void OnConnectionRestored(object? sender, ConnectionFailedEventArgs e)
    {
        _logger.LogInformation("Redis connection restored to {EndPoint}", e.EndPoint);
        IncrementMetric("connection_restored");
    }

    private void OnErrorMessage(object? sender, RedisErrorEventArgs e)
    {
        _logger.LogWarning("Redis error message: {Message}", e.Message);
        IncrementMetric("error_message");
    }

    private void OnInternalError(object? sender, InternalErrorEventArgs e)
    {
        _logger.LogError("Redis internal error: {Exception}", e.Exception);
        IncrementMetric("internal_error");
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        using var activity = _activitySource.StartActivity("GetAsync");
        activity?.SetTag("cache.key", key);
        activity?.SetTag("cache.operation", "get");

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var redisValue = await _combinedPolicy.ExecuteAsync(async () =>
            {
                var database = GetDatabase();
                var redisKey = GetRedisKey(key);
                
                var value = await database.StringGetAsync(redisKey);
                
                if (value.HasValue)
                {
                    activity?.SetTag("cache.hit", true);
                    IncrementMetric("cache_hit");
                    return value;
                }
                
                activity?.SetTag("cache.hit", false);
                IncrementMetric("cache_miss");
                return RedisValue.Null;
            });
            
            if (!redisValue.HasValue)
                return null;
                
            var bytes = (byte[])redisValue!;
            
            // Decompress if needed
            if (_enableCompression && IsCompressed(bytes))
            {
                bytes = await DecompressAsync(bytes);
                activity?.SetTag("cache.decompressed", true);
            }
            
            activity?.SetTag("cache.size_bytes", bytes.Length);
            var result = bytes;
            
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error getting value from Redis cache for key {Key}", key);
            activity?.SetTag("cache.error", ex.Message);
            IncrementMetric("get_error");
            
            // Return null on error to allow fallback behavior
            return null;
        }
        finally
        {
            stopwatch.Stop();
            activity?.SetTag("cache.duration_ms", stopwatch.ElapsedMilliseconds);
            RecordOperationTime("get", stopwatch.ElapsedMilliseconds);
        }
    }

    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        using var activity = _activitySource.StartActivity("SetAsync");
        activity?.SetTag("cache.key", key);
        activity?.SetTag("cache.operation", "set");
        activity?.SetTag("cache.size_bytes", value.Length);

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await _combinedPolicy.ExecuteAsync(async () =>
            {
                var database = GetDatabase();
                var redisKey = GetRedisKey(key);
                
                // Compress if enabled and value is large enough
                var finalValue = value;
                if (_enableCompression && value.Length > _compressionThreshold)
                {
                    finalValue = await CompressAsync(value);
                    activity?.SetTag("cache.compressed", true);
                    activity?.SetTag("cache.original_size_bytes", value.Length);
                    activity?.SetTag("cache.compressed_size_bytes", finalValue.Length);
                    activity?.SetTag("cache.compression_ratio", (double)finalValue.Length / value.Length);
                }

                var expiration = GetExpiration(options);
                
                if (expiration.HasValue)
                {
                    await database.StringSetAsync(redisKey, finalValue, expiration);
                    activity?.SetTag("cache.expiration_seconds", expiration.Value.TotalSeconds);
                }
                else
                {
                    await database.StringSetAsync(redisKey, finalValue);
                }
                
                IncrementMetric("cache_set");
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error setting value in Redis cache for key {Key}", key);
            activity?.SetTag("cache.error", ex.Message);
            IncrementMetric("set_error");
            throw;
        }
        finally
        {
            stopwatch.Stop();
            activity?.SetTag("cache.duration_ms", stopwatch.ElapsedMilliseconds);
            RecordOperationTime("set", stopwatch.ElapsedMilliseconds);
        }
    }

    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        using var activity = _activitySource.StartActivity("RefreshAsync");
        activity?.SetTag("cache.key", key);
        activity?.SetTag("cache.operation", "refresh");

        try
        {
            await _combinedPolicy.ExecuteAsync(async () =>
            {
                var database = GetDatabase();
                var redisKey = GetRedisKey(key);
                
                // Touch the key to refresh its TTL if it exists
                var exists = await database.KeyExistsAsync(redisKey);
                if (exists)
                {
                    await database.KeyExpireAsync(redisKey, _defaultExpiration);
                    IncrementMetric("cache_refresh");
                }
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error refreshing Redis cache key {Key}", key);
            activity?.SetTag("cache.error", ex.Message);
            IncrementMetric("refresh_error");
            throw;
        }
    }

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        using var activity = _activitySource.StartActivity("RemoveAsync");
        activity?.SetTag("cache.key", key);
        activity?.SetTag("cache.operation", "remove");

        try
        {
            await _combinedPolicy.ExecuteAsync(async () =>
            {
                var database = GetDatabase();
                var redisKey = GetRedisKey(key);
                
                var removed = await database.KeyDeleteAsync(redisKey);
                activity?.SetTag("cache.removed", removed);
                
                if (removed)
                {
                    IncrementMetric("cache_remove");
                }
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error removing value from Redis cache for key {Key}", key);
            activity?.SetTag("cache.error", ex.Message);
            IncrementMetric("remove_error");
            throw;
        }
    }

    public byte[]? Get(string key)
    {
        return GetAsync(key).GetAwaiter().GetResult();
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        SetAsync(key, value, options).GetAwaiter().GetResult();
    }

    public void Refresh(string key)
    {
        RefreshAsync(key).GetAwaiter().GetResult();
    }

    public void Remove(string key)
    {
        RemoveAsync(key).GetAwaiter().GetResult();
    }

    // Additional Redis-specific methods
    public async Task RemoveByPatternAsync(string pattern, CancellationToken token = default)
    {
        using var activity = _activitySource.StartActivity("RemoveByPatternAsync");
        activity?.SetTag("cache.pattern", pattern);
        activity?.SetTag("cache.operation", "remove_pattern");

        try
        {
            await _combinedPolicy.ExecuteAsync(async () =>
            {
                var database = GetDatabase();
                var server = GetServer();
                var redisPattern = GetRedisKey(pattern);
                
                var keys = server.Keys(database: database.Database, pattern: redisPattern);
                var keyArray = keys.ToArray();
                
                if (keyArray.Length > 0)
                {
                    await database.KeyDeleteAsync(keyArray);
                    activity?.SetTag("cache.removed_count", keyArray.Length);
                    IncrementMetric("pattern_remove", keyArray.Length);
                }
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error removing keys by pattern {Pattern} from Redis", pattern);
            activity?.SetTag("cache.error", ex.Message);
            IncrementMetric("pattern_remove_error");
            throw;
        }
    }

    public async Task<Dictionary<string, string>> GetHealthInfoAsync()
    {
        var info = new Dictionary<string, string>();
        
        try
        {
            if (_connectionLazy.IsValueCreated && _connection.IsConnected)
            {
                var database = GetDatabase();
                var server = GetServer();
                
                // Get server info
                var serverInfo = await server.InfoAsync();
                var memoryInfo = serverInfo.FirstOrDefault(g => g.Key == "memory");
                var statsInfo = serverInfo.FirstOrDefault(g => g.Key == "stats");
                
                info["status"] = "connected";
                info["endpoints"] = string.Join(", ", _connection.GetEndPoints().Select(ep => ep.ToString()));
                
                if (memoryInfo.Any())
                {
                    var usedMemory = memoryInfo.FirstOrDefault(kv => kv.Key == "used_memory").Value;
                    if (!string.IsNullOrEmpty(usedMemory)) info["used_memory"] = usedMemory;
                }
                
                if (statsInfo.Any())
                {
                    var totalConnections = statsInfo.FirstOrDefault(kv => kv.Key == "total_connections_received").Value;
                    if (!string.IsNullOrEmpty(totalConnections)) info["total_connections"] = totalConnections;
                }
                
                // Test connectivity
                var ping = await database.PingAsync();
                info["ping_ms"] = ping.TotalMilliseconds.ToString("F2");
            }
            else
            {
                info["status"] = "disconnected";
            }
        }
        catch (Exception ex)
        {
            info["status"] = "error";
            info["error"] = ex.Message;
        }
        
        // Add operation metrics
        foreach (var metric in _operationMetrics)
        {
            info[$"metric_{metric.Key}"] = metric.Value.ToString();
        }
        
        return info;
    }

    private IDatabase GetDatabase()
    {
        var connection = _connectionLazy.Value;
        if (!connection.IsConnected)
        {
            throw new InvalidOperationException("Redis connection is not available");
        }
        return connection.GetDatabase();
    }
    
    private IServer GetServer()
    {
        var connection = _connectionLazy.Value;
        var endpoints = connection.GetEndPoints();
        return connection.GetServer(endpoints.First());
    }

    private IConnectionMultiplexer _connection => _connectionLazy.Value;

    private string GetRedisKey(string key) => $"{_keyPrefix}{key}";

    private TimeSpan? GetExpiration(DistributedCacheEntryOptions options)
    {
        if (options.AbsoluteExpirationRelativeToNow.HasValue)
            return options.AbsoluteExpirationRelativeToNow;
            
        if (options.SlidingExpiration.HasValue)
            return options.SlidingExpiration;
            
        return _defaultExpiration;
    }

    private async Task<byte[]> CompressAsync(byte[] data)
    {
        using var output = new MemoryStream();
        await using (var gzip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionLevel.Fastest))
        {
            await gzip.WriteAsync(data);
        }
        
        // Add compression marker
        var compressed = output.ToArray();
        var result = new byte[compressed.Length + 1];
        result[0] = 0x1F; // GZip magic number first byte as marker
        Array.Copy(compressed, 0, result, 1, compressed.Length);
        return result;
    }

    private async Task<byte[]> DecompressAsync(byte[] data)
    {
        // Remove compression marker
        var compressedData = new byte[data.Length - 1];
        Array.Copy(data, 1, compressedData, 0, compressedData.Length);
        
        using var input = new MemoryStream(compressedData);
        using var gzip = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress);
        using var output = new MemoryStream();
        await gzip.CopyToAsync(output);
        return output.ToArray();
    }

    private bool IsCompressed(byte[] data)
    {
        return data.Length > 0 && data[0] == 0x1F;
    }

    private void IncrementMetric(string metric, long increment = 1)
    {
        _operationMetrics.AddOrUpdate(metric, increment, (key, value) => value + increment);
    }

    private void RecordOperationTime(string operation, long milliseconds)
    {
        IncrementMetric($"{operation}_total_time_ms", milliseconds);
        IncrementMetric($"{operation}_count");
    }

    private void LogMetrics(object? state)
    {
        try
        {
            if (_operationMetrics.IsEmpty) return;
            
            var metrics = _operationMetrics.ToDictionary(kv => kv.Key, kv => kv.Value);
            _logger.LogDebug("Redis cache metrics: {@Metrics}", metrics);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error logging Redis cache metrics");
        }
    }

    public void Dispose()
    {
        _metricsTimer?.Dispose();
        _activitySource?.Dispose();
        
        if (_connectionLazy.IsValueCreated)
        {
            _connection?.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_metricsTimer != null)
        {
            await _metricsTimer.DisposeAsync();
        }
        
        _activitySource?.Dispose();
        
        if (_connectionLazy.IsValueCreated)
        {
            await _connection.DisposeAsync();
        }
    }
}