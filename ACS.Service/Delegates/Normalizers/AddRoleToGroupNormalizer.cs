using ACS.Service.Domain;

namespace ACS.Service.Delegates.Normalizers
{
    /// <summary>
    /// Pure behavioral transformation for adding a role to a group
    /// LMAX ARCHITECTURE: This is a pure static method that only manipulates in-memory object graph
    /// NO DATABASE OPERATIONS - NO VALIDATION - NO SIDE EFFECTS
    /// Domain objects handle business rules, this normalizer only handles behavioral consistency
    /// </summary>
    public static class AddRoleToGroupNormalizer
    {
        /// <summary>
        /// Creates bidirectional parent-child relationship between role and group in memory only
        /// </summary>
        /// <param name="role">The role being added to the group</param>
        /// <param name="group">The group receiving the role</param>
        public static void Execute(Role role, Group group)
        {
            // Add role to group's children collection (role becomes child of group)
            if (!group.Children.Contains(role))
            {
                group.Children.Add(role);
            }

            // Add group to role's parents collection (group becomes parent of role)
            if (!role.Parents.Contains(group))
            {
                role.Parents.Add(group);
            }
        }
    }
}
