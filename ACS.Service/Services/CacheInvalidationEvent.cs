namespace ACS.Service.Services;

/// <summary>
/// Event for cache invalidation operations
/// </summary>
public class CacheInvalidationEvent
{
    public string Key { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Source { get; set; } = string.Empty;
    public string[] DependentKeys { get; set; } = Array.Empty<string>();
    public CacheType Type { get; set; }
    public string TenantId { get; set; } = string.Empty;
}