using ACS.Service.Data.Models;
using ACS.Service.Domain;
using Entity = ACS.Service.Data.Models.Entity;

namespace ACS.Service.Delegates.Normalizers
{
    internal static class AddPermissionToEntity
    {
        public static List<PermissionScheme> PermissionSchemes { get; set; }
        public static List<Entity> Entities { get; set; }
        public static List<Resource> Resources { get; set; }
        public static List<UriAccess> UriAccessList { get; set; }
        public static List<SchemeType> SchemeTypes { get; set; }
        public static void Execute(Permission permission, int entityId)
        {
            var schemeType = SchemeTypes.Single(x => x.SchemeName == permission.Scheme.ToString());
            var entity = Entities.Single(x => x.Id == entityId);
            var resource = Resources.SingleOrDefault(x => x.Uri == permission.Uri);
            if (resource == null)
            {
                resource = CreateResourceNormalizer.Execute(permission);
            }
            var permissionScheme = PermissionSchemes.SingleOrDefault(x => x.EntityId == entityId && x.SchemeType == schemeType);
            if (permissionScheme == null)
            {
                permissionScheme = CreatePermissionSchemeNormalizer.Execute(entityId);
            }
            var uriAccess = UriAccessList.SingleOrDefault(x => x.EntityPermissionId == entityId);
            if (uriAccess != null)
            {
                throw new InvalidOperationException("The Permission is already added somehow in some way so yea.");
            }
            uriAccess = CreateUriAccessNormalizer.Execute(permissionScheme, resource, permission);
            entity.EntityPermissions.Add(permissionScheme);
        }
    }
}
