using ACS.Service.Data.Models;
using ACS.Service.Domain;

namespace ACS.Service.Delegates.Normalizers
{
    internal static class CreateUriAccessNormalizer
    {
        public static List<VerbType> VerbTypes { get; set; }
        public static List<UriAccess>? UriAccessList { get; set; }
        public static UriAccess Execute(PermissionScheme permissionScheme, Resource resource, Permission permission)
        {
            var uriAccess = new UriAccess
            {
                Deny = permission.Deny,
                PermissionScheme = permissionScheme,
                Grant = permission.Grant,
                Resource = resource,
                VerbType = VerbTypes.Single(x => x.VerbName == permission.HttpVerb.ToString()) //TODO: Map the enum correctly.
            };

            UriAccessList?.Add(uriAccess);

            return uriAccess;
        }
    }
}
