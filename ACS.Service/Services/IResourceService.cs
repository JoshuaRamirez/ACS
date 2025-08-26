using ACS.Service.Domain;

namespace ACS.Service.Services;

public interface IResourceService
{
    // Basic CRUD Operations
    Task<IEnumerable<Resource>> GetAllResourcesAsync();
    Task<Resource?> GetResourceByIdAsync(int resourceId);
    Task<Resource?> GetResourceByUriAsync(string uri);
    Task<Resource> CreateResourceAsync(string uri, string description, string resourceType, string createdBy);
    Task<Resource> UpdateResourceAsync(int resourceId, string uri, string description, string resourceType, string updatedBy);
    Task DeleteResourceAsync(int resourceId, string deletedBy);
    
    // URI Pattern Matching
    Task<IEnumerable<Resource>> GetResourcesByPatternAsync(string uriPattern);
    Task<bool> DoesUriMatchResourceAsync(string requestUri, int resourceId);
    Task<Resource?> FindBestMatchingResourceAsync(string requestUri);
    Task<IEnumerable<Resource>> GetAllMatchingResourcesAsync(string requestUri);
    Task<bool> IsUriProtectedAsync(string requestUri);
    
    // Resource Discovery
    Task<IEnumerable<Resource>> DiscoverResourcesAsync(string basePath);
    Task<IEnumerable<Resource>> GetResourcesByTypeAsync(string resourceType);
    Task<IEnumerable<Resource>> GetChildResourcesAsync(int parentResourceId);
    Task<IEnumerable<Resource>> GetResourceHierarchyAsync(int resourceId);
    Task<Dictionary<string, List<Resource>>> GetResourceTreeAsync();
    
    // Resource Versioning
    Task<Resource> CreateResourceVersionAsync(int sourceResourceId, string version, string createdBy);
    Task<IEnumerable<Resource>> GetResourceVersionsAsync(int resourceId);
    Task<Resource?> GetResourceByVersionAsync(string uri, string version);
    Task SetActiveVersionAsync(int resourceId, string version, string updatedBy);
    Task<string> GetCurrentVersionAsync(int resourceId);
    
    // Pattern Management
    Task<bool> ValidateUriPatternAsync(string pattern);
    Task<string> ConvertWildcardToRegexAsync(string wildcardPattern);
    Task<IEnumerable<string>> ExtractVariablesFromPatternAsync(string uriPattern);
    Task<Dictionary<string, string>> ExtractVariableValuesAsync(string uriPattern, string actualUri);
    Task<bool> IsPatternConflictingAsync(string newPattern);
    
    // Access Control Integration
    Task<IEnumerable<Permission>> GetResourcePermissionsAsync(int resourceId);
    Task<IEnumerable<Entity>> GetResourceAuthorizedEntitiesAsync(int resourceId, HttpVerb verb);
    Task<bool> IsResourceAccessibleAsync(int resourceId, int entityId, HttpVerb verb);
    Task SetResourcePermissionAsync(int resourceId, int entityId, HttpVerb verb, bool grant, string setBy);
    Task RemoveResourcePermissionAsync(int resourceId, int entityId, HttpVerb verb, string removedBy);
    
    // Resource Metadata
    Task<Dictionary<string, object>> GetResourceMetadataAsync(int resourceId);
    Task SetResourceMetadataAsync(int resourceId, string key, object value, string setBy);
    Task RemoveResourceMetadataAsync(int resourceId, string key, string removedBy);
    Task<IEnumerable<Resource>> GetResourcesByMetadataAsync(string key, object value);
    Task<bool> HasResourceMetadataAsync(int resourceId, string key);
    
    // Bulk Operations
    Task<IEnumerable<Resource>> CreateResourcesBulkAsync(IEnumerable<(string Uri, string Description, string Type)> resources, string createdBy);
    Task DeleteResourcesBulkAsync(IEnumerable<int> resourceIds, string deletedBy);
    Task UpdateResourceTypeBulkAsync(IEnumerable<int> resourceIds, string newType, string updatedBy);
    Task SetPermissionsBulkAsync(int resourceId, IEnumerable<(int EntityId, HttpVerb Verb, bool Grant)> permissions, string setBy);
    
    // Search and Filtering
    Task<IEnumerable<Resource>> SearchResourcesAsync(string searchTerm);
    Task<IEnumerable<Resource>> GetResourcesByPermissionAsync(int entityId, HttpVerb verb);
    Task<IEnumerable<Resource>> GetUnprotectedResourcesAsync();
    Task<IEnumerable<Resource>> GetResourcesModifiedAfterAsync(DateTime timestamp);
    Task<IEnumerable<Resource>> GetResourcesPaginatedAsync(int page, int pageSize, string? filter = null);
    
    // Statistics and Analytics
    Task<int> GetTotalResourceCountAsync();
    Task<Dictionary<string, int>> GetResourceCountByTypeAsync();
    Task<Dictionary<HttpVerb, int>> GetPermissionCountByVerbAsync(int resourceId);
    Task<IEnumerable<(Resource Resource, int AccessCount)>> GetMostAccessedResourcesAsync(int topN);
    Task<IEnumerable<(Resource Resource, int PermissionCount)>> GetMostProtectedResourcesAsync(int topN);
    
    // URI Pattern Testing
    Task<object> TestUriPatternMatchAsync(string pattern, List<string> testUris);
    
    // Maintenance and Validation
    Task<IEnumerable<Resource>> ValidateAllResourcePatternsAsync();
    Task<IEnumerable<Resource>> FindDuplicateResourcesAsync();
    Task<IEnumerable<Resource>> FindOrphanedResourcesAsync();
    Task CleanupUnusedResourcesAsync(string cleanedBy);
    Task RebuildResourceIndexAsync();
}