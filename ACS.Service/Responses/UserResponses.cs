using ACS.Service.Domain;

namespace ACS.Service.Responses;

/// <summary>
/// Service response for a single user operation
/// </summary>
public record UserResponse
{
    public User? User { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for multiple users with pagination
/// </summary>
public record UsersResponse
{
    public ICollection<User> Users { get; init; } = new List<User>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public bool HasNextPage => Page * PageSize < TotalCount;
    public bool HasPreviousPage => Page > 1;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for user creation operation
/// </summary>
public record CreateUserResponse
{
    public User? User { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for user update operation
/// </summary>
public record UpdateUserResponse
{
    public User? User { get; init; }
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for user deletion operation
/// </summary>
public record DeleteUserResponse
{
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for user-group relationship operations
/// </summary>
public record UserGroupResponse
{
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for user-role relationship operations
/// </summary>
public record UserRoleResponse
{
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public ICollection<string> Errors { get; init; } = new List<string>();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service response for bulk user operations
/// </summary>
public record BulkUserResponse<T>
{
    public int TotalRequested { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public ICollection<T> SuccessfulItems { get; init; } = new List<T>();
    public ICollection<BulkOperationError> Errors { get; init; } = new List<BulkOperationError>();
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Bulk operation error details
/// </summary>
public record BulkOperationError
{
    public int ItemIndex { get; init; }
    public string Error { get; init; } = string.Empty;
    public string? Details { get; init; }
}