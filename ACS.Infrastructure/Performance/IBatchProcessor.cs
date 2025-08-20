namespace ACS.Infrastructure.Performance;

/// <summary>
/// Interface for batch processing operations
/// </summary>
public interface IBatchProcessor
{
    /// <summary>
    /// Processes items in batches
    /// </summary>
    Task<BatchResult<TResult>> ProcessBatchAsync<TItem, TResult>(
        IEnumerable<TItem> items,
        Func<IEnumerable<TItem>, Task<IEnumerable<TResult>>> batchOperation,
        BatchOptions? options = null);
    
    /// <summary>
    /// Executes database commands in batches
    /// </summary>
    Task<BatchExecutionResult> ExecuteBatchCommandsAsync(
        IEnumerable<BatchCommand> commands,
        BatchOptions? options = null);
    
    /// <summary>
    /// Performs bulk insert operation
    /// </summary>
    Task<int> BulkInsertAsync<T>(
        IEnumerable<T> entities,
        BulkInsertOptions? options = null) where T : class;
    
    /// <summary>
    /// Performs bulk update operation
    /// </summary>
    Task<int> BulkUpdateAsync<T>(
        IEnumerable<T> entities,
        BulkUpdateOptions? options = null) where T : class;
    
    /// <summary>
    /// Performs bulk delete operation
    /// </summary>
    Task<int> BulkDeleteAsync<T>(
        IEnumerable<T> entities) where T : class;
}

/// <summary>
/// Batch processing options
/// </summary>
public class BatchOptions
{
    public int BatchSize { get; set; } = 100;
    public int MaxDegreeOfParallelism { get; set; } = 1;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    public bool StopOnFirstError { get; set; } = false;
    public bool EnableTransaction { get; set; } = true;
    public int RetryCount { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Bulk insert options
/// </summary>
public class BulkInsertOptions : BatchOptions
{
    public bool CheckConstraints { get; set; } = true;
    public bool FireTriggers { get; set; } = false;
    public bool KeepIdentity { get; set; } = false;
    public bool KeepNulls { get; set; } = true;
    public string? TableName { get; set; }
}

/// <summary>
/// Bulk update options
/// </summary>
public class BulkUpdateOptions : BatchOptions
{
    public List<string> UpdateColumns { get; set; } = new();
    public List<string> KeyColumns { get; set; } = new();
    public bool UpdateOnlyChangedColumns { get; set; } = true;
}

/// <summary>
/// Batch command for execution
/// </summary>
public class BatchCommand
{
    public string CommandText { get; set; } = string.Empty;
    public Dictionary<string, object?> Parameters { get; set; } = new();
    public CommandType Type { get; set; } = CommandType.Text;
}

/// <summary>
/// Command type enumeration
/// </summary>
public enum CommandType
{
    Text,
    StoredProcedure
}

/// <summary>
/// Batch processing result
/// </summary>
public class BatchResult<T>
{
    public bool Success { get; set; }
    public List<T> Results { get; set; } = new();
    public List<BatchError> Errors { get; set; } = new();
    public int ProcessedCount { get; set; }
    public int FailedCount { get; set; }
    public TimeSpan ElapsedTime { get; set; }
}

/// <summary>
/// Batch execution result
/// </summary>
public class BatchExecutionResult
{
    public bool Success { get; set; }
    public int AffectedRows { get; set; }
    public List<BatchError> Errors { get; set; } = new();
    public int ExecutedCommands { get; set; }
    public TimeSpan ElapsedTime { get; set; }
}

/// <summary>
/// Batch processing error
/// </summary>
public class BatchError
{
    public int BatchIndex { get; set; }
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public DateTime Timestamp { get; set; }
}