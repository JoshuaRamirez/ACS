using System.Collections.Concurrent;
using ACS.Service.Services;

namespace ACS.Service.Infrastructure;

/// <summary>
/// Base class for batch commands that process multiple items
/// </summary>
public abstract class BatchCommand : DomainCommand
{
    public int BatchSize { get; set; } = 100;
    public bool StopOnFirstError { get; set; } = false;
    public ConcurrentBag<BatchResult> Results { get; } = new();
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    
    /// <summary>
    /// Total number of items to process
    /// </summary>
    public abstract int TotalItems { get; }
    
    /// <summary>
    /// Number of successfully processed items
    /// </summary>
    public int SuccessCount => Results.Count(r => r.Success);
    
    /// <summary>
    /// Number of failed items
    /// </summary>
    public int FailureCount => Results.Count(r => !r.Success);
    
    /// <summary>
    /// Overall success rate
    /// </summary>
    public double SuccessRate => TotalItems > 0 ? (double)SuccessCount / TotalItems : 0;
}

/// <summary>
/// Result of a single item in a batch operation
/// </summary>
public class BatchResult
{
    public string ItemId { get; set; } = "";
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public object? ResultData { get; set; }
}

/// <summary>
/// Batch command with typed result
/// </summary>
public abstract class BatchCommand<TResult> : BatchCommand
{
    public TaskCompletionSource<BatchOperationResult<TResult>>? CompletionSource 
    { 
        get => base.CompletionSourceObject as TaskCompletionSource<BatchOperationResult<TResult>>;
        set => base.CompletionSourceObject = value; 
    }
}

/// <summary>
/// Result of a batch operation
/// </summary>
public class BatchOperationResult<TResult>
{
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public List<BatchResult> Results { get; set; } = new();
    public List<TResult> SuccessfulItems { get; set; } = new();
}