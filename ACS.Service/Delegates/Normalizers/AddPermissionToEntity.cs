using ACS.Service.Data.Models;
using ACS.Service.Domain;
using Entity = ACS.Service.Data.Models.Entity;

namespace ACS.Service.Delegates.Normalizers
{
    internal static class AddPermissionToEntity
    {
        public static List<PermissionScheme> PermissionSchemes { get; set; } = null!;
        public static List<Entity> Entities { get; set; } = null!;
        public static List<Resource> Resources { get; set; } = null!;
        public static List<UriAccess> UriAccessList { get; set; } = null!;
        public static List<SchemeType> SchemeTypes { get; set; } = null!;
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
                permissionScheme.SchemeType = schemeType;
                entity.EntityPermissions.Add(permissionScheme);
            }
            else if (!entity.EntityPermissions.Contains(permissionScheme))
            {
                entity.EntityPermissions.Add(permissionScheme);
            }

            var existing = UriAccessList.SingleOrDefault(x =>
                x.PermissionScheme == permissionScheme &&
                x.Resource == resource &&
                x.VerbType.VerbName == permission.HttpVerb.ToString());

            if (existing != null)
            {
                throw new InvalidOperationException("Permission already exists for this entity.");
            }

            CreateUriAccessNormalizer.Execute(permissionScheme, resource, permission);
        }
    }
}
