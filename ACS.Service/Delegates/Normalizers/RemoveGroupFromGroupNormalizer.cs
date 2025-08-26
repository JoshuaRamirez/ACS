using ACS.Service.Domain;

namespace ACS.Service.Delegates.Normalizers
{
    /// <summary>
    /// Pure behavioral transformation for removing a child group from a parent group
    /// LMAX ARCHITECTURE: This is a pure static method that only manipulates in-memory object graph
    /// NO DATABASE OPERATIONS - NO VALIDATION - NO SIDE EFFECTS
    /// Domain objects handle business rules, this normalizer only handles behavioral consistency
    /// </summary>
    public static class RemoveGroupFromGroupNormalizer
    {
        /// <summary>
        /// Removes bidirectional parent-child relationship between groups in memory only
        /// </summary>
        /// <param name="childGroup">The child group being removed</param>
        /// <param name="parentGroup">The parent group to remove from</param>
        public static void Execute(Group childGroup, Group parentGroup)
        {
            // Remove child from parent's children collection
            if (parentGroup.Children.Contains(childGroup))
            {
                parentGroup.Children.Remove(childGroup);
            }

            // Remove parent from child's parents collection
            if (childGroup.Parents.Contains(parentGroup))
            {
                childGroup.Parents.Remove(parentGroup);
            }
        }

        /// <summary>
        /// Async version for orchestration service compatibility
        /// </summary>
        public static Task ExecuteAsync(Group childGroup, Group parentGroup)
        {
            Execute(childGroup, parentGroup);
            return Task.CompletedTask;
        }
    }
}
