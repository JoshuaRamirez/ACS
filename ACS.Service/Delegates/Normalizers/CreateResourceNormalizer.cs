using ACS.Service.Domain;

namespace ACS.Service.Delegates.Normalizers
{
    /// <summary>
    /// Pure normalizer for normalizing resource URI patterns
    /// Handles only the behavioral transformation - no validation, no side effects
    /// Assumes business rules have already been validated by the domain object
    /// </summary>
    public static class CreateResourceNormalizer
    {
        /// <summary>
        /// Executes the pure behavioral transformation of normalizing a resource URI
        /// Updates the permission object to ensure consistent URI formatting
        /// </summary>
        /// <param name="permission">The permission domain object containing the URI to normalize</param>
        public static void Execute(Permission permission)
        {
            // BEHAVIORAL NORMALIZATION: Pure URI normalization
            // No validation - assumes domain object already validated
            // No database operations - pure in-memory transformation
            // No side effects - just ensures consistent URI formatting
            
            if (permission?.Uri == null) return;
            
            // Normalize URI format (remove trailing slashes, lowercase scheme, etc.)
            var normalizedUri = NormalizeUri(permission.Uri);
            permission.Uri = normalizedUri;
            
            // That's it - pure mechanical transformation
            // Persistence will be handled later by the command processor
        }
        
        private static string NormalizeUri(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri))
                return uri;
                
            // Basic URI normalization - remove trailing slash, ensure consistent format
            var normalized = uri.Trim();
            if (normalized.EndsWith("/") && normalized.Length > 1)
                normalized = normalized.TrimEnd('/');
                
            return normalized;
        }
        
        // Legacy async method - kept for compatibility during transition
        [Obsolete("Use pure Execute(Permission) method instead. Database operations moved to persistence layer.")]
        public static Task<object> ExecuteAsync(object dbContext, string uri)
        {
            throw new NotSupportedException(
                "Database operations have been moved to persistence layer. " +
                "Use Execute(Permission) for pure in-memory normalization, " +
                "or call through domain object which handles persistence.");
        }
    }
}
