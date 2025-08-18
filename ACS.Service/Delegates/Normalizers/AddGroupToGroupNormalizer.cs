using ACS.Service.Data;
using ACS.Service.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ACS.Service.Delegates.Normalizers
{
    public static class AddGroupToGroupNormalizer
    {
        public static async Task ExecuteAsync(ApplicationDbContext dbContext, int childGroupId, int parentGroupId, string createdBy)
        {
            // Verify both groups exist
            var childExists = await dbContext.Groups.AnyAsync(g => g.Id == childGroupId);
            if (!childExists)
            {
                throw new InvalidOperationException($"Child group {childGroupId} not found.");
            }

            var parentExists = await dbContext.Groups.AnyAsync(g => g.Id == parentGroupId);
            if (!parentExists)
            {
                throw new InvalidOperationException($"Parent group {parentGroupId} not found.");
            }

            // Check for self-reference
            if (childGroupId == parentGroupId)
            {
                throw new InvalidOperationException("Cannot add group to itself.");
            }

            // Check if relationship already exists
            var existingRelation = await dbContext.GroupHierarchies
                .FirstOrDefaultAsync(gh => gh.ChildGroupId == childGroupId && gh.ParentGroupId == parentGroupId);
            
            if (existingRelation != null)
            {
                return; // Relationship already exists, nothing to do
            }

            // TODO: Add cycle detection logic here
            // This would involve checking if adding this relationship would create a cycle

            // Create the new relationship
            var groupHierarchy = new GroupHierarchy
            {
                ChildGroupId = childGroupId,
                ParentGroupId = parentGroupId,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.GroupHierarchies.Add(groupHierarchy);
            
            // Update the UpdatedAt timestamp for both groups
            var childGroup = await dbContext.Groups.FindAsync(childGroupId);
            var parentGroup = await dbContext.Groups.FindAsync(parentGroupId);
            
            if (childGroup != null)
            {
                childGroup.UpdatedAt = DateTime.UtcNow;
                dbContext.Groups.Update(childGroup);
            }
            
            if (parentGroup != null)
            {
                parentGroup.UpdatedAt = DateTime.UtcNow;
                dbContext.Groups.Update(parentGroup);
            }
        }
        
        // Legacy method for compatibility - remove after domain layer is updated
        public static void Execute(int childGroupId, int parentGroupId)
        {
            throw new NotSupportedException("Legacy normalizer method is no longer supported. Use ExecuteAsync instead.");
        }
    }
}
