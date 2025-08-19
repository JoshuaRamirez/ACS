using ACS.Service.Data;
using ACS.Service.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ACS.Service.Delegates.Normalizers
{
    internal static class CreateResourceNormalizer
    {
        public static async Task<Resource> ExecuteAsync(ApplicationDbContext dbContext, string uri)
        {
            // Check if resource already exists
            var existingResource = await dbContext.Resources
                .FirstOrDefaultAsync(r => r.Uri == uri);
            
            if (existingResource != null)
            {
                return existingResource; // Return existing resource
            }

            // Create new resource
            var resource = new Resource
            {
                Uri = uri
            };

            dbContext.Resources.Add(resource);
            await dbContext.SaveChangesAsync();

            return resource;
        }
        
        // Legacy method for compatibility - remove after all callers are updated
        public static Resource Execute(Domain.Permission permission)
        {
            throw new NotSupportedException("Legacy normalizer method is no longer supported. Use ExecuteAsync instead.");
        }
    }
}
