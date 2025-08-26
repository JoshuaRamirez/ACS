using ACS.Service.Domain;

namespace ACS.Service.Delegates.Normalizers
{
    /// <summary>
    /// Pure behavioral transformation for removing a role from a group
    /// LMAX ARCHITECTURE: This is a pure static method that only manipulates in-memory object graph
    /// NO DATABASE OPERATIONS - NO VALIDATION - NO SIDE EFFECTS
    /// Domain objects handle business rules, this normalizer only handles behavioral consistency
    /// </summary>
    public static class RemoveRoleFromGroupNormalizer
    {
        /// <summary>
        /// Removes bidirectional parent-child relationship between role and group in memory only
        /// </summary>
        /// <param name="role">The role being removed from the group</param>
        /// <param name="group">The group to remove the role from</param>
        public static void Execute(Role role, Group group)
        {
            // Remove role from group's children collection
            if (group.Children.Contains(role))
            {
                group.Children.Remove(role);
            }

            // Remove group from role's parents collection
            if (role.Parents.Contains(group))
            {
                role.Parents.Remove(group);
            }
        }

        /// <summary>
        /// Async version for orchestration service compatibility
        /// </summary>
        public static Task ExecuteAsync(Role role, Group group)
        {
            Execute(role, group);
            return Task.CompletedTask;
        }
    }
}
