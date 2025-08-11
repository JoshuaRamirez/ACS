using System;
using ACS.Service.Domain;
using ACS.WebApi.Models.Permissions;
using ACS.WebResources.Permissions;

namespace ACS.WebApi.Mapping;

internal static class PermissionMappings
{
    public static Permission ToDomain(this CreatePermissionRequest request)
    {
        var permission = new Permission
        {
            Uri = request.Permission.Uri,
            HttpVerb = Enum.Parse<HttpVerb>(request.Permission.HttpVerb, true),
            Grant = request.Permission.Grant,
            Deny = request.Permission.Deny,
        };

        return permission;
    }

    public static PermissionResource ToResource(this Permission permission)
    {
        return new PermissionResource
        {
            Id = permission.Id,
            Uri = permission.Uri,
            HttpVerb = permission.HttpVerb.ToString(),
            Grant = permission.Grant,
            Deny = permission.Deny
        };
    }
}
