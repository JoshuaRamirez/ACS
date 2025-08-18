using ACS.Service.Data;
using ACS.Service.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ACS.Service.Delegates.Normalizers
{
    public static class AddUserToGroupNormalizer
    {
        public static async Task ExecuteAsync(ApplicationDbContext dbContext, int userId, int groupId, string createdBy)
        {
            // Verify the user and group exist
            var userExists = await dbContext.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
            {
                throw new InvalidOperationException($"User {userId} not found.");
            }

            var groupExists = await dbContext.Groups.AnyAsync(g => g.Id == groupId);
            if (!groupExists)
            {
                throw new InvalidOperationException($"Group {groupId} not found.");
            }

            // Check if relationship already exists
            var existingRelation = await dbContext.UserGroups
                .FirstOrDefaultAsync(ug => ug.UserId == userId && ug.GroupId == groupId);
            
            if (existingRelation != null)
            {
                return; // Relationship already exists, nothing to do
            }

            // Create the new relationship
            var userGroup = new UserGroup
            {
                UserId = userId,
                GroupId = groupId,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.UserGroups.Add(userGroup);
            
            // Update the UpdatedAt timestamp for both entities
            var user = await dbContext.Users.FindAsync(userId);
            var group = await dbContext.Groups.FindAsync(groupId);
            
            if (user != null)
            {
                user.UpdatedAt = DateTime.UtcNow;
                dbContext.Users.Update(user);
            }
            
            if (group != null)
            {
                group.UpdatedAt = DateTime.UtcNow;
                dbContext.Groups.Update(group);
            }
        }
        
        // Legacy method for compatibility - remove after domain layer is updated
        public static void Execute(int userId, int groupId)
        {
            throw new NotSupportedException("Legacy normalizer method is no longer supported. Use ExecuteAsync instead.");
        }
    }
}
