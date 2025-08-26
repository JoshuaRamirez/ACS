using ACS.Service.Domain;

namespace ACS.Service.Delegates.Normalizers
{
    /// <summary>
    /// Pure normalizer for adding a permission to an entity
    /// Handles only the behavioral transformation - no validation, no side effects
    /// Assumes business rules have already been validated by the domain object
    /// </summary>
    public static class AddPermissionToEntityNormalizer
    {
        /// <summary>
        /// Executes the pure behavioral transformation of adding a permission to an entity
        /// Updates the in-memory object graph to maintain bidirectional relationships
        /// </summary>
        /// <param name="permission">The permission domain object to add</param>
        /// <param name="entity">The entity domain object to add the permission to</param>
        public static void Execute(Permission permission, Entity entity)
        {
            // BEHAVIORAL NORMALIZATION: Pure graph manipulation
            // No validation - assumes domain object already validated
            // No database operations - pure in-memory transformation
            // No side effects - just ensures bidirectional consistency
            
            // Add permission to entity's permissions collection if not already there
            if (!entity.Permissions.Contains(permission))
            {
                entity.Permissions.Add(permission);
            }
            
            // Normalize the permission URI while we're at it
            CreateResourceNormalizer.Execute(permission);
            
            // That's it - pure mechanical transformation
            // Persistence will be handled later by the command processor
        }
        
        // Legacy async method - kept for compatibility during transition
        [Obsolete("Use pure Execute(Permission, Entity) method instead. Database operations moved to persistence layer.")]
        public static Task ExecuteAsync(object dbContext, Permission permission, int entityId)
        {
            throw new NotSupportedException(
                "Database operations have been moved to persistence layer. " +
                "Use Execute(Permission, Entity) for pure in-memory normalization, " +
                "or call through domain object which handles persistence.");
        }
    }
}
