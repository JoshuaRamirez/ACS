using ACS.Service.Data;
using ACS.Service.Data.Models;
using ACS.Service.Domain;
using Microsoft.EntityFrameworkCore;

namespace ACS.Service.Delegates.Normalizers
{
    internal static class CreateUriAccessNormalizer
    {
        public static async Task<UriAccess> ExecuteAsync(ApplicationDbContext dbContext, PermissionScheme permissionScheme, Resource resource, string httpVerb, bool grant, bool deny)
        {
            // Find the verb type
            var verbType = await dbContext.VerbTypes
                .FirstOrDefaultAsync(vt => string.Equals(vt.VerbName, httpVerb, StringComparison.OrdinalIgnoreCase));
            
            if (verbType == null)
            {
                throw new InvalidOperationException($"VerbType '{httpVerb}' not found.");
            }

            // Check if URI access already exists
            var existingUriAccess = await dbContext.UriAccesses
                .FirstOrDefaultAsync(ua => ua.PermissionSchemeId == permissionScheme.Id &&
                                          ua.ResourceId == resource.Id &&
                                          ua.VerbTypeId == verbType.Id);
            
            if (existingUriAccess != null)
            {
                // Update existing URI access
                existingUriAccess.Grant = grant;
                existingUriAccess.Deny = deny;
                await dbContext.SaveChangesAsync();
                return existingUriAccess;
            }

            // Create new URI access
            var uriAccess = new UriAccess
            {
                PermissionSchemeId = permissionScheme.Id,
                ResourceId = resource.Id,
                VerbTypeId = verbType.Id,
                Grant = grant,
                Deny = deny,
                PermissionScheme = permissionScheme,
                Resource = resource,
                VerbType = verbType
            };

            dbContext.UriAccesses.Add(uriAccess);
            await dbContext.SaveChangesAsync();

            return uriAccess;
        }
        
        // Legacy method for compatibility - remove after all callers are updated
        public static UriAccess Execute(PermissionScheme permissionScheme, Resource resource, Permission permission)
        {
            throw new NotSupportedException("Legacy normalizer method is no longer supported. Use ExecuteAsync instead.");
        }
    }
}
