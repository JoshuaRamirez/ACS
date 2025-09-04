namespace ACS.Service.Requests;

/// <summary>
/// Service request for system overview information
/// </summary>
public record SystemOverviewRequest
{
    public string TenantId { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for migration history information
/// </summary>
public record MigrationHistoryRequest
{
    public string TenantId { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
}

/// <summary>
/// Service request for system diagnostic information
/// </summary>
public record SystemDiagnosticsRequest
{
    public string TenantId { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
}