using ACS.Service.Data.Models;

namespace ACS.Service.Delegates.Normalizers
{
    internal class AssignUserToRoleNormalizer
    {
        public static List<Role> Roles { get; set; }
        public static List<Group> Groups { get; set; }
        public static void Execute(int userId, int roleId)
        {
            var group = Groups.Single(x => x.Id == userId);
            var role = Roles.Single(x => x.Id == roleId);
            group.Roles.Add(role);
        }
    }
}
