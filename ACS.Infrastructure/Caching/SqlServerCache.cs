using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Text;

namespace ACS.Infrastructure.Caching;

/// <summary>
/// SQL Server distributed cache implementation with performance optimizations,
/// automatic cleanup, and comprehensive monitoring
/// </summary>
public class SqlServerCache : IDistributedCache, IDisposable, IAsyncDisposable
{
    private readonly ILogger<SqlServerCache> _logger;
    private readonly IConfiguration _configuration;
    private readonly ActivitySource _activitySource = new("ACS.SqlServerCache");
    
    // Configuration
    private readonly string _connectionString;
    private readonly string _schemaName;
    private readonly string _tableName;
    private readonly string _keyColumnName;
    private readonly string _valueColumnName;
    private readonly string _expirationColumnName;
    private readonly string _createdColumnName;
    private readonly TimeSpan _defaultExpiration;
    private readonly int _cleanupIntervalMinutes;
    
    // Performance tracking
    private readonly ConcurrentDictionary<string, long> _operationMetrics = new();
    private readonly Timer _cleanupTimer;
    private readonly Timer _metricsTimer;
    
    // Connection pooling will be handled by SqlConnection automatically
    private readonly SemaphoreSlim _cleanupSemaphore = new(1, 1);
    
    // Prepared SQL statements for performance
    private readonly string _getQuery;
    private readonly string _setQuery;
    private readonly string _removeQuery;
    private readonly string _refreshQuery;
    private readonly string _cleanupQuery;
    private readonly string _existsQuery;

    public SqlServerCache(
        IConfiguration configuration,
        ILogger<SqlServerCache> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("SQL Server connection string not configured");
            
        _schemaName = configuration.GetValue<string>("SqlServerCache:SchemaName") ?? "dbo";
        _tableName = configuration.GetValue<string>("SqlServerCache:TableName") ?? "DataCache";
        _keyColumnName = configuration.GetValue<string>("SqlServerCache:KeyColumnName") ?? "Id";
        _valueColumnName = configuration.GetValue<string>("SqlServerCache:ValueColumnName") ?? "Value";
        _expirationColumnName = configuration.GetValue<string>("SqlServerCache:ExpirationColumnName") ?? "ExpiresAtTime";
        _createdColumnName = configuration.GetValue<string>("SqlServerCache:CreatedColumnName") ?? "CreatedTime";
        
        _defaultExpiration = TimeSpan.FromMinutes(configuration.GetValue<int>("SqlServerCache:DefaultExpirationMinutes", 30));
        _cleanupIntervalMinutes = configuration.GetValue<int>("SqlServerCache:CleanupIntervalMinutes", 5);
        
        var fullTableName = $"[{_schemaName}].[{_tableName}]";
        
        // Prepare optimized SQL statements
        _getQuery = $@"
            SELECT [{_valueColumnName}] 
            FROM {fullTableName} 
            WHERE [{_keyColumnName}] = @Key 
                AND ([{_expirationColumnName}] IS NULL OR [{_expirationColumnName}] > GETUTCDATE())";
                
        _setQuery = $@"
            MERGE {fullTableName} AS target
            USING (SELECT @Key AS [{_keyColumnName}]) AS source
            ON target.[{_keyColumnName}] = source.[{_keyColumnName}]
            WHEN MATCHED THEN
                UPDATE SET 
                    [{_valueColumnName}] = @Value,
                    [{_expirationColumnName}] = @ExpiresAtTime,
                    [{_createdColumnName}] = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT ([{_keyColumnName}], [{_valueColumnName}], [{_expirationColumnName}], [{_createdColumnName}])
                VALUES (@Key, @Value, @ExpiresAtTime, GETUTCDATE());";
                
        _removeQuery = $@"
            DELETE FROM {fullTableName} 
            WHERE [{_keyColumnName}] = @Key";
            
        _refreshQuery = $@"
            UPDATE {fullTableName} 
            SET [{_expirationColumnName}] = @ExpiresAtTime 
            WHERE [{_keyColumnName}] = @Key 
                AND ([{_expirationColumnName}] IS NULL OR [{_expirationColumnName}] > GETUTCDATE())";
                
        _cleanupQuery = $@"
            DELETE FROM {fullTableName} 
            WHERE [{_expirationColumnName}] IS NOT NULL 
                AND [{_expirationColumnName}] <= GETUTCDATE()";
                
        _existsQuery = $@"
            SELECT 1 FROM {fullTableName} 
            WHERE [{_keyColumnName}] = @Key 
                AND ([{_expirationColumnName}] IS NULL OR [{_expirationColumnName}] > GETUTCDATE())";

        // Initialize table if needed
        InitializeTableAsync().ConfigureAwait(false);

        // Start cleanup timer
        _cleanupTimer = new Timer(
            async _ => await PerformCleanupAsync(),
            null,
            TimeSpan.FromMinutes(_cleanupIntervalMinutes),
            TimeSpan.FromMinutes(_cleanupIntervalMinutes));

        // Start metrics timer
        _metricsTimer = new Timer(LogMetrics, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        _logger.LogInformation("Initialized SQL Server cache with table {SchemaName}.{TableName}",
            _schemaName, _tableName);
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
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await using var command = new SqlCommand(_getQuery, connection);
            command.Parameters.Add("@Key", SqlDbType.NVarChar, 900).Value = key;
            
            var result = await command.ExecuteScalarAsync(token);
            
            if (result is byte[] value)
            {
                activity?.SetTag("cache.hit", true);
                activity?.SetTag("cache.size_bytes", value.Length);
                IncrementMetric("cache_hit");
                return value;
            }

            activity?.SetTag("cache.hit", false);
            IncrementMetric("cache_miss");
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error getting value from SQL Server cache for key {Key}", key);
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
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await using var command = new SqlCommand(_setQuery, connection);
            command.Parameters.Add("@Key", SqlDbType.NVarChar, 900).Value = key;
            command.Parameters.Add("@Value", SqlDbType.VarBinary, -1).Value = value;
            
            var expiration = GetExpiration(options);
            if (expiration.HasValue)
            {
                command.Parameters.Add("@ExpiresAtTime", SqlDbType.DateTime2).Value = DateTime.UtcNow.Add(expiration.Value);
                activity?.SetTag("cache.expiration_seconds", expiration.Value.TotalSeconds);
            }
            else
            {
                command.Parameters.Add("@ExpiresAtTime", SqlDbType.DateTime2).Value = DBNull.Value;
            }

            var rowsAffected = await command.ExecuteNonQueryAsync(token);
            
            if (rowsAffected > 0)
            {
                IncrementMetric("cache_set");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error setting value in SQL Server cache for key {Key}", key);
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
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await using var command = new SqlCommand(_refreshQuery, connection);
            command.Parameters.Add("@Key", SqlDbType.NVarChar, 900).Value = key;
            command.Parameters.Add("@ExpiresAtTime", SqlDbType.DateTime2).Value = DateTime.UtcNow.Add(_defaultExpiration);

            var rowsAffected = await command.ExecuteNonQueryAsync(token);
            
            if (rowsAffected > 0)
            {
                IncrementMetric("cache_refresh");
                activity?.SetTag("cache.refreshed", true);
            }
            else
            {
                activity?.SetTag("cache.refreshed", false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error refreshing SQL Server cache key {Key}", key);
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
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            await using var command = new SqlCommand(_removeQuery, connection);
            command.Parameters.Add("@Key", SqlDbType.NVarChar, 900).Value = key;

            var rowsAffected = await command.ExecuteNonQueryAsync(token);
            
            activity?.SetTag("cache.removed", rowsAffected > 0);
            
            if (rowsAffected > 0)
            {
                IncrementMetric("cache_remove");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error removing value from SQL Server cache for key {Key}", key);
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

    // Additional SQL Server-specific methods
    public async Task RemoveByPatternAsync(string pattern, CancellationToken token = default)
    {
        using var activity = _activitySource.StartActivity("RemoveByPatternAsync");
        activity?.SetTag("cache.pattern", pattern);
        activity?.SetTag("cache.operation", "remove_pattern");

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(token);

            var sqlPattern = pattern.Replace("*", "%").Replace("?", "_");
            var query = $@"
                DELETE FROM [{_schemaName}].[{_tableName}] 
                WHERE [{_keyColumnName}] LIKE @Pattern";

            await using var command = new SqlCommand(query, connection);
            command.Parameters.Add("@Pattern", SqlDbType.NVarChar, 900).Value = sqlPattern;

            var rowsAffected = await command.ExecuteNonQueryAsync(token);
            
            activity?.SetTag("cache.removed_count", rowsAffected);
            
            if (rowsAffected > 0)
            {
                IncrementMetric("pattern_remove", rowsAffected);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error removing keys by pattern {Pattern} from SQL Server cache", pattern);
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
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            
            info["status"] = "connected";
            info["server"] = connection.DataSource;
            info["database"] = connection.Database;
            
            // Get table statistics
            var statsQuery = $@"
                SELECT 
                    COUNT(*) as TotalEntries,
                    COUNT(CASE WHEN [{_expirationColumnName}] IS NOT NULL AND [{_expirationColumnName}] <= GETUTCDATE() THEN 1 END) as ExpiredEntries,
                    AVG(DATALENGTH([{_valueColumnName}])) as AvgValueSize
                FROM [{_schemaName}].[{_tableName}]";
                
            await using var command = new SqlCommand(statsQuery, connection);
            await using var reader = await command.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                info["total_entries"] = reader.GetInt32("TotalEntries").ToString();
                info["expired_entries"] = reader.GetInt32("ExpiredEntries").ToString();
                if (!reader.IsDBNull("AvgValueSize"))
                {
                    info["avg_value_size_bytes"] = reader.GetDouble("AvgValueSize").ToString("F0");
                }
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

    private async Task InitializeTableAsync()
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Check if table exists
            var checkTableQuery = $@"
                SELECT COUNT(*) 
                FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_SCHEMA = '{_schemaName}' 
                AND TABLE_NAME = '{_tableName}'";

            await using var checkCommand = new SqlCommand(checkTableQuery, connection);
            var tableExists = (int)await checkCommand.ExecuteScalarAsync() > 0;

            if (!tableExists)
            {
                var createTableQuery = $@"
                    CREATE TABLE [{_schemaName}].[{_tableName}] (
                        [{_keyColumnName}] NVARCHAR(900) NOT NULL PRIMARY KEY,
                        [{_valueColumnName}] VARBINARY(MAX) NOT NULL,
                        [{_expirationColumnName}] DATETIME2 NULL,
                        [{_createdColumnName}] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                    );
                    
                    CREATE NONCLUSTERED INDEX IX_{_tableName}_{_expirationColumnName} 
                    ON [{_schemaName}].[{_tableName}] ([{_expirationColumnName}]) 
                    WHERE [{_expirationColumnName}] IS NOT NULL;
                    
                    CREATE NONCLUSTERED INDEX IX_{_tableName}_{_createdColumnName} 
                    ON [{_schemaName}].[{_tableName}] ([{_createdColumnName}]);";

                await using var createCommand = new SqlCommand(createTableQuery, connection);
                await createCommand.ExecuteNonQueryAsync();

                _logger.LogInformation("Created SQL Server cache table {SchemaName}.{TableName} with indexes",
                    _schemaName, _tableName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing SQL Server cache table");
            throw;
        }
    }

    private async Task PerformCleanupAsync()
    {
        if (!await _cleanupSemaphore.WaitAsync(0))
        {
            // Cleanup already in progress
            return;
        }

        try
        {
            using var activity = _activitySource.StartActivity("CleanupAsync");
            
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(_cleanupQuery, connection);
            var deletedRows = await command.ExecuteNonQueryAsync();

            if (deletedRows > 0)
            {
                activity?.SetTag("cache.cleanup_deleted", deletedRows);
                IncrementMetric("cleanup_deleted", deletedRows);
                _logger.LogDebug("Cleaned up {DeletedRows} expired entries from SQL Server cache", deletedRows);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during SQL Server cache cleanup");
            IncrementMetric("cleanup_error");
        }
        finally
        {
            _cleanupSemaphore.Release();
        }
    }

    private TimeSpan? GetExpiration(DistributedCacheEntryOptions options)
    {
        if (options.AbsoluteExpirationRelativeToNow.HasValue)
            return options.AbsoluteExpirationRelativeToNow;
            
        if (options.SlidingExpiration.HasValue)
            return options.SlidingExpiration;
            
        return _defaultExpiration;
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
            _logger.LogDebug("SQL Server cache metrics: {@Metrics}", metrics);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error logging SQL Server cache metrics");
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _metricsTimer?.Dispose();
        _cleanupSemaphore?.Dispose();
        _activitySource?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_cleanupTimer != null)
        {
            await _cleanupTimer.DisposeAsync();
        }
        
        if (_metricsTimer != null)
        {
            await _metricsTimer.DisposeAsync();
        }
        
        _cleanupSemaphore?.Dispose();
        _activitySource?.Dispose();
    }
}