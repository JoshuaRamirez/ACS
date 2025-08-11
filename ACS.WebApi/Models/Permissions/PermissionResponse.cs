using ACS.WebResources.Permissions;

namespace ACS.WebApi.Models.Permissions;

public class PermissionResponse
{
    public required PermissionResource Permission { get; init; }
}
