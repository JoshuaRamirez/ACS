using ACS.Service.Data;
using ACS.Service.Data.Models;
using ACS.Service.Domain;
using Microsoft.EntityFrameworkCore;

namespace ACS.Service.Delegates.Normalizers
{
    internal static class AddPermissionToEntity
    {
        public static async Task ExecuteAsync(ApplicationDbContext dbContext, Permission permission, int entityId)
        {
            // Find the scheme type
            var schemeType = await dbContext.SchemeTypes
                .FirstOrDefaultAsync(st => st.SchemeName == permission.Scheme.ToString());
            
            if (schemeType == null)
            {
                throw new InvalidOperationException($"SchemeType '{permission.Scheme}' not found.");
            }

            // Verify the entity exists
            var entityExists = await dbContext.Entities.AnyAsync(e => e.Id == entityId);
            if (!entityExists)
            {
                throw new InvalidOperationException($"Entity {entityId} not found.");
            }

            // Get or create the resource
            var resource = await CreateResourceNormalizer.ExecuteAsync(dbContext, permission.Uri);

            // Get or create the permission scheme
            var permissionScheme = await CreatePermissionSchemeNormalizer.ExecuteAsync(dbContext, entityId, schemeType.Id);

            // Check if URI access already exists
            var existingUriAccess = await dbContext.UriAccesses
                .Include(ua => ua.VerbType)
                .FirstOrDefaultAsync(ua => ua.PermissionSchemeId == permissionScheme.Id &&
                                          ua.ResourceId == resource.Id &&
                                          ua.VerbType.VerbName == permission.HttpVerb.ToString());

            if (existingUriAccess != null)
            {
                throw new InvalidOperationException($"Permission already exists for this entity: {permission.Uri} {permission.HttpVerb}");
            }

            // Create the URI access
            await CreateUriAccessNormalizer.ExecuteAsync(dbContext, permissionScheme, resource, permission.HttpVerb.ToString(), permission.Grant, permission.Deny);
        }
        
        // Legacy method for compatibility - remove after all callers are updated
        public static void Execute(Permission permission, int entityId)
        {
            throw new NotSupportedException("Legacy normalizer method is no longer supported. Use ExecuteAsync instead.");
        }
    }
}
