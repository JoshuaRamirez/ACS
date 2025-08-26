using ACS.Service.Domain;

namespace ACS.Service.Delegates.Normalizers
{
    /// <summary>
    /// Pure normalizer for removing a user from a group
    /// Handles only the behavioral transformation - no validation, no side effects
    /// Assumes business rules have already been validated by the domain object
    /// </summary>
    public static class RemoveUserFromGroupNormalizer
    {
        /// <summary>
        /// Executes the pure behavioral transformation of removing a user from a group
        /// Updates the in-memory object graph to maintain bidirectional relationships
        /// </summary>
        /// <param name="user">The user domain object to remove</param>
        /// <param name="group">The group domain object to remove the user from</param>
        public static void Execute(User user, Group group)
        {
            // BEHAVIORAL NORMALIZATION: Pure graph manipulation
            // No validation - assumes domain object already validated
            // No database operations - pure in-memory transformation
            // No side effects - just ensures bidirectional consistency
            
            // Remove user from group's children
            group.Children.Remove(user);
            
            // Remove group from user's parents
            user.Parents.Remove(group);
            
            // That's it - pure mechanical transformation
            // Persistence will be handled later by the command processor
        }
        
        // Legacy async method - kept for compatibility during transition
        [Obsolete("Use pure Execute(User, Group) method instead. Database operations moved to persistence layer.")]
        public static Task ExecuteAsync(object dbContext, int userId, int groupId)
        {
            throw new NotSupportedException(
                "Database operations have been moved to persistence layer. " +
                "Use Execute(User, Group) for pure in-memory normalization, " +
                "or call through domain object which handles persistence.");
        }
    }
}
