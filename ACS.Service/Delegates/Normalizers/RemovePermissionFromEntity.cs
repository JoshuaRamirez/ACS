using ACS.Service.Data.Models;
using ACS.Service.Domain;
using Entity = ACS.Service.Data.Models.Entity;

namespace ACS.Service.Delegates.Normalizers
{
    internal static class RemovePermissionFromEntity
    {
        public static List<PermissionScheme> PermissionSchemes { get; set; }
        public static List<Entity> Entities { get; set; }
        public static List<Resource> Resources { get; set; }
        public static List<UriAccess> UriAccessList { get; set; }
        public static List<SchemeType> SchemeTypes { get; set; }

        public static void Execute(Permission permission, int entityId)
        {
            var schemeType = SchemeTypes.SingleOrDefault(x => x.SchemeName == permission.Scheme.ToString());
            if (schemeType == null)
            {
                return;
            }

            var entity = Entities.SingleOrDefault(x => x.Id == entityId);
            if (entity == null)
            {
                return;
            }

            var resource = Resources.SingleOrDefault(x => x.Uri == permission.Uri);
            if (resource == null)
            {
                return;
            }

            var permissionScheme = PermissionSchemes.SingleOrDefault(x => x.EntityId == entityId && x.SchemeType == schemeType);
            if (permissionScheme == null)
            {
                return;
            }

            var uriAccess = UriAccessList.SingleOrDefault(x =>
                x.PermissionScheme == permissionScheme &&
                x.Resource == resource &&
                x.VerbType.VerbName == permission.HttpVerb.ToString());

            if (uriAccess == null)
            {
                return;
            }

            UriAccessList.Remove(uriAccess);

            if (!UriAccessList.Any(x => x.PermissionScheme == permissionScheme))
            {
                entity.EntityPermissions.Remove(permissionScheme);
                PermissionSchemes.Remove(permissionScheme);
            }
        }
    }
}
