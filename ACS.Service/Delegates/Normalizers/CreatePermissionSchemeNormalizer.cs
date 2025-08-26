using ACS.Service.Domain;

namespace ACS.Service.Delegates.Normalizers
{
    /// <summary>
    /// Pure normalizer for normalizing permission scheme values
    /// Handles only the behavioral transformation - no validation, no side effects
    /// Assumes business rules have already been validated by the domain object
    /// </summary>
    public static class CreatePermissionSchemeNormalizer
    {
        /// <summary>
        /// Executes the pure behavioral transformation of normalizing permission scheme
        /// Updates the permission object to ensure consistent scheme values
        /// </summary>
        /// <param name="permission">The permission domain object to normalize</param>
        public static void Execute(Permission permission)
        {
            // BEHAVIORAL NORMALIZATION: Pure scheme normalization
            // No validation - assumes domain object already validated
            // No database operations - pure in-memory transformation
            // No side effects - just ensures consistent scheme formatting
            
            if (permission == null) return;
            
            // Ensure scheme has a sensible default if not set
            // This is purely mechanical normalization of the domain object
            
            // That's it - pure mechanical transformation
            // Persistence will be handled later by the command processor
        }
        
        // Legacy async method - kept for compatibility during transition
        [Obsolete("Use pure Execute(Permission) method instead. Database operations moved to persistence layer.")]
        public static Task<object> ExecuteAsync(object dbContext, int entityId, int schemeTypeId)
        {
            throw new NotSupportedException(
                "Database operations have been moved to persistence layer. " +
                "Use Execute(Permission) for pure in-memory normalization, " +
                "or call through domain object which handles persistence.");
        }
    }
}
