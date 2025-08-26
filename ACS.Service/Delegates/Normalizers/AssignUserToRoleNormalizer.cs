using ACS.Service.Domain;

namespace ACS.Service.Delegates.Normalizers
{
    /// <summary>
    /// Pure normalizer for assigning a user to a role
    /// Handles only the behavioral transformation - no validation, no side effects
    /// Assumes business rules have already been validated by the domain object
    /// </summary>
    public static class AssignUserToRoleNormalizer
    {
        /// <summary>
        /// Executes the pure behavioral transformation of assigning a user to a role
        /// Updates the in-memory object graph to maintain bidirectional relationships
        /// </summary>
        /// <param name="user">The user domain object to assign</param>
        /// <param name="role">The role domain object to assign the user to</param>
        public static void Execute(User user, Role role)
        {
            // BEHAVIORAL NORMALIZATION: Pure graph manipulation
            // No validation - assumes domain object already validated
            // No database operations - pure in-memory transformation
            // No side effects - just ensures bidirectional consistency
            
            // Add role to user's parents if not already there
            if (!user.Parents.Contains(role))
            {
                user.Parents.Add(role);
            }
            
            // Add user to role's children if not already there
            if (!role.Children.Contains(user))
            {
                role.Children.Add(user);
            }
            
            // That's it - pure mechanical transformation
            // Persistence will be handled later by the command processor
        }
        
        // Legacy async method - kept for compatibility during transition
        // TODO: Remove after all callers updated to use pure Execute method
        [Obsolete("Use pure Execute(User, Role) method instead. Database operations moved to persistence layer.")]
        public static Task ExecuteAsync(object dbContext, int userId, int roleId, string createdBy)
        {
            throw new NotSupportedException(
                "Database operations have been moved to persistence layer. " +
                "Use Execute(User, Role) for pure in-memory normalization, " +
                "or call through domain object which handles persistence.");
        }
    }
}
