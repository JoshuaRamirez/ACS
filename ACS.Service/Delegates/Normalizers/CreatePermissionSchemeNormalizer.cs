using ACS.Service.Data;
using ACS.Service.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ACS.Service.Delegates.Normalizers
{
    internal static class CreatePermissionSchemeNormalizer
    {
        public static async Task<PermissionScheme> ExecuteAsync(ApplicationDbContext dbContext, int entityId, int schemeTypeId)
        {
            // Verify the entity exists
            var entityExists = await dbContext.Entities.AnyAsync(e => e.Id == entityId);
            if (!entityExists)
            {
                throw new InvalidOperationException($"Entity {entityId} not found.");
            }

            // Verify the scheme type exists
            var schemeTypeExists = await dbContext.SchemeTypes.AnyAsync(st => st.Id == schemeTypeId);
            if (!schemeTypeExists)
            {
                throw new InvalidOperationException($"SchemeType {schemeTypeId} not found.");
            }

            // Check if permission scheme already exists
            var existingScheme = await dbContext.EntityPermissions
                .FirstOrDefaultAsync(ps => ps.EntityId == entityId && ps.SchemeTypeId == schemeTypeId);
            
            if (existingScheme != null)
            {
                return existingScheme; // Return existing scheme
            }

            // Create new permission scheme
            var permissionScheme = new PermissionScheme
            {
                EntityId = entityId,
                SchemeTypeId = schemeTypeId
            };

            dbContext.EntityPermissions.Add(permissionScheme);
            await dbContext.SaveChangesAsync();

            return permissionScheme;
        }
        
        // Legacy method for compatibility - remove after all callers are updated
        public static PermissionScheme Execute(int entityId)
        {
            throw new NotSupportedException("Legacy normalizer method is no longer supported. Use ExecuteAsync instead.");
        }
    }
}
