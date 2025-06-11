using ACS.Service.Data.Models;

namespace ACS.Service.Delegates.Normalizers
{
    internal class RemoveRoleFromGroupNormalizer
    {
        public static List<Group> Groups { get; set; }
        public static List<Role> Roles { get; set; }
        public static void Execute(int roleId, int groupId)
        {
            var role = Roles.Single(x => x.Id == roleId);
            var group = Groups.Single(x => x.Id == groupId);
            if (group.Roles.Contains(role))
            {
                group.Roles.Remove(role);
                role.Group = null;
                role.GroupId = 0;
            }
        }
    }
}
