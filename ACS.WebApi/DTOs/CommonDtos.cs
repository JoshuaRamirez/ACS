namespace ACS.WebApi.DTOs;

/// <summary>
/// Standard API response wrapper
/// </summary>
/// <typeparam name="T">The response data type</typeparam>
public record ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string Message { get; init; } = string.Empty;
    public IList<string> Errors { get; init; } = new List<string>();

    public ApiResponse(bool success, T? data, string message = "", IList<string>? errors = null)
    {
        Success = success;
        Data = data;
        Message = message;
        Errors = errors ?? new List<string>();
    }
}

/// <summary>
/// Bulk operation result wrapper
/// </summary>
/// <typeparam name="T">The resource type</typeparam>
public record BulkOperationResultResource<T>
{
    public IList<T> SuccessfulItems { get; init; } = new List<T>();
    public IList<BulkOperationError> FailedItems { get; init; } = new List<BulkOperationError>();
    public int TotalProcessed { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
}

/// <summary>
/// Bulk operation error details
/// </summary>
public record BulkOperationError
{
    public int Index { get; init; }
    public string Item { get; init; } = string.Empty;
    public IList<string> Errors { get; init; } = new List<string>();
}

/// <summary>
/// Bulk operation request wrapper
/// </summary>
/// <typeparam name="T">The resource type</typeparam>
public record BulkOperationResource<T>
{
    public IList<T> Items { get; init; } = new List<T>();
}