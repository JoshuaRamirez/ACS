namespace ACS.Infrastructure.Logging;

/// <summary>
/// Service for managing correlation IDs across requests
/// </summary>
public interface ICorrelationService
{
    /// <summary>
    /// Gets the current correlation ID
    /// </summary>
    string CorrelationId { get; }
    
    /// <summary>
    /// Gets the current request ID
    /// </summary>
    string RequestId { get; }
    
    /// <summary>
    /// Gets the current session ID
    /// </summary>
    string? SessionId { get; }
    
    /// <summary>
    /// Gets the current user ID
    /// </summary>
    string? UserId { get; }
    
    /// <summary>
    /// Gets the current tenant ID
    /// </summary>
    string? TenantId { get; }
    
    /// <summary>
    /// Sets correlation context for the current operation
    /// </summary>
    void SetContext(CorrelationContext context);
    
    /// <summary>
    /// Gets the current correlation context
    /// </summary>
    CorrelationContext GetContext();
    
    /// <summary>
    /// Creates a new child correlation ID for nested operations
    /// </summary>
    string CreateChildCorrelationId();
    
    /// <summary>
    /// Begins a new correlation scope
    /// </summary>
    IDisposable BeginScope(CorrelationContext context);
}

/// <summary>
/// Correlation context containing all tracking identifiers
/// </summary>
public class CorrelationContext
{
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
    public string? SessionId { get; set; }
    public string? UserId { get; set; }
    public string? TenantId { get; set; }
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public string? ParentId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object?> Properties { get; set; } = new();
    
    /// <summary>
    /// Creates a child context for nested operations
    /// </summary>
    public CorrelationContext CreateChild()
    {
        return new CorrelationContext
        {
            CorrelationId = Guid.NewGuid().ToString(),
            RequestId = RequestId,
            SessionId = SessionId,
            UserId = UserId,
            TenantId = TenantId,
            TraceId = TraceId,
            SpanId = Guid.NewGuid().ToString(),
            ParentId = CorrelationId,
            Timestamp = DateTime.UtcNow,
            Properties = new Dictionary<string, object?>(Properties)
        };
    }
}