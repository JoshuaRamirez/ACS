using ACS.Service.Data.Models;

namespace ACS.Service.Delegates.Normalizers
{
    internal class AssignUserToRoleNormalizer
    {
        public static List<Role> Roles { get; set; }
        public static List<User> Users { get; set; }
        public static void Execute(int userId, int roleId)
        {
            var user = Users.Single(x => x.Id == userId);
            var role = Roles.Single(x => x.Id == roleId);
            role.Users.Add(user);
            user.Role = role;
            user.RoleId = role.Id;
        }
    }
}
