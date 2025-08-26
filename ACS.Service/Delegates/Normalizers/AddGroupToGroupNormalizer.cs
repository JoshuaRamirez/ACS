using ACS.Service.Domain;

namespace ACS.Service.Delegates.Normalizers
{
    /// <summary>
    /// Pure normalizer for adding a child group to a parent group
    /// Handles only the behavioral transformation - no validation, no side effects
    /// Assumes business rules have already been validated by the domain object
    /// </summary>
    public static class AddGroupToGroupNormalizer
    {
        /// <summary>
        /// Executes the pure behavioral transformation of adding a child group to a parent group
        /// Updates the in-memory object graph to maintain bidirectional relationships
        /// </summary>
        /// <param name="childGroup">The child group domain object to add</param>
        /// <param name="parentGroup">The parent group domain object to add the child to</param>
        public static void Execute(Group childGroup, Group parentGroup)
        {
            // BEHAVIORAL NORMALIZATION: Pure graph manipulation
            // No validation - assumes domain object already validated
            // No database operations - pure in-memory transformation
            // No side effects - just ensures bidirectional consistency
            
            // Add child group to parent's children if not already there
            if (!parentGroup.Children.Contains(childGroup))
            {
                parentGroup.Children.Add(childGroup);
            }
            
            // Add parent group to child's parents if not already there
            if (!childGroup.Parents.Contains(parentGroup))
            {
                childGroup.Parents.Add(parentGroup);
            }
            
            // That's it - pure mechanical transformation
            // Persistence will be handled later by the command processor
        }
        
        // Legacy async method - kept for compatibility during transition
        [Obsolete("Use pure Execute(Group, Group) method instead. Database operations moved to persistence layer.")]
        public static Task ExecuteAsync(object dbContext, int childGroupId, int parentGroupId, string createdBy)
        {
            throw new NotSupportedException(
                "Database operations have been moved to persistence layer. " +
                "Use Execute(Group, Group) for pure in-memory normalization, " +
                "or call through domain object which handles persistence.");
        }
    }
}
