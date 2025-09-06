using ACS.Service.Requests;
using ACS.Service.Responses;

namespace ACS.Service.Services;

/// <summary>
/// Service interface for Permission operations using request/response pattern
/// Supports permission checking, granting, revoking, and analysis operations
/// </summary>
public interface IPermissionService
{
    // Core permission operations
    Task<PermissionCheckResult> CheckPermissionAsync(int entityId, string entityType, int permissionId, int? resourceId = null);
    Task<PermissionGrantResponse> GrantPermissionAsync(GrantPermissionRequest request);
    Task<PermissionRevokeResponse> RevokePermissionAsync(RevokePermissionRequest request);
    
    // Advanced permission operations
    Task<PermissionCheckWithDetailsResponse> CheckPermissionWithDetailsAsync(CheckPermissionRequest request);
    Task<ValidatePermissionStructureResponse> ValidatePermissionStructureAsync(ValidatePermissionStructureRequest request);
    Task<GetEntityPermissionsResponse> GetEntityPermissionsAsync(GetEntityPermissionsRequest request);
    Task<GetPermissionUsageResponse> GetPermissionUsageAsync(GetPermissionUsageRequest request);
    
    // Bulk and complex operations
    Task<BulkPermissionUpdateResponse> BulkUpdatePermissionsAsync(BulkPermissionUpdateRequest request);
    Task<EvaluateComplexPermissionResponse> EvaluateComplexPermissionAsync(EvaluateComplexPermissionRequest request);
    Task<GetEffectivePermissionsResponse> GetEffectivePermissionsAsync(GetEffectivePermissionsRequest request);
    
    // Analysis operations
    Task<PermissionImpactAnalysisResponse> AnalyzePermissionImpactAsync(PermissionImpactAnalysisRequest request);
    Task<GetResourcePermissionsResponse> GetResourcePermissionsAsync(GetResourcePermissionsRequest request);
}