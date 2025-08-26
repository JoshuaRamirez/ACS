namespace ACS.WebApi.Resources;

/// <summary>
/// Resource for health check responses
/// </summary>
public record HealthCheckResource
{
    public string Status { get; init; } = string.Empty;
    public Dictionary<string, object> Details { get; init; } = new();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Resource for error responses
/// </summary>
public record ErrorResource
{
    public string Message { get; init; } = string.Empty;
    public string? Details { get; init; }
    public int StatusCode { get; init; } = 500;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Resource for validation errors
/// </summary>
public record ValidationErrorResource
{
    public string Field { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Resource for tenant health status
/// </summary>
public record TenantHealthResource
{
    public string TenantId { get; init; } = string.Empty;
    public bool IsHealthy { get; init; }
    public DateTime CheckTime { get; init; }
    public long UptimeSeconds { get; init; }
    public int ActiveConnections { get; init; }
    public long CommandsProcessed { get; init; }
    public string? Message { get; init; }
}

/// <summary>
/// Resource for component health status
/// </summary>
public record HealthStatusResource
{
    public string Component { get; init; } = string.Empty;
    public bool IsHealthy { get; init; }
    public DateTime CheckTime { get; init; }
    public string? Message { get; init; }
    public Dictionary<string, object>? Details { get; init; }
}

/// <summary>
/// Resource for detailed health responses
/// </summary>
public record DetailedHealthResource
{
    public bool OverallHealthy { get; init; }
    public DateTime CheckTime { get; init; }
    public HealthStatusResource WebApiHealth { get; init; } = new();
    public ICollection<TenantHealthResource> TenantHealths { get; init; } = new List<TenantHealthResource>();
}