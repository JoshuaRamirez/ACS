using ACS.Service.Data.Models;

namespace ACS.Service.Delegates.Normalizers
{
    internal class UnAssignUserFromRoleNormalizer
    {
        public static List<Role> Roles { get; set; }
        public static List<User> Users { get; set; }
        public static void Execute(int userId, int roleId)
        {
            var user = Users.Single(x => x.Id == userId);
            var role = Roles.Single(x => x.Id == roleId);
            if (role.Users.Contains(user))
            {
                role.Users.Remove(user);
                user.Role = null;
                user.RoleId = 0;
            }
        }
    }
}
