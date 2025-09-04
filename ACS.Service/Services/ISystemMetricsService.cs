using ACS.Service.Requests;
using ACS.Service.Responses;

namespace ACS.Service.Services;

/// <summary>
/// Service interface for system metrics and diagnostic operations
/// Provides system overview, health status, and diagnostic information
/// </summary>
public interface ISystemMetricsService
{
    /// <summary>
    /// Gets system overview including entity counts and status
    /// </summary>
    Task<SystemOverviewResponse> GetSystemOverviewAsync(SystemOverviewRequest request);
    
    /// <summary>
    /// Gets applied database migrations
    /// </summary>
    Task<MigrationHistoryResponse> GetMigrationHistoryAsync(MigrationHistoryRequest request);
    
    /// <summary>
    /// Gets comprehensive system diagnostic information
    /// </summary>
    Task<SystemDiagnosticsResponse> GetSystemDiagnosticsAsync(SystemDiagnosticsRequest request);
}