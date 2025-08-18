using ACS.Service.Data;
using ACS.Service.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ACS.Service.Delegates.Normalizers
{
    public static class AddRoleToGroupNormalizer
    {
        public static async Task ExecuteAsync(ApplicationDbContext dbContext, int groupId, int roleId, string createdBy)
        {
            // Verify the group and role exist
            var groupExists = await dbContext.Groups.AnyAsync(g => g.Id == groupId);
            if (!groupExists)
            {
                throw new InvalidOperationException($"Group {groupId} not found.");
            }

            var roleExists = await dbContext.Roles.AnyAsync(r => r.Id == roleId);
            if (!roleExists)
            {
                throw new InvalidOperationException($"Role {roleId} not found.");
            }

            // Check if relationship already exists
            var existingRelation = await dbContext.GroupRoles
                .FirstOrDefaultAsync(gr => gr.GroupId == groupId && gr.RoleId == roleId);
            
            if (existingRelation != null)
            {
                return; // Relationship already exists, nothing to do
            }

            // Create the new relationship
            var groupRole = new GroupRole
            {
                GroupId = groupId,
                RoleId = roleId,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.GroupRoles.Add(groupRole);
            
            // Update the UpdatedAt timestamp for both entities
            var group = await dbContext.Groups.FindAsync(groupId);
            var role = await dbContext.Roles.FindAsync(roleId);
            
            if (group != null)
            {
                group.UpdatedAt = DateTime.UtcNow;
                dbContext.Groups.Update(group);
            }
            
            if (role != null)
            {
                role.UpdatedAt = DateTime.UtcNow;
                dbContext.Roles.Update(role);
            }
        }
        
        // Legacy method for compatibility - remove after domain layer is updated
        public static void Execute(int roleId, int groupId)
        {
            throw new NotSupportedException("Legacy normalizer method is no longer supported. Use ExecuteAsync instead.");
        }
    }
}
