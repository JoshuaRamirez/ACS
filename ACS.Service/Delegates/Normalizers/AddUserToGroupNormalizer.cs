using ACS.Service.Data.Models;

namespace ACS.Service.Delegates.Normalizers
{
    internal static class AddUserToGroupNormalizer
    {
        public static List<Group> Groups { get; set; }
        public static List<User> Users { get; set; }
        public static void Execute(int userId, int groupId)
        {
            var user = Users.Single(x => x.Id == userId);
            var group = Groups.Single(x => x.Id == groupId);
            group.Users.Add(user);
        }
    }
}
