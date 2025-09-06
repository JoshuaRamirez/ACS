using ACS.VerticalHost.Services;

namespace ACS.VerticalHost.Commands;

// Resource Commands
public class CreateResourceCommand : ICommand<ACS.Service.Domain.Resource>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string UriPattern { get; set; } = string.Empty;
    public List<string> HttpVerbs { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public string? CreatedBy { get; set; }
}

public class UpdateResourceCommand : ICommand<ACS.Service.Domain.Resource>
{
    public int ResourceId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? UriPattern { get; set; }
    public List<string>? HttpVerbs { get; set; }
    public bool? IsActive { get; set; }
    public string? UpdatedBy { get; set; }
}

public class DeleteResourceCommand : ICommand<DeleteResourceResult>
{
    public int ResourceId { get; set; }
    public bool ForceDelete { get; set; }
    public string? DeletedBy { get; set; }
}

// Resource Queries
public class GetResourceQuery : IQuery<ACS.Service.Domain.Resource>
{
    public int ResourceId { get; set; }
    public bool IncludePermissions { get; set; }
    public bool IncludeUsage { get; set; }
}

public class GetResourcesQuery : IQuery<List<ACS.Service.Domain.Resource>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? UriPatternFilter { get; set; }
    public List<string>? HttpVerbFilter { get; set; }
    public bool? ActiveOnly { get; set; } = true;
    public bool IncludePermissions { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
}

public class GetResourcePermissionsQuery : IQuery<List<ResourcePermissionInfo>>
{
    public int ResourceId { get; set; }
    public bool IncludeInherited { get; set; } = true;
    public bool IncludeEffective { get; set; } = true;
    public string? EntityType { get; set; } // "User", "Group", "Role" or null for all
}

// Resource-specific result types
public class DeleteResourceResult
{
    public bool Success { get; set; } = true;
    public int ResourceId { get; set; }
    public DateTime DeletedAt { get; set; } = DateTime.UtcNow;
    public string? Message { get; set; }
    public List<string> DependenciesRemoved { get; set; } = new();
}

public class ResourcePermissionInfo
{
    public int PermissionId { get; set; }
    public string PermissionName { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty; // User, Group, Role
    public bool IsInherited { get; set; }
    public string? InheritedFrom { get; set; }
    public DateTime GrantedAt { get; set; }
    public string? GrantedBy { get; set; }
}