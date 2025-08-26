using ACS.Service.Domain;

namespace ACS.Service.Delegates.Normalizers
{
    /// <summary>
    /// Pure normalizer for normalizing URI access patterns
    /// Handles only the behavioral transformation - no validation, no side effects
    /// Assumes business rules have already been validated by the domain object
    /// </summary>
    public static class CreateUriAccessNormalizer
    {
        /// <summary>
        /// Executes the pure behavioral transformation of normalizing URI access permissions
        /// Updates the permission object to ensure consistent grant/deny settings
        /// </summary>
        /// <param name="permission">The permission domain object to normalize</param>
        public static void Execute(Permission permission)
        {
            // BEHAVIORAL NORMALIZATION: Pure access pattern normalization
            // No validation - assumes domain object already validated
            // No database operations - pure in-memory transformation
            // No side effects - just ensures consistent grant/deny patterns
            
            if (permission == null) return;
            
            // Normalize grant/deny combinations to prevent conflicts
            // Business rule: If both grant and deny are true, deny takes precedence
            if (permission.Grant && permission.Deny)
            {
                permission.Grant = false; // Deny overrides grant
            }
            
            // Ensure at least one access type is specified
            if (!permission.Grant && !permission.Deny)
            {
                permission.Grant = true; // Default to grant if neither specified
            }
            
            // That's it - pure mechanical transformation
            // Persistence will be handled later by the command processor
        }
        
        // Legacy async method - kept for compatibility during transition
        [Obsolete("Use pure Execute(Permission) method instead. Database operations moved to persistence layer.")]
        public static Task<object> ExecuteAsync(object dbContext, object permissionScheme, object resource, string httpVerb, bool grant, bool deny)
        {
            throw new NotSupportedException(
                "Database operations have been moved to persistence layer. " +
                "Use Execute(Permission) for pure in-memory normalization, " +
                "or call through domain object which handles persistence.");
        }
    }
}
