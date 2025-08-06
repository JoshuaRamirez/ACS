using System;
using System.Linq;
using ACS.Service.Data.Models;

namespace ACS.Service.Delegates.Normalizers
{
    internal static class AddUserToGroupNormalizer
    {
        public static List<Group> Groups { get; set; } = null!;
        public static List<User> Users { get; set; } = null!;
        public static void Execute(int userId, int groupId)
        {
            if (Groups is null)
            {
                throw new InvalidOperationException("Groups collection has not been initialized.");
            }

            if (Users is null)
            {
                throw new InvalidOperationException("Users collection has not been initialized.");
            }

            var user = Users.SingleOrDefault(x => x.Id == userId)
                ?? throw new InvalidOperationException($"User {userId} not found.");

            var group = Groups.SingleOrDefault(x => x.Id == groupId)
                ?? throw new InvalidOperationException($"Group {groupId} not found.");

            group.Users.Add(user);
            user.Group = group;
            user.GroupId = group.Id;
        }
    }
}
