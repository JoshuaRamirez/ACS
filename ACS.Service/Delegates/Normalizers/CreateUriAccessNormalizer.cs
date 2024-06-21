using ACS.Service.Data.Models;
using ACS.Service.Domain;

namespace ACS.Service.Delegates.Normalizers
{
    internal static class CreateUriAccessNormalizer
    {
        public static List<VerbType> VerbTypes { get; set; }
        public static UriAccess Execute(PermissionScheme permissionScheme, Resource resource, Permission permission)
        {
            var uriAccess = new UriAccess();
            uriAccess.Deny = permission.Deny;
            uriAccess.PermissionScheme = permissionScheme;
            uriAccess.Grant = permission.Grant;
            uriAccess.Resource = resource;
            uriAccess.VerbType = VerbTypes.Single(x => x.VerbName == permission.HttpVerb.ToString()); //TODO: Map the enum correctly.
            return uriAccess;
        }
    }
}
