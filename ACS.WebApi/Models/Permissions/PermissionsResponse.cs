using ACS.WebResources.Permissions;

namespace ACS.WebApi.Models.Permissions;

public class PermissionsResponse
{
    public IReadOnlyCollection<PermissionResource> Permissions { get; init; } = Array.Empty<PermissionResource>();
}
