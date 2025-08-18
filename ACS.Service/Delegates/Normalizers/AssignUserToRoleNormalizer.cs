using ACS.Service.Data;
using ACS.Service.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ACS.Service.Delegates.Normalizers
{
    public static class AssignUserToRoleNormalizer
    {
        public static async Task ExecuteAsync(ApplicationDbContext dbContext, int userId, int roleId, string createdBy)
        {
            // Verify the user and role exist
            var userExists = await dbContext.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
            {
                throw new InvalidOperationException($"User {userId} not found.");
            }

            var roleExists = await dbContext.Roles.AnyAsync(r => r.Id == roleId);
            if (!roleExists)
            {
                throw new InvalidOperationException($"Role {roleId} not found.");
            }

            // Check if relationship already exists
            var existingRelation = await dbContext.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
            
            if (existingRelation != null)
            {
                return; // Relationship already exists, nothing to do
            }

            // Create the new relationship
            var userRole = new UserRole
            {
                UserId = userId,
                RoleId = roleId,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.UserRoles.Add(userRole);
            
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
