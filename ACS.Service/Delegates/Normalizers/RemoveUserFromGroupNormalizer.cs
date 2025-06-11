using ACS.Service.Data.Models;

namespace ACS.Service.Delegates.Normalizers
{
    internal static class RemoveUserFromGroupNormalizer
    {
        public static List<Group> Groups { get; set; }
        public static List<User> Users { get; set; }
        public static void Execute(int userId, int groupId)
        {
            var user = Users.Single(x => x.Id == userId);
            var group = Groups.Single(x => x.Id == groupId);
            if (group.Users.Contains(user))
            {
                group.Users.Remove(user);
                user.Group = null;
                user.GroupId = 0;
            }
        }
    }
}
