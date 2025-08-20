using Grpc.Net.Client;

namespace ACS.Infrastructure.Services;

/// <summary>
/// Service for accessing tenant context information
/// </summary>
public interface ITenantContextService
{
    /// <summary>
    /// Gets the current tenant ID
    /// </summary>
    string? GetTenantId();
    
    /// <summary>
    /// Gets the current tenant ID, throwing if not available
    /// </summary>
    string GetRequiredTenantId();
    
    /// <summary>
    /// Gets tenant process information
    /// </summary>
    TenantProcessInfo? GetTenantProcessInfo();
    
    /// <summary>
    /// Gets gRPC channel for the current tenant
    /// </summary>
    GrpcChannel? GetGrpcChannel();
    
    /// <summary>
    /// Sets tenant context (used by middleware)
    /// </summary>
    void SetTenantContext(string tenantId, TenantProcessInfo? processInfo = null, GrpcChannel? grpcChannel = null);
    
    /// <summary>
    /// Clears tenant context
    /// </summary>
    void ClearTenantContext();
    
    /// <summary>
    /// Validates if the current user has access to the tenant
    /// </summary>
    Task<bool> ValidateTenantAccessAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Tenant process information
/// </summary>
public class TenantProcessInfo
{
    public string TenantId { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public string GrpcEndpoint { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime LastHealthCheck { get; set; }
}