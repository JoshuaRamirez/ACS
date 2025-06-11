using ACS.Service.Data.Models;
using ACS.Service.Domain;

namespace ACS.Service.Delegates.Normalizers
{
    internal static class CreateResourceNormalizer
    {
        public static List<Resource>? Resources { get; set; }
        public static Resource Execute(Permission permission)
        {
            var resource = new Resource
            {
                Uri = permission.Uri
            };

            Resources?.Add(resource);

            return resource;
        }
    }
}
