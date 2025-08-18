namespace ACS.WebApi.DTOs;

public record ApiResponse<T>(
    bool Success,
    T? Data,
    string? Message = null,
    List<string>? Errors = null
);

public record ErrorResponse(
    string Message,
    string? Details = null,
    int StatusCode = 500,
    DateTime Timestamp = default
)
{
    public ErrorResponse(string message, string? details = null, int statusCode = 500) 
        : this(message, details, statusCode, DateTime.UtcNow) { }
}

public record ValidationError(
    string Field,
    string Message
);

public record PagedRequest(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? SortBy = null,
    bool SortDescending = false
);

public record HealthCheckResponse(
    string Status,
    Dictionary<string, object> Details,
    DateTime Timestamp = default
)
{
    public HealthCheckResponse(string status, Dictionary<string, object> details) 
        : this(status, details, DateTime.UtcNow) { }
};