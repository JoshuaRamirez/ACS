using System;
using ACS.Service.Data.Models;
using ACS.Service.Domain;

namespace ACS.Service.Delegates.Normalizers
{
    internal static class CreateUriAccessNormalizer
    {
        public static List<VerbType> VerbTypes { get; set; } = null!;
        public static List<UriAccess>? UriAccessList { get; set; }
        public static UriAccess Execute(PermissionScheme permissionScheme, Resource resource, Permission permission)
        {
            var verb = permission.HttpVerb.ToString();
            var verbType = VerbTypes.Single(x => string.Equals(x.VerbName, verb, StringComparison.OrdinalIgnoreCase));

            var uriAccess = new UriAccess
            {
                Deny = permission.Deny,
                PermissionScheme = permissionScheme,
                Grant = permission.Grant,
                Resource = resource,
                VerbType = verbType
            };

            UriAccessList?.Add(uriAccess);

            return uriAccess;
        }
    }
}
