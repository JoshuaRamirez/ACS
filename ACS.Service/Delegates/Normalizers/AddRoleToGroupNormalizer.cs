using System;
using System.Linq;
using ACS.Service.Data.Models;

namespace ACS.Service.Delegates.Normalizers
{
    internal class AddRoleToGroupNormalizer
    {
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

            group.Roles.Add(role);
            role.Group = group;
            role.GroupId = group.Id;
        }
    }
}
