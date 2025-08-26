using ACS.Service.Domain;

namespace ACS.Service.Delegates.Normalizers
{
    /// <summary>
    /// Pure normalizer for removing a permission from an entity
    /// Handles only the behavioral transformation - no validation, no side effects
    /// Assumes business rules have already been validated by the domain object
    /// </summary>
    public static class RemovePermissionFromEntityNormalizer
    {
        /// <summary>
        /// Executes the pure behavioral transformation of removing a permission from an entity
        /// Updates the in-memory object graph to maintain bidirectional relationships
        /// </summary>
        /// <param name="permission">The permission domain object to remove</param>
        /// <param name="entity">The entity domain object to remove the permission from</param>
        public static void Execute(Permission permission, Entity entity)
        {
            // BEHAVIORAL NORMALIZATION: Pure graph manipulation
            // No validation - assumes domain object already validated
            // No database operations - pure in-memory transformation
            // No side effects - just ensures bidirectional consistency
            
            // Remove permission from entity's permissions collection
            entity.Permissions.Remove(permission);
            
            // That's it - pure mechanical transformation
            // Persistence will be handled later by the command processor
        }
        
        // Legacy method - kept for compatibility during transition
        [Obsolete("Use pure Execute(Permission, Entity) method instead. Database operations moved to persistence layer.")]
        public static void Execute(Permission permission, int entityId)
        {
            throw new NotSupportedException(
                "Database operations have been moved to persistence layer. " +
                "Use Execute(Permission, Entity) for pure in-memory normalization, " +
                "or call through domain object which handles persistence.");
        }
    }
}