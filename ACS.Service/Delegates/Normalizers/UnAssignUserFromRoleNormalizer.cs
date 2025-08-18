using ACS.Service.Data;
using Microsoft.EntityFrameworkCore;

namespace ACS.Service.Delegates.Normalizers
{
    public static class UnAssignUserFromRoleNormalizer
    {
        public static async Task ExecuteAsync(ApplicationDbContext dbContext, int userId, int roleId)
        {
            // Find the existing relationship
            var existingRelation = await dbContext.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
            
            if (existingRelation == null)
            {
                throw new InvalidOperationException($"User {userId} is not assigned to role {roleId}.");
            }

            // Remove the relationship
            dbContext.UserRoles.Remove(existingRelation);
            
            // Update the UpdatedAt timestamp for both entities
            var user = await dbContext.Users.FindAsync(userId);
            var role = await dbContext.Roles.FindAsync(roleId);
            
            if (user != null)
            {
                user.UpdatedAt = DateTime.UtcNow;
                dbContext.Users.Update(user);
            }
            
            if (role != null)
            {
                role.UpdatedAt = DateTime.UtcNow;
                dbContext.Roles.Update(role);
            }
        }
        
        // Legacy method for compatibility - remove after domain layer is updated
        public static void Execute(int userId, int roleId)
        {
            throw new NotSupportedException("Legacy normalizer method is no longer supported. Use ExecuteAsync instead.");
        }
    }
}
