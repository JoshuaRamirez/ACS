using ACS.Service.Data.Models;
using ACS.Service.Domain;

namespace ACS.Service.Delegates.Normalizers
{
    internal static class CreatePermissionSchemeNormalizer
    {
        public static List<PermissionScheme> PermissionSchemes { get; set; } = null!;
        public static PermissionScheme Execute(int entityId)
        {
            var entityPermission = new PermissionScheme();
            entityPermission.EntityId = entityId;
            PermissionSchemes.Add(entityPermission);
            return entityPermission;
        }
    }
}
