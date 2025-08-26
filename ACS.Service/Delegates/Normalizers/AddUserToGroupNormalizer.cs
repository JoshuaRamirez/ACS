using ACS.Service.Domain;

namespace ACS.Service.Delegates.Normalizers
{
    /// <summary>
    /// Pure normalizer for adding a user to a group
    /// Handles only the behavioral transformation - no validation, no side effects
    /// Assumes business rules have already been validated by the domain object
    /// </summary>
    public static class AddUserToGroupNormalizer
    {
        /// <summary>
        /// Executes the pure behavioral transformation of adding a user to a group
        /// Updates the in-memory object graph to maintain bidirectional relationships
        /// </summary>
        /// <param name="user">The user domain object to add</param>
        /// <param name="group">The group domain object to add the user to</param>
        public static void Execute(User user, Group group)
        {
            // BEHAVIORAL NORMALIZATION: Pure graph manipulation
            // No validation - assumes domain object already validated
            // No database operations - pure in-memory transformation
            // No side effects - just ensures bidirectional consistency
            
            // Add user to group's children if not already there
            if (!group.Children.Contains(user))
            {
                group.Children.Add(user);
            }
            
            // Add group to user's parents if not already there
            if (!user.Parents.Contains(group))
            {
                user.Parents.Add(group);
            }
            
            // That's it - pure mechanical transformation
            // Persistence will be handled later by the command processor
        }
        
        // Legacy async method - kept for compatibility during transition
        // TODO: Remove after all callers updated to use pure Execute method
        [Obsolete("Use pure Execute(User, Group) method instead. Database operations moved to persistence layer.")]
        public static Task ExecuteAsync(object dbContext, int userId, int groupId, string createdBy)
        {
            throw new NotSupportedException(
                "Database operations have been moved to persistence layer. " +
                "Use Execute(User, Group) for pure in-memory normalization, " +
                "or call through domain object which handles persistence.");
        }
    }
}
