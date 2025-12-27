using ACS.Service.Data;
using ACS.Service.Data.Models;
using ACS.Service.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace ACS.Service.Services;

public class ResourceService : IResourceService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<ResourceService> _logger;
    private readonly IPermissionEvaluationService _permissionService;

    public ResourceService(
        ApplicationDbContext dbContext,
        ILogger<ResourceService> logger,
        IPermissionEvaluationService permissionService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _permissionService = permissionService;
    }

    #region Basic CRUD Operations

    public async Task<IEnumerable<Domain.Resource>> GetAllResourcesAsync()
    {
        var resources = await _dbContext.Resources.ToListAsync();
        return resources.Select(ConvertToDomainResource);
    }

    public async Task<Domain.Resource?> GetResourceByIdAsync(int resourceId)
    {
        var resource = await _dbContext.Resources.FindAsync(resourceId);
        return resource != null ? ConvertToDomainResource(resource) : null;
    }

    public async Task<Domain.Resource?> GetResourceByUriAsync(string uri)
    {
        var resource = await _dbContext.Resources
            .FirstOrDefaultAsync(r => r.Uri == uri);
        return resource != null ? ConvertToDomainResource(resource) : null;
    }

    public async Task<Domain.Resource> CreateResourceAsync(string uri, string description, string resourceType, string createdBy)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(uri))
            {
                throw new ArgumentException("Resource URI cannot be null or empty", nameof(uri));
            }

            if (string.IsNullOrWhiteSpace(resourceType))
            {
                throw new ArgumentException("Resource type cannot be null or empty", nameof(resourceType));
            }

            if (string.IsNullOrWhiteSpace(createdBy))
            {
                throw new ArgumentException("CreatedBy cannot be null or empty", nameof(createdBy));
            }

            // Check if resource URI already exists
            var existingResource = await _dbContext.Resources
                .FirstOrDefaultAsync(r => r.Uri == uri);
            if (existingResource != null)
            {
                _logger.LogWarning("Attempted to create resource with duplicate URI: {Uri}", uri);
                throw new InvalidOperationException($"Resource with URI '{uri}' already exists");
            }

            var resource = new Data.Models.Resource
            {
                Uri = uri,
                Description = description,
                ResourceType = resourceType,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Version = "1.0.0",
                IsActive = true
            };

            _dbContext.Resources.Add(resource);
            await _dbContext.SaveChangesAsync();

            await LogAuditAsync("CreateResource", "Resource", resource.Id, createdBy,
                $"Created resource '{uri}'");

            _logger.LogInformation("Created resource {ResourceId} with URI {Uri} by {CreatedBy}",
                resource.Id, uri, createdBy);

            return ConvertToDomainResource(resource);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument provided for resource creation: {Uri}", uri);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during resource creation: {Uri}", uri);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating resource {Uri}", uri);
            throw new InvalidOperationException($"Failed to create resource '{uri}': {ex.Message}", ex);
        }
    }

    public async Task<Domain.Resource> UpdateResourceAsync(int resourceId, string uri, string description, string resourceType, string updatedBy)
    {
        try
        {
            if (resourceId <= 0)
            {
                throw new ArgumentException("Resource ID must be a positive integer", nameof(resourceId));
            }

            if (string.IsNullOrWhiteSpace(uri))
            {
                throw new ArgumentException("Resource URI cannot be null or empty", nameof(uri));
            }

            if (string.IsNullOrWhiteSpace(resourceType))
            {
                throw new ArgumentException("Resource type cannot be null or empty", nameof(resourceType));
            }

            if (string.IsNullOrWhiteSpace(updatedBy))
            {
                throw new ArgumentException("UpdatedBy cannot be null or empty", nameof(updatedBy));
            }

            var resource = await _dbContext.Resources.FindAsync(resourceId);
            if (resource == null)
            {
                _logger.LogWarning("Attempted to update non-existent resource {ResourceId}", resourceId);
                throw new InvalidOperationException($"Resource {resourceId} not found");
            }

            var oldUri = resource.Uri;
            resource.Uri = uri;
            resource.Description = description;
            resource.ResourceType = resourceType;
            resource.UpdatedAt = DateTime.UtcNow;

            _dbContext.Resources.Update(resource);
            await _dbContext.SaveChangesAsync();

            await LogAuditAsync("UpdateResource", "Resource", resource.Id, updatedBy,
                $"Updated resource from '{oldUri}' to '{uri}'");

            _logger.LogInformation("Updated resource {ResourceId} with URI {Uri} by {UpdatedBy}",
                resourceId, uri, updatedBy);

            return ConvertToDomainResource(resource);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument provided for resource update: {ResourceId}", resourceId);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during resource update: {ResourceId}", resourceId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating resource {ResourceId}", resourceId);
            throw new InvalidOperationException($"Failed to update resource {resourceId}: {ex.Message}", ex);
        }
    }

    public async Task DeleteResourceAsync(int resourceId, string deletedBy)
    {
        try
        {
            if (resourceId <= 0)
            {
                throw new ArgumentException("Resource ID must be a positive integer", nameof(resourceId));
            }

            if (string.IsNullOrWhiteSpace(deletedBy))
            {
                throw new ArgumentException("DeletedBy cannot be null or empty", nameof(deletedBy));
            }

            var resource = await _dbContext.Resources.FindAsync(resourceId);
            if (resource == null)
            {
                _logger.LogWarning("Attempted to delete non-existent resource {ResourceId}", resourceId);
                throw new InvalidOperationException($"Resource {resourceId} not found");
            }

            var uri = resource.Uri;

            // Check for active dependencies
            var hasActiveDependencies = await HasActiveDependenciesAsync(resourceId);
            if (hasActiveDependencies)
            {
                _logger.LogWarning("Cannot delete resource {ResourceId} due to active URI access permissions", resourceId);
                throw new InvalidOperationException($"Cannot delete resource {resourceId} as it has active URI access permissions. Remove permissions first.");
            }

            // Delete all permissions associated with this resource
            var uriAccesses = await _dbContext.UriAccesses
                .Where(ua => ua.ResourceId == resourceId)
                .ToListAsync();

            _dbContext.UriAccesses.RemoveRange(uriAccesses);
            _dbContext.Resources.Remove(resource);
            await _dbContext.SaveChangesAsync();

            await LogAuditAsync("DeleteResource", "Resource", resourceId, deletedBy,
                $"Deleted resource '{uri}'");

            _logger.LogInformation("Deleted resource {ResourceId} by {DeletedBy}", resourceId, deletedBy);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument provided for resource deletion: {ResourceId}", resourceId);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during resource deletion: {ResourceId}", resourceId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting resource {ResourceId}", resourceId);
            throw new InvalidOperationException($"Failed to delete resource {resourceId}: {ex.Message}", ex);
        }
    }

    #endregion

    #region URI Pattern Matching

    public async Task<IEnumerable<Domain.Resource>> GetResourcesByPatternAsync(string uriPattern)
    {
        var regex = ConvertWildcardToRegex(uriPattern);
        var resources = await _dbContext.Resources.ToListAsync();
        
        var matchingResources = resources
            .Where(r => Regex.IsMatch(r.Uri, regex, RegexOptions.IgnoreCase))
            .ToList();

        return matchingResources.Select(ConvertToDomainResource);
    }

    public async Task<bool> DoesUriMatchResourceAsync(string requestUri, int resourceId)
    {
        var resource = await _dbContext.Resources.FindAsync(resourceId);
        if (resource == null)
            return false;

        var pattern = ConvertWildcardToRegex(resource.Uri);
        return Regex.IsMatch(requestUri, pattern, RegexOptions.IgnoreCase);
    }

    public async Task<Domain.Resource?> FindBestMatchingResourceAsync(string requestUri)
    {
        var resources = await _dbContext.Resources.ToListAsync();
        
        // Find exact match first
        var exactMatch = resources.FirstOrDefault(r => r.Uri.Equals(requestUri, StringComparison.OrdinalIgnoreCase));
        if (exactMatch != null)
            return ConvertToDomainResource(exactMatch);

        // Find pattern matches, prioritize by specificity (length and wildcard count)
        var patternMatches = resources
            .Where(r => r.Uri.Contains("*") || r.Uri.Contains("{"))
            .Select(r => new 
            { 
                Resource = r, 
                Pattern = ConvertWildcardToRegex(r.Uri),
                Specificity = CalculateSpecificity(r.Uri)
            })
            .Where(x => Regex.IsMatch(requestUri, x.Pattern, RegexOptions.IgnoreCase))
            .OrderByDescending(x => x.Specificity)
            .ThenByDescending(x => x.Resource.Uri.Length)
            .FirstOrDefault();

        return patternMatches != null ? ConvertToDomainResource(patternMatches.Resource) : null;
    }

    public async Task<IEnumerable<Domain.Resource>> GetAllMatchingResourcesAsync(string requestUri)
    {
        var resources = await _dbContext.Resources.ToListAsync();
        
        var matchingResources = new List<Data.Models.Resource>();

        // Add exact matches
        matchingResources.AddRange(resources.Where(r => 
            r.Uri.Equals(requestUri, StringComparison.OrdinalIgnoreCase)));

        // Add pattern matches
        foreach (var resource in resources.Where(r => r.Uri.Contains("*") || r.Uri.Contains("{")))
        {
            var pattern = ConvertWildcardToRegex(resource.Uri);
            if (Regex.IsMatch(requestUri, pattern, RegexOptions.IgnoreCase))
            {
                matchingResources.Add(resource);
            }
        }

        return matchingResources.Distinct().Select(ConvertToDomainResource);
    }

    public async Task<bool> IsUriProtectedAsync(string requestUri)
    {
        var matchingResource = await FindBestMatchingResourceAsync(requestUri);
        if (matchingResource == null)
            return false;

        // Check if any permissions are defined for this resource
        return await _dbContext.UriAccesses.AnyAsync(ua => ua.ResourceId == matchingResource.Id);
    }

    #endregion

    #region Resource Discovery

    public async Task<IEnumerable<Domain.Resource>> DiscoverResourcesAsync(string basePath)
    {
        var discoveredResources = new List<Domain.Resource>();
        
        // This would typically integrate with API metadata, reflection, or configuration
        // For now, we'll discover resources based on existing patterns in the database
        var existingResources = await _dbContext.Resources
            .Where(r => r.Uri.StartsWith(basePath))
            .ToListAsync();

        // Generate common REST patterns
        var entities = new[] { "users", "groups", "roles", "permissions", "resources" };
        foreach (var entity in entities)
        {
            var patterns = new[]
            {
                $"{basePath}/{entity}",
                $"{basePath}/{entity}/{{id}}",
                $"{basePath}/{entity}/{{id}}/{{action}}",
                $"{basePath}/{entity}/bulk",
                $"{basePath}/{entity}/search"
            };

            foreach (var pattern in patterns)
            {
                if (!existingResources.Any(r => r.Uri.Equals(pattern, StringComparison.OrdinalIgnoreCase)))
                {
                    discoveredResources.Add(new Domain.Resource
                    {
                        Uri = pattern,
                        Description = $"Auto-discovered resource for {pattern}",
                        ResourceType = "API",
                        Version = "1.0.0",
                        IsActive = true
                    });
                }
            }
        }

        _logger.LogInformation("Discovered {Count} new resources under {BasePath}",
            discoveredResources.Count, basePath);

        return discoveredResources;
    }

    public async Task<IEnumerable<Domain.Resource>> GetResourcesByTypeAsync(string resourceType)
    {
        var resources = await _dbContext.Resources
            .Where(r => r.ResourceType == resourceType)
            .ToListAsync();

        return resources.Select(ConvertToDomainResource);
    }

    public async Task<IEnumerable<Domain.Resource>> GetChildResourcesAsync(int parentResourceId)
    {
        var parentResource = await _dbContext.Resources.FindAsync(parentResourceId);
        if (parentResource == null)
            return Enumerable.Empty<Domain.Resource>();

        var childResources = await _dbContext.Resources
            .Where(r => r.Uri.StartsWith(parentResource.Uri + "/") && r.Id != parentResourceId)
            .ToListAsync();

        return childResources.Select(ConvertToDomainResource);
    }

    public async Task<IEnumerable<Domain.Resource>> GetResourceHierarchyAsync(int resourceId)
    {
        var resource = await _dbContext.Resources.FindAsync(resourceId);
        if (resource == null)
            return Enumerable.Empty<Domain.Resource>();

        var hierarchy = new List<Data.Models.Resource> { resource };
        
        // Get all child resources recursively
        var childResources = await GetChildResourcesRecursiveAsync(resource.Uri);
        hierarchy.AddRange(childResources);

        return hierarchy.Select(ConvertToDomainResource);
    }

    public async Task<Dictionary<string, List<Domain.Resource>>> GetResourceTreeAsync()
    {
        var resources = await _dbContext.Resources.ToListAsync();
        var tree = new Dictionary<string, List<Domain.Resource>>();

        // Group resources by their base path
        foreach (var resource in resources)
        {
            var segments = resource.Uri.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var basePath = segments.Length > 0 ? "/" + segments[0] : "/";

            if (!tree.ContainsKey(basePath))
                tree[basePath] = new List<Domain.Resource>();

            tree[basePath].Add(ConvertToDomainResource(resource));
        }

        return tree;
    }

    #endregion

    #region Resource Versioning

    public async Task<Domain.Resource> CreateResourceVersionAsync(int sourceResourceId, string version, string createdBy)
    {
        var sourceResource = await _dbContext.Resources.FindAsync(sourceResourceId);
        if (sourceResource == null)
        {
            throw new InvalidOperationException($"Source resource {sourceResourceId} not found");
        }

        var versionedResource = new Data.Models.Resource
        {
            Uri = sourceResource.Uri,
            Description = $"{sourceResource.Description} (Version {version})",
            ResourceType = sourceResource.ResourceType,
            Version = version,
            ParentResourceId = sourceResourceId,
            IsActive = false, // New versions start as inactive
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Resources.Add(versionedResource);
        await _dbContext.SaveChangesAsync();

        await LogAuditAsync("CreateResourceVersion", "Resource", versionedResource.Id, createdBy,
            $"Created version {version} of resource {sourceResource.Uri}");

        _logger.LogInformation("Created version {Version} of resource {ResourceId} by {CreatedBy}",
            version, sourceResourceId, createdBy);

        return ConvertToDomainResource(versionedResource);
    }

    public async Task<IEnumerable<Domain.Resource>> GetResourceVersionsAsync(int resourceId)
    {
        var versions = await _dbContext.Resources
            .Where(r => r.Id == resourceId || r.ParentResourceId == resourceId)
            .OrderBy(r => r.Version)
            .ToListAsync();

        return versions.Select(ConvertToDomainResource);
    }

    public async Task<Domain.Resource?> GetResourceByVersionAsync(string uri, string version)
    {
        var resource = await _dbContext.Resources
            .FirstOrDefaultAsync(r => r.Uri == uri && r.Version == version);

        return resource != null ? ConvertToDomainResource(resource) : null;
    }

    public async Task SetActiveVersionAsync(int resourceId, string version, string updatedBy)
    {
        var resource = await _dbContext.Resources.FindAsync(resourceId);
        if (resource == null)
        {
            throw new InvalidOperationException($"Resource {resourceId} not found");
        }

        // Deactivate all versions of this resource
        var allVersions = await _dbContext.Resources
            .Where(r => r.Uri == resource.Uri)
            .ToListAsync();

        foreach (var r in allVersions)
        {
            r.IsActive = r.Version == version;
            r.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        await LogAuditAsync("SetActiveResourceVersion", "Resource", resourceId, updatedBy,
            $"Set active version to {version} for resource {resource.Uri}");

        _logger.LogInformation("Set active version to {Version} for resource {ResourceId} by {UpdatedBy}",
            version, resourceId, updatedBy);
    }

    public async Task<string> GetCurrentVersionAsync(int resourceId)
    {
        var resource = await _dbContext.Resources.FindAsync(resourceId);
        return resource?.Version ?? "1.0.0";
    }

    #endregion

    #region Pattern Management

    public Task<bool> ValidateUriPatternAsync(string pattern)
    {
        try
        {
            // Check for valid wildcard and parameter patterns
            if (string.IsNullOrWhiteSpace(pattern))
                return Task.FromResult(false);

            // Check for valid characters
            var validPattern = @"^[a-zA-Z0-9\-_.~/{}*]+$";
            if (!Regex.IsMatch(pattern, validPattern))
                return Task.FromResult(false);

            // Check for balanced braces
            var openBraces = pattern.Count(c => c == '{');
            var closeBraces = pattern.Count(c => c == '}');
            if (openBraces != closeBraces)
                return Task.FromResult(false);

            // Try to convert to regex to validate
            ConvertWildcardToRegex(pattern);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<string> ConvertWildcardToRegexAsync(string wildcardPattern)
    {
        return Task.FromResult(ConvertWildcardToRegex(wildcardPattern));
    }

    public async Task<IEnumerable<string>> ExtractVariablesFromPatternAsync(string uriPattern)
    {
        var variables = new List<string>();
        var regex = new Regex(@"\{([^}]+)\}");
        var matches = regex.Matches(uriPattern);

        foreach (Match match in matches)
        {
            variables.Add(match.Groups[1].Value);
        }

        return await Task.FromResult(variables);
    }

    public async Task<Dictionary<string, string>> ExtractVariableValuesAsync(string uriPattern, string actualUri)
    {
        var variables = await ExtractVariablesFromPatternAsync(uriPattern);
        var values = new Dictionary<string, string>();

        // Convert pattern to regex with capture groups
        var regexPattern = uriPattern;
        foreach (var variable in variables)
        {
            regexPattern = regexPattern.Replace($"{{{variable}}}", $"(?<{variable}>[^/]+)");
        }
        regexPattern = regexPattern.Replace("*", ".*");
        regexPattern = "^" + regexPattern + "$";

        var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
        var match = regex.Match(actualUri);

        if (match.Success)
        {
            foreach (var variable in variables)
            {
                if (match.Groups[variable].Success)
                {
                    values[variable] = match.Groups[variable].Value;
                }
            }
        }

        return values;
    }

    public async Task<bool> IsPatternConflictingAsync(string newPattern)
    {
        var resources = await _dbContext.Resources.ToListAsync();
        var newRegex = ConvertWildcardToRegex(newPattern);

        foreach (var resource in resources)
        {
            var existingRegex = ConvertWildcardToRegex(resource.Uri);
            
            // Check if patterns could match the same URI
            // This is a simplified check - a more thorough implementation would test with sample URIs
            if (newRegex == existingRegex || 
                (newPattern.Replace("*", "") == resource.Uri.Replace("*", "")))
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Access Control Integration

    public async Task<IEnumerable<Domain.Permission>> GetResourcePermissionsAsync(int resourceId)
    {
        var permissions = new List<Domain.Permission>();

        var uriAccesses = await _dbContext.UriAccesses
            .Include(ua => ua.Resource)
            .Include(ua => ua.VerbType)
            .Include(ua => ua.PermissionScheme)
            .Where(ua => ua.ResourceId == resourceId)
            .ToListAsync();

        foreach (var access in uriAccesses)
        {
            permissions.Add(new Domain.Permission
            {
                Id = access.Id,
                EntityId = access.PermissionScheme.EntityId ?? 0,
                Uri = access.Resource.Uri,
                HttpVerb = Enum.Parse<Domain.HttpVerb>(access.VerbType.VerbName),
                Grant = access.Grant,
                Deny = access.Deny,
                Scheme = Domain.Scheme.ApiUriAuthorization
            });
        }

        return permissions;
    }

    public async Task<IEnumerable<Domain.Entity>> GetResourceAuthorizedEntitiesAsync(int resourceId, Domain.HttpVerb verb)
    {
        var authorizedEntities = new List<Domain.Entity>();

        var uriAccesses = await _dbContext.UriAccesses
            .Include(ua => ua.VerbType)
            .Include(ua => ua.PermissionScheme)
                .ThenInclude(ps => ps.Entity)
            .Where(ua => ua.ResourceId == resourceId && 
                        ua.VerbType.VerbName == verb.ToString() &&
                        ua.Grant)
            .ToListAsync();

        foreach (var access in uriAccesses)
        {
            var entity = access.PermissionScheme.Entity;
            
            // Load the specific entity type
            if (entity.EntityType == "User")
            {
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.EntityId == entity.Id);
                if (user != null)
                {
                    authorizedEntities.Add(new Domain.User { Id = user.Id, Name = user.Name });
                }
            }
            else if (entity.EntityType == "Group")
            {
                var group = await _dbContext.Groups.FirstOrDefaultAsync(g => g.EntityId == entity.Id);
                if (group != null)
                {
                    authorizedEntities.Add(new Domain.Group { Id = group.Id, Name = group.Name });
                }
            }
            else if (entity.EntityType == "Role")
            {
                var role = await _dbContext.Roles.FirstOrDefaultAsync(r => r.EntityId == entity.Id);
                if (role != null)
                {
                    authorizedEntities.Add(new Domain.Role { Id = role.Id, Name = role.Name });
                }
            }
        }

        return authorizedEntities;
    }

    public async Task<bool> IsResourceAccessibleAsync(int resourceId, int entityId, Domain.HttpVerb verb)
    {
        var resource = await _dbContext.Resources.FindAsync(resourceId);
        if (resource == null)
            return false;

        return await _permissionService.HasPermissionAsync(entityId, resource.Uri, verb.ToString());
    }

    public async Task SetResourcePermissionAsync(int resourceId, int entityId, Domain.HttpVerb verb, bool grant, string setBy)
    {
        var resource = await _dbContext.Resources.FindAsync(resourceId);
        if (resource == null)
        {
            throw new InvalidOperationException($"Resource {resourceId} not found");
        }

        // Get or create permission scheme
        var schemeType = await _dbContext.SchemeTypes
            .FirstOrDefaultAsync(st => st.SchemeName == "ApiUriAuthorization");

        if (schemeType == null)
        {
            schemeType = new SchemeType { SchemeName = "ApiUriAuthorization" };
            _dbContext.SchemeTypes.Add(schemeType);
            await _dbContext.SaveChangesAsync();
        }

        var permissionScheme = await _dbContext.EntityPermissions
            .FirstOrDefaultAsync(ps => ps.EntityId == entityId && ps.SchemeTypeId == schemeType.Id);

        if (permissionScheme == null)
        {
            permissionScheme = new PermissionScheme
            {
                EntityId = entityId,
                SchemeTypeId = schemeType.Id
            };
            _dbContext.EntityPermissions.Add(permissionScheme);
            await _dbContext.SaveChangesAsync();
        }

        // Get or create verb type
        var verbType = await _dbContext.VerbTypes
            .FirstOrDefaultAsync(vt => vt.VerbName == verb.ToString());

        if (verbType == null)
        {
            verbType = new VerbType { VerbName = verb.ToString() };
            _dbContext.VerbTypes.Add(verbType);
            await _dbContext.SaveChangesAsync();
        }

        // Check for existing access
        var existingAccess = await _dbContext.UriAccesses
            .FirstOrDefaultAsync(ua => ua.ResourceId == resourceId &&
                                      ua.PermissionSchemeId == permissionScheme.Id &&
                                      ua.VerbTypeId == verbType.Id);

        if (existingAccess != null)
        {
            existingAccess.Grant = grant;
            existingAccess.Deny = !grant;
            _dbContext.UriAccesses.Update(existingAccess);
        }
        else
        {
            var uriAccess = new UriAccess
            {
                ResourceId = resourceId,
                VerbTypeId = verbType.Id,
                PermissionSchemeId = permissionScheme.Id,
                Grant = grant,
                Deny = !grant
            };
            _dbContext.UriAccesses.Add(uriAccess);
        }

        await _dbContext.SaveChangesAsync();

        await LogAuditAsync("SetResourcePermission", "Resource", resourceId, setBy,
            $"Set permission for entity {entityId} on resource {resource.Uri} {verb}: Grant={grant}");

        _logger.LogInformation("Set permission for entity {EntityId} on resource {ResourceId} {Verb}: Grant={Grant} by {SetBy}",
            entityId, resourceId, verb, grant, setBy);
    }

    public async Task RemoveResourcePermissionAsync(int resourceId, int entityId, Domain.HttpVerb verb, string removedBy)
    {
        var resource = await _dbContext.Resources.FindAsync(resourceId);
        if (resource == null)
        {
            throw new InvalidOperationException($"Resource {resourceId} not found");
        }

        var schemeType = await _dbContext.SchemeTypes
            .FirstOrDefaultAsync(st => st.SchemeName == "ApiUriAuthorization");

        if (schemeType == null)
            return;

        var permissionScheme = await _dbContext.EntityPermissions
            .FirstOrDefaultAsync(ps => ps.EntityId == entityId && ps.SchemeTypeId == schemeType.Id);

        if (permissionScheme == null)
            return;

        var verbType = await _dbContext.VerbTypes
            .FirstOrDefaultAsync(vt => vt.VerbName == verb.ToString());

        if (verbType == null)
            return;

        var uriAccess = await _dbContext.UriAccesses
            .FirstOrDefaultAsync(ua => ua.ResourceId == resourceId &&
                                      ua.PermissionSchemeId == permissionScheme.Id &&
                                      ua.VerbTypeId == verbType.Id);

        if (uriAccess != null)
        {
            _dbContext.UriAccesses.Remove(uriAccess);
            await _dbContext.SaveChangesAsync();

            await LogAuditAsync("RemoveResourcePermission", "Resource", resourceId, removedBy,
                $"Removed permission for entity {entityId} on resource {resource.Uri} {verb}");

            _logger.LogInformation("Removed permission for entity {EntityId} on resource {ResourceId} {Verb} by {RemovedBy}",
                entityId, resourceId, verb, removedBy);
        }
    }

    #endregion

    #region Resource Metadata

    private Dictionary<int, Dictionary<string, object>> _resourceMetadata = new();

    public async Task<Dictionary<string, object>> GetResourceMetadataAsync(int resourceId)
    {
        if (_resourceMetadata.ContainsKey(resourceId))
            return await Task.FromResult(new Dictionary<string, object>(_resourceMetadata[resourceId]));

        return await Task.FromResult(new Dictionary<string, object>());
    }

    public async Task SetResourceMetadataAsync(int resourceId, string key, object value, string setBy)
    {
        if (!_resourceMetadata.ContainsKey(resourceId))
            _resourceMetadata[resourceId] = new Dictionary<string, object>();

        _resourceMetadata[resourceId][key] = value;

        await LogAuditAsync("SetResourceMetadata", "Resource", resourceId, setBy,
            $"Set metadata key '{key}' for resource {resourceId}");

        _logger.LogInformation("Set metadata key '{Key}' for resource {ResourceId} by {SetBy}",
            key, resourceId, setBy);

        await Task.CompletedTask;
    }

    public async Task RemoveResourceMetadataAsync(int resourceId, string key, string removedBy)
    {
        if (_resourceMetadata.ContainsKey(resourceId) && _resourceMetadata[resourceId].ContainsKey(key))
        {
            _resourceMetadata[resourceId].Remove(key);

            if (_resourceMetadata[resourceId].Count == 0)
                _resourceMetadata.Remove(resourceId);

            await LogAuditAsync("RemoveResourceMetadata", "Resource", resourceId, removedBy,
                $"Removed metadata key '{key}' from resource {resourceId}");

            _logger.LogInformation("Removed metadata key '{Key}' from resource {ResourceId} by {RemovedBy}",
                key, resourceId, removedBy);
        }

        await Task.CompletedTask;
    }

    public async Task<IEnumerable<Domain.Resource>> GetResourcesByMetadataAsync(string key, object value)
    {
        var matchingResourceIds = _resourceMetadata
            .Where(kvp => kvp.Value.ContainsKey(key) && Equals(kvp.Value[key], value))
            .Select(kvp => kvp.Key)
            .ToList();

        var resources = await _dbContext.Resources
            .Where(r => matchingResourceIds.Contains(r.Id))
            .ToListAsync();

        return resources.Select(ConvertToDomainResource);
    }

    public async Task<bool> HasResourceMetadataAsync(int resourceId, string key)
    {
        return await Task.FromResult(
            _resourceMetadata.ContainsKey(resourceId) && 
            _resourceMetadata[resourceId].ContainsKey(key));
    }

    #endregion

    #region Bulk Operations

    public async Task<IEnumerable<Domain.Resource>> CreateResourcesBulkAsync(
        IEnumerable<(string Uri, string Description, string Type)> resources, string createdBy)
    {
        var createdResources = new List<Domain.Resource>();

        foreach (var (uri, description, type) in resources)
        {
            var resource = await CreateResourceAsync(uri, description, type, createdBy);
            createdResources.Add(resource);
        }

        _logger.LogInformation("Created {Count} resources in bulk by {CreatedBy}",
            createdResources.Count, createdBy);

        return createdResources;
    }

    public async Task DeleteResourcesBulkAsync(IEnumerable<int> resourceIds, string deletedBy)
    {
        foreach (var resourceId in resourceIds)
        {
            await DeleteResourceAsync(resourceId, deletedBy);
        }

        _logger.LogInformation("Deleted {Count} resources in bulk by {DeletedBy}",
            resourceIds.Count(), deletedBy);
    }

    public async Task UpdateResourceTypeBulkAsync(IEnumerable<int> resourceIds, string newType, string updatedBy)
    {
        var resources = await _dbContext.Resources
            .Where(r => resourceIds.Contains(r.Id))
            .ToListAsync();

        foreach (var resource in resources)
        {
            resource.ResourceType = newType;
            resource.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        await LogAuditAsync("UpdateResourceTypeBulk", "Resource", 0, updatedBy,
            $"Updated type to '{newType}' for {resources.Count} resources");

        _logger.LogInformation("Updated type to '{NewType}' for {Count} resources by {UpdatedBy}",
            newType, resources.Count, updatedBy);
    }

    public async Task SetPermissionsBulkAsync(int resourceId, 
        IEnumerable<(int EntityId, Domain.HttpVerb Verb, bool Grant)> permissions, string setBy)
    {
        foreach (var (entityId, verb, grant) in permissions)
        {
            await SetResourcePermissionAsync(resourceId, entityId, verb, grant, setBy);
        }

        _logger.LogInformation("Set {Count} permissions for resource {ResourceId} in bulk by {SetBy}",
            permissions.Count(), resourceId, setBy);
    }

    #endregion

    #region Search and Filtering

    public async Task<IEnumerable<Domain.Resource>> SearchResourcesAsync(string searchTerm)
    {
        var resources = await _dbContext.Resources
            .Where(r => r.Uri.Contains(searchTerm) ||
                       (r.Description != null && r.Description.Contains(searchTerm)) ||
                       (r.ResourceType != null && r.ResourceType.Contains(searchTerm)))
            .ToListAsync();

        return resources.Select(ConvertToDomainResource);
    }

    public async Task<IEnumerable<Domain.Resource>> GetResourcesByPermissionAsync(int entityId, Domain.HttpVerb verb)
    {
        var schemeType = await _dbContext.SchemeTypes
            .FirstOrDefaultAsync(st => st.SchemeName == "ApiUriAuthorization");

        if (schemeType == null)
            return Enumerable.Empty<Domain.Resource>();

        var resourceIds = await _dbContext.UriAccesses
            .Include(ua => ua.VerbType)
            .Include(ua => ua.PermissionScheme)
            .Where(ua => ua.PermissionScheme.EntityId == entityId &&
                        ua.VerbType.VerbName == verb.ToString() &&
                        ua.Grant)
            .Select(ua => ua.ResourceId)
            .Distinct()
            .ToListAsync();

        var resources = await _dbContext.Resources
            .Where(r => resourceIds.Contains(r.Id))
            .ToListAsync();

        return resources.Select(ConvertToDomainResource);
    }

    public async Task<IEnumerable<Domain.Resource>> GetUnprotectedResourcesAsync()
    {
        var protectedResourceIds = await _dbContext.UriAccesses
            .Select(ua => ua.ResourceId)
            .Distinct()
            .ToListAsync();

        var unprotectedResources = await _dbContext.Resources
            .Where(r => !protectedResourceIds.Contains(r.Id))
            .ToListAsync();

        return unprotectedResources.Select(ConvertToDomainResource);
    }

    public async Task<IEnumerable<Domain.Resource>> GetResourcesModifiedAfterAsync(DateTime timestamp)
    {
        var resources = await _dbContext.Resources
            .Where(r => r.UpdatedAt > timestamp)
            .OrderBy(r => r.UpdatedAt)
            .ToListAsync();

        return resources.Select(ConvertToDomainResource);
    }

    public async Task<IEnumerable<Domain.Resource>> GetResourcesPaginatedAsync(int page, int pageSize, string? filter = null)
    {
        var query = _dbContext.Resources.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            query = query.Where(r => r.Uri.Contains(filter) ||
                                    (r.Description != null && r.Description.Contains(filter)));
        }

        var resources = await query
            .OrderBy(r => r.Uri)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return resources.Select(ConvertToDomainResource);
    }

    #endregion

    #region Statistics and Analytics

    public async Task<int> GetTotalResourceCountAsync()
    {
        return await _dbContext.Resources.CountAsync();
    }

    public async Task<Dictionary<string, int>> GetResourceCountByTypeAsync()
    {
        var counts = await _dbContext.Resources
            .GroupBy(r => r.ResourceType ?? "Unknown")
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count);

        return counts;
    }

    public async Task<Dictionary<Domain.HttpVerb, int>> GetPermissionCountByVerbAsync(int resourceId)
    {
        var counts = new Dictionary<Domain.HttpVerb, int>();

        var permissions = await _dbContext.UriAccesses
            .Include(ua => ua.VerbType)
            .Where(ua => ua.ResourceId == resourceId)
            .GroupBy(ua => ua.VerbType.VerbName)
            .Select(g => new { Verb = g.Key, Count = g.Count() })
            .ToListAsync();

        foreach (var p in permissions)
        {
            if (Enum.TryParse<Domain.HttpVerb>(p.Verb, out var verb))
            {
                counts[verb] = p.Count;
            }
        }

        return counts;
    }

    public async Task<IEnumerable<(Domain.Resource Resource, int AccessCount)>> GetMostAccessedResourcesAsync(int topN)
    {
        // This would typically be based on access logs
        // For now, return resources with most permissions as a proxy
        var resourceAccess = await _dbContext.UriAccesses
            .GroupBy(ua => ua.ResourceId)
            .Select(g => new { ResourceId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(topN)
            .ToListAsync();

        var result = new List<(Domain.Resource, int)>();
        foreach (var ra in resourceAccess)
        {
            var resource = await GetResourceByIdAsync(ra.ResourceId);
            if (resource != null)
            {
                result.Add((resource, ra.Count));
            }
        }

        return result;
    }

    public async Task<IEnumerable<(Domain.Resource Resource, int PermissionCount)>> GetMostProtectedResourcesAsync(int topN)
    {
        var resourcePermissions = await _dbContext.UriAccesses
            .GroupBy(ua => ua.ResourceId)
            .Select(g => new { ResourceId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(topN)
            .ToListAsync();

        var result = new List<(Domain.Resource, int)>();
        foreach (var rp in resourcePermissions)
        {
            var resource = await GetResourceByIdAsync(rp.ResourceId);
            if (resource != null)
            {
                result.Add((resource, rp.Count));
            }
        }

        return result;
    }

    #endregion

    #region Maintenance and Validation

    public async Task<IEnumerable<Domain.Resource>> ValidateAllResourcePatternsAsync()
    {
        var invalidResources = new List<Data.Models.Resource>();
        var allResources = await _dbContext.Resources.ToListAsync();

        foreach (var resource in allResources)
        {
            if (!await ValidateUriPatternAsync(resource.Uri))
            {
                invalidResources.Add(resource);
            }
        }

        _logger.LogWarning("Found {Count} resources with invalid URI patterns", invalidResources.Count);
        return invalidResources.Select(ConvertToDomainResource);
    }

    public async Task<IEnumerable<Domain.Resource>> FindDuplicateResourcesAsync()
    {
        var duplicates = await _dbContext.Resources
            .GroupBy(r => r.Uri.ToLower())
            .Where(g => g.Count() > 1)
            .SelectMany(g => g)
            .ToListAsync();

        _logger.LogWarning("Found {Count} duplicate resources", duplicates.Count);
        return duplicates.Select(ConvertToDomainResource);
    }

    public async Task<IEnumerable<Domain.Resource>> FindOrphanedResourcesAsync()
    {
        // Find resources with no associated permissions
        var resourcesWithPermissions = await _dbContext.UriAccesses
            .Select(ua => ua.ResourceId)
            .Distinct()
            .ToListAsync();

        var orphanedResources = await _dbContext.Resources
            .Where(r => !resourcesWithPermissions.Contains(r.Id))
            .ToListAsync();

        _logger.LogInformation("Found {Count} orphaned resources without permissions", orphanedResources.Count);
        return orphanedResources.Select(ConvertToDomainResource);
    }

    public async Task CleanupUnusedResourcesAsync(string cleanedBy)
    {
        var orphanedResources = await FindOrphanedResourcesAsync();
        var resourceIds = orphanedResources.Select(r => r.Id).ToList();

        if (resourceIds.Any())
        {
            await DeleteResourcesBulkAsync(resourceIds, cleanedBy);

            await LogAuditAsync("CleanupUnusedResources", "Resource", 0, cleanedBy,
                $"Cleaned up {resourceIds.Count} unused resources");

            _logger.LogInformation("Cleaned up {Count} unused resources by {CleanedBy}",
                resourceIds.Count, cleanedBy);
        }
    }

    public async Task RebuildResourceIndexAsync()
    {
        // This would typically rebuild any custom indexes or caches
        // For now, just log the operation
        _logger.LogInformation("Rebuilding resource index");

        // Clear and rebuild any in-memory caches
        _resourceMetadata.Clear();

        // Could trigger database index rebuild here if needed
        await Task.CompletedTask;

        _logger.LogInformation("Resource index rebuilt successfully");
    }

    #endregion

    #region Helper Methods

    private Domain.Resource ConvertToDomainResource(Data.Models.Resource dataResource)
    {
        return new Domain.Resource
        {
            Id = dataResource.Id,
            Uri = dataResource.Uri,
            Description = dataResource.Description,
            ResourceType = dataResource.ResourceType,
            Version = dataResource.Version,
            ParentResourceId = dataResource.ParentResourceId,
            IsActive = dataResource.IsActive,
            CreatedAt = dataResource.CreatedAt,
            UpdatedAt = dataResource.UpdatedAt
        };
    }

    private string ConvertWildcardToRegex(string wildcardPattern)
    {
        // Escape special regex characters except * and {}
        var pattern = Regex.Escape(wildcardPattern);
        
        // Unescape the curly braces for parameters
        pattern = pattern.Replace("\\{", "{").Replace("\\}", "}");
        
        // Replace wildcards with regex equivalents
        pattern = pattern.Replace("\\*", ".*");
        
        // Replace parameter placeholders with regex groups
        pattern = Regex.Replace(pattern, @"\{([^}]+)\}", "(?<$1>[^/]+)");
        
        return "^" + pattern + "$";
    }

    private int CalculateSpecificity(string pattern)
    {
        // Higher score = more specific
        var score = 100;
        
        // Deduct points for wildcards
        score -= pattern.Count(c => c == '*') * 10;
        
        // Deduct points for parameters
        score -= Regex.Matches(pattern, @"\{[^}]+\}").Count * 5;
        
        // Add points for path segments
        score += pattern.Count(c => c == '/') * 2;
        
        return score;
    }

    private async Task<List<Data.Models.Resource>> GetChildResourcesRecursiveAsync(string parentUri)
    {
        var children = await _dbContext.Resources
            .Where(r => r.Uri.StartsWith(parentUri + "/"))
            .ToListAsync();

        var allChildren = new List<Data.Models.Resource>(children);
        
        foreach (var child in children)
        {
            var grandChildren = await GetChildResourcesRecursiveAsync(child.Uri);
            allChildren.AddRange(grandChildren);
        }

        return allChildren.Distinct().ToList();
    }

    private async Task LogAuditAsync(string action, string entityType, int entityId,
        string changedBy, string changeDetails)
    {
        var auditLog = new AuditLog
        {
            EntityType = entityType,
            EntityId = entityId,
            ChangeType = action,
            ChangedBy = changedBy,
            ChangeDate = DateTime.UtcNow,
            ChangeDetails = changeDetails
        };

        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync();
    }
    
    private async Task<bool> HasActiveDependenciesAsync(int resourceId)
    {
        try
        {
            // Check if resource has URI access permissions
            var hasUriAccesses = await _dbContext.UriAccesses
                .AnyAsync(ua => ua.ResourceId == resourceId);
            
            return hasUriAccesses;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking dependencies for resource {ResourceId}", resourceId);
            // Return true to be safe and prevent deletion if we can't verify
            return true;
        }
    }

    #endregion

    public Task<object> TestUriPatternMatchAsync(string pattern, List<string> testUris)
    {
        var results = new List<object>();
        int matchCount = 0;

        foreach (var testUri in testUris)
        {
            try
            {
                // Simple pattern matching - can be enhanced with regex or advanced patterns
                bool matches = testUri.Contains(pattern.Replace("*", "")) ||
                              Regex.IsMatch(testUri, pattern.Replace("*", ".*"));

                results.Add(new
                {
                    Uri = testUri,
                    Matches = matches,
                    Pattern = pattern
                });

                if (matches) matchCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error testing pattern {Pattern} against URI {Uri}", pattern, testUri);
                results.Add(new
                {
                    Uri = testUri,
                    Matches = false,
                    Pattern = pattern,
                    Error = ex.Message
                });
            }
        }

        var result = new
        {
            Pattern = pattern,
            TestResults = results,
            MatchCount = matchCount,
            TotalTests = testUris.Count
        };

        return Task.FromResult<object>(result);
    }

    #region Handler-Compatible Request/Response Methods

    public async Task<Responses.CreateResourceResponse> CreateAsync(Requests.CreateResourceRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.UriPattern))
            {
                return new Responses.CreateResourceResponse
                {
                    Success = false,
                    Message = "URI pattern is required",
                    Errors = new[] { "URI pattern cannot be empty" }
                };
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return new Responses.CreateResourceResponse
                {
                    Success = false,
                    Message = "Resource name is required",
                    Errors = new[] { "Resource name cannot be empty" }
                };
            }

            // Create the resource using the existing domain method
            var resource = await CreateResourceAsync(
                uri: request.UriPattern,
                description: request.Description ?? string.Empty,
                resourceType: request.Name,
                createdBy: request.CreatedBy
            );

            _logger.LogInformation("Created resource {ResourceId} via request/response pattern by {CreatedBy}",
                resource.Id, request.CreatedBy);

            return new Responses.CreateResourceResponse
            {
                Resource = resource,
                Success = true,
                Message = "Resource created successfully"
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Validation error creating resource via request/response pattern");
            return new Responses.CreateResourceResponse
            {
                Success = false,
                Message = "Validation error",
                Errors = new[] { ex.Message }
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Operation error creating resource via request/response pattern");
            return new Responses.CreateResourceResponse
            {
                Success = false,
                Message = "Operation error",
                Errors = new[] { ex.Message }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating resource via request/response pattern");
            return new Responses.CreateResourceResponse
            {
                Success = false,
                Message = "Error creating resource",
                Errors = new[] { ex.Message }
            };
        }
    }

    public async Task<Responses.UpdateResourceResponse> UpdateAsync(Requests.UpdateResourceRequest request)
    {
        try
        {
            if (request.ResourceId <= 0)
            {
                return new Responses.UpdateResourceResponse
                {
                    Success = false,
                    Message = "Invalid resource ID",
                    Errors = new[] { "Resource ID must be a positive integer" }
                };
            }

            if (string.IsNullOrWhiteSpace(request.UriPattern))
            {
                return new Responses.UpdateResourceResponse
                {
                    Success = false,
                    Message = "URI pattern is required",
                    Errors = new[] { "URI pattern cannot be empty" }
                };
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return new Responses.UpdateResourceResponse
                {
                    Success = false,
                    Message = "Resource name is required",
                    Errors = new[] { "Resource name cannot be empty" }
                };
            }

            // Update the resource using the existing domain method
            var resource = await UpdateResourceAsync(
                resourceId: request.ResourceId,
                uri: request.UriPattern,
                description: request.Description ?? string.Empty,
                resourceType: request.Name,
                updatedBy: request.UpdatedBy
            );

            _logger.LogInformation("Updated resource {ResourceId} via request/response pattern by {UpdatedBy}",
                request.ResourceId, request.UpdatedBy);

            return new Responses.UpdateResourceResponse
            {
                Resource = resource,
                Success = true,
                Message = "Resource updated successfully"
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Validation error updating resource {ResourceId} via request/response pattern",
                request.ResourceId);
            return new Responses.UpdateResourceResponse
            {
                Success = false,
                Message = "Validation error",
                Errors = new[] { ex.Message }
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Operation error updating resource {ResourceId} via request/response pattern",
                request.ResourceId);
            return new Responses.UpdateResourceResponse
            {
                Success = false,
                Message = "Operation error",
                Errors = new[] { ex.Message }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating resource {ResourceId} via request/response pattern",
                request.ResourceId);
            return new Responses.UpdateResourceResponse
            {
                Success = false,
                Message = "Error updating resource",
                Errors = new[] { ex.Message }
            };
        }
    }

    public async Task<Responses.GetResourceResponse> GetByIdAsync(Requests.GetResourceRequest request)
    {
        try
        {
            if (request.ResourceId <= 0)
            {
                return new Responses.GetResourceResponse
                {
                    Success = false,
                    Message = "Invalid resource ID",
                    Errors = new[] { "Resource ID must be a positive integer" }
                };
            }

            // Get the resource using the existing domain method
            var resource = await GetResourceByIdAsync(request.ResourceId);

            if (resource == null)
            {
                return new Responses.GetResourceResponse
                {
                    Success = false,
                    Message = "Resource not found",
                    Errors = new[] { $"Resource {request.ResourceId} not found" }
                };
            }

            _logger.LogInformation("Retrieved resource {ResourceId} via request/response pattern by {RequestedBy}",
                request.ResourceId, request.RequestedBy);

            return new Responses.GetResourceResponse
            {
                Resource = resource,
                Success = true,
                Message = "Resource retrieved successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving resource {ResourceId} via request/response pattern",
                request.ResourceId);
            return new Responses.GetResourceResponse
            {
                Success = false,
                Message = "Error retrieving resource",
                Errors = new[] { ex.Message }
            };
        }
    }

    public async Task<Responses.GetResourcesResponse> GetAllAsync(Requests.GetResourcesRequest request)
    {
        try
        {
            IEnumerable<Domain.Resource> resources;

            // Apply filters based on the request
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                resources = await SearchResourcesAsync(request.Search);
            }
            else if (!string.IsNullOrWhiteSpace(request.ResourceType))
            {
                resources = await GetResourcesByTypeAsync(request.ResourceType);
            }
            else if (!string.IsNullOrWhiteSpace(request.UriPatternFilter))
            {
                resources = await GetResourcesByPatternAsync(request.UriPatternFilter);
            }
            else
            {
                resources = await GetAllResourcesAsync();
            }

            // Filter by active status if requested
            if (request.ActiveOnly.HasValue && request.ActiveOnly.Value)
            {
                resources = resources.Where(r => r.IsActive);
            }

            // Get total count before pagination
            var totalCount = resources.Count();

            // Apply sorting
            if (!string.IsNullOrWhiteSpace(request.SortBy))
            {
                resources = request.SortBy.ToLower() switch
                {
                    "uri" => request.SortDescending
                        ? resources.OrderByDescending(r => r.Uri)
                        : resources.OrderBy(r => r.Uri),
                    "type" => request.SortDescending
                        ? resources.OrderByDescending(r => r.ResourceType)
                        : resources.OrderBy(r => r.ResourceType),
                    "created" => request.SortDescending
                        ? resources.OrderByDescending(r => r.CreatedAt)
                        : resources.OrderBy(r => r.CreatedAt),
                    "updated" => request.SortDescending
                        ? resources.OrderByDescending(r => r.UpdatedAt)
                        : resources.OrderBy(r => r.UpdatedAt),
                    _ => resources.OrderBy(r => r.Uri)
                };
            }

            // Apply pagination
            var paginatedResources = resources
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            _logger.LogInformation("Retrieved {Count} resources (page {Page} of {PageSize}) via request/response pattern by {RequestedBy}",
                paginatedResources.Count, request.Page, request.PageSize, request.RequestedBy);

            return new Responses.GetResourcesResponse
            {
                Resources = paginatedResources,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize,
                Success = true,
                Message = "Resources retrieved successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving resources via request/response pattern");
            return new Responses.GetResourcesResponse
            {
                Success = false,
                Message = "Error retrieving resources",
                Errors = new[] { ex.Message }
            };
        }
    }

    public async Task<Responses.DeleteResourceResponse> DeleteAsync(Requests.DeleteResourceRequest request)
    {
        try
        {
            if (request.ResourceId <= 0)
            {
                return new Responses.DeleteResourceResponse
                {
                    Success = false,
                    Message = "Invalid resource ID",
                    Errors = new[] { "Resource ID must be a positive integer" }
                };
            }

            // Check dependencies unless force delete is requested
            if (!request.ForceDelete)
            {
                var dependencyCheck = await CheckDependenciesAsync(request.ResourceId);
                if (!dependencyCheck.CanDelete)
                {
                    var errorMessages = new List<string> { "Cannot delete resource due to dependencies" };
                    errorMessages.AddRange(dependencyCheck.Dependencies.Select(d =>
                        $"{d.DependencyType}: {d.EntityName} ({d.EntityType})"));

                    return new Responses.DeleteResourceResponse
                    {
                        Success = false,
                        Message = "Resource has active dependencies",
                        Errors = errorMessages
                    };
                }
            }

            // Delete the resource using the existing domain method
            await DeleteResourceAsync(request.ResourceId, request.DeletedBy);

            _logger.LogInformation("Deleted resource {ResourceId} via request/response pattern by {DeletedBy}",
                request.ResourceId, request.DeletedBy);

            return new Responses.DeleteResourceResponse
            {
                Success = true,
                Message = "Resource deleted successfully"
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Validation error deleting resource {ResourceId} via request/response pattern",
                request.ResourceId);
            return new Responses.DeleteResourceResponse
            {
                Success = false,
                Message = "Validation error",
                Errors = new[] { ex.Message }
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Operation error deleting resource {ResourceId} via request/response pattern",
                request.ResourceId);
            return new Responses.DeleteResourceResponse
            {
                Success = false,
                Message = "Operation error",
                Errors = new[] { ex.Message }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting resource {ResourceId} via request/response pattern",
                request.ResourceId);
            return new Responses.DeleteResourceResponse
            {
                Success = false,
                Message = "Error deleting resource",
                Errors = new[] { ex.Message }
            };
        }
    }

    public async Task<Domain.DependencyCheckResult> CheckDependenciesAsync(int resourceId)
    {
        try
        {
            var result = new Domain.DependencyCheckResult
            {
                CanDelete = true,
                Dependencies = new List<Domain.Dependency>(),
                Warnings = new List<string>(),
                Messages = new List<string>()
            };

            // Check if resource exists
            var resource = await _dbContext.Resources.FindAsync(resourceId);
            if (resource == null)
            {
                result.CanDelete = false;
                result.Messages.Add($"Resource {resourceId} not found");
                return result;
            }

            // Check for URI access permissions (dependencies)
            var uriAccesses = await _dbContext.UriAccesses
                .Include(ua => ua.PermissionScheme)
                    .ThenInclude(ps => ps.Entity)
                .Where(ua => ua.ResourceId == resourceId)
                .ToListAsync();

            if (uriAccesses.Any())
            {
                result.CanDelete = false;
                result.Messages.Add($"Resource '{resource.Uri}' has {uriAccesses.Count} active permission(s)");

                foreach (var uriAccess in uriAccesses)
                {
                    var entity = uriAccess.PermissionScheme.Entity;
                    result.Dependencies.Add(new Domain.Dependency
                    {
                        EntityId = entity.Id,
                        EntityName = entity.EntityType,
                        EntityType = entity.EntityType,
                        DependencyType = "URI Access Permission",
                        Description = $"Permission scheme {uriAccess.PermissionSchemeId} references this resource"
                    });
                }
            }

            // Check for child resources
            var childResources = await _dbContext.Resources
                .Where(r => r.ParentResourceId == resourceId)
                .ToListAsync();

            if (childResources.Any())
            {
                result.Warnings.Add($"Resource has {childResources.Count} child resource(s) that may be affected");
                foreach (var child in childResources)
                {
                    result.Dependencies.Add(new Domain.Dependency
                    {
                        EntityId = child.Id,
                        EntityName = child.Uri,
                        EntityType = "Resource",
                        DependencyType = "Child Resource",
                        Description = $"Child resource with URI '{child.Uri}'"
                    });
                }
            }

            // If no blocking dependencies found
            if (result.CanDelete)
            {
                result.Messages.Add($"Resource '{resource.Uri}' can be safely deleted");
            }

            _logger.LogInformation("Dependency check for resource {ResourceId}: CanDelete={CanDelete}, Dependencies={DependencyCount}",
                resourceId, result.CanDelete, result.Dependencies.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking dependencies for resource {ResourceId}", resourceId);
            return new Domain.DependencyCheckResult
            {
                CanDelete = false,
                Messages = new List<string> { $"Error checking dependencies: {ex.Message}" }
            };
        }
    }

    #endregion
}