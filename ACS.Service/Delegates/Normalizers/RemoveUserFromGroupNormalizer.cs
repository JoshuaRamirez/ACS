using ACS.Service.Data;
using Microsoft.EntityFrameworkCore;

namespace ACS.Service.Delegates.Normalizers
{
    public static class RemoveUserFromGroupNormalizer
    {
        public static async Task ExecuteAsync(ApplicationDbContext dbContext, int userId, int groupId)
        {
            // Find the existing relationship
            var existingRelation = await dbContext.UserGroups
                .FirstOrDefaultAsync(ug => ug.UserId == userId && ug.GroupId == groupId);
            
            if (existingRelation == null)
            {
                throw new InvalidOperationException($"User {userId} is not a member of group {groupId}.");
            }

            // Remove the relationship
            dbContext.UserGroups.Remove(existingRelation);
            
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
