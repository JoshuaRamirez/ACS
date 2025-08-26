using ACS.Service.Domain;

namespace ACS.Service.Delegates.Normalizers
{
    /// <summary>
    /// Pure normalizer for unassigning a user from a role
    /// Handles only the behavioral transformation - no validation, no side effects
    /// Assumes business rules have already been validated by the domain object
    /// </summary>
    public static class UnAssignUserFromRoleNormalizer
    {
        /// <summary>
        /// Executes the pure behavioral transformation of unassigning a user from a role
        /// Updates the in-memory object graph to maintain bidirectional relationships
        /// </summary>
        /// <param name="user">The user domain object to unassign</param>
        /// <param name="role">The role domain object to unassign the user from</param>
        public static void Execute(User user, Role role)
        {
            // BEHAVIORAL NORMALIZATION: Pure graph manipulation
            // No validation - assumes domain object already validated
            // No database operations - pure in-memory transformation
            // No side effects - just ensures bidirectional consistency
            
            // Remove role from user's parents
            user.Parents.Remove(role);
            
            // Remove user from role's children
            role.Children.Remove(user);
            
            // That's it - pure mechanical transformation
            // Persistence will be handled later by the command processor
        }
        
        // Legacy async method - kept for compatibility during transition
        [Obsolete("Use pure Execute(User, Role) method instead. Database operations moved to persistence layer.")]
        public static Task ExecuteAsync(object dbContext, int userId, int roleId)
        {
            throw new NotSupportedException(
                "Database operations have been moved to persistence layer. " +
                "Use Execute(User, Role) for pure in-memory normalization, " +
                "or call through domain object which handles persistence.");
        }
    }
}
