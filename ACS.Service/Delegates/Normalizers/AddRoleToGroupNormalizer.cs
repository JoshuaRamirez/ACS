using System;
using System.Linq;
using ACS.Service.Domain;

namespace ACS.Service.Delegates.Normalizers
{
    internal static class AddRoleToGroupNormalizer
    {
        // These now reference the same Domain objects as the entity graph
        public static List<Group> Groups { get; set; } = null!;
        public static List<Role> Roles { get; set; } = null!;
        
        public static void Execute(int roleId, int groupId)
        {
            if (Groups is null)
            {
                throw new InvalidOperationException("Groups collection has not been initialized.");
            }

            if (Roles is null)
            {
                throw new InvalidOperationException("Roles collection has not been initialized.");
            }

            var role = Roles.SingleOrDefault(x => x.Id == roleId)
                ?? throw new InvalidOperationException($"Role {roleId} not found.");

            var group = Groups.SingleOrDefault(x => x.Id == groupId)
                ?? throw new InvalidOperationException($"Group {groupId} not found.");

            // Update the domain object collections directly
            if (!group.Children.Contains(role))
            {
                group.Children.Add(role);
            }
            
            if (!role.Parents.Contains(group))
            {
                role.Parents.Add(group);
            }
        }
    }
}
