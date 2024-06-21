using ACS.Service.Data.Models;
using ACS.Service.Domain;

namespace ACS.Service.Delegates.Normalizers
{
    internal static class CreateResourceNormalizer
    {
        public static Resource Execute(Permission permission)
        {
            var resource = new Resource();
            resource.Uri = permission.Uri;
            var uriAccess = new UriAccess();
            uriAccess.Resource = resource;
            return resource;
        }
    }
}
