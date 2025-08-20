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

            // Implement cycle detection logic to prevent circular references
            if (await WouldCreateCycleAsync(childGroupId, parentGroupId))
            {
                throw new InvalidOperationException(
                    $"Cannot add group {childGroupId} to parent group {parentGroupId} as it would create a circular reference");
            }

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
        
        /// <summary>
        /// Checks if adding the specified parent-child relationship would create a cycle
        /// </summary>
        private static async Task<bool> WouldCreateCycleAsync(int childGroupId, int parentGroupId)
        {
            using var scope = ServiceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            // If the child group is the same as parent group, it's a direct cycle
            if (childGroupId == parentGroupId)
            {
                return true;
            }
            
            // Use BFS to detect cycles: check if parentGroupId is already an ancestor of childGroupId
            var visited = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(childGroupId);
            
            while (queue.Count > 0)
            {
                var currentGroupId = queue.Dequeue();
                
                if (visited.Contains(currentGroupId))
                {
                    continue;
                }
                
                visited.Add(currentGroupId);
                
                // If we find the intended parent in the child's ancestry, it would create a cycle
                if (currentGroupId == parentGroupId)
                {
                    return true;
                }
                
                // Get all parents of the current group and add them to the queue
                var parentIds = await context.GroupHierarchy
                    .Where(gh => gh.ChildGroupId == currentGroupId)
                    .Select(gh => gh.ParentGroupId)
                    .ToListAsync();
                    
                foreach (var pid in parentIds)
                {
                    if (!visited.Contains(pid))
                    {
                        queue.Enqueue(pid);
                    }
                }
            }
            
            return false;
        }
    }
}
