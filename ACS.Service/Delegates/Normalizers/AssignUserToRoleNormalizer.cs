using ACS.Service.Domain;

namespace ACS.Service.Delegates.Normalizers
{
    internal static class AssignUserToRoleNormalizer
    {
        // These now reference the same Domain objects as the entity graph
        public static List<Role> Roles { get; set; } = null!;
        public static List<User> Users { get; set; } = null!;
        
        public static void Execute(int userId, int roleId)
        {
            if (Roles is null)
            {
                throw new InvalidOperationException("Roles collection has not been initialized.");
            }

            if (Users is null)
            {
                throw new InvalidOperationException("Users collection has not been initialized.");
            }

            var user = Users.SingleOrDefault(x => x.Id == userId)
                ?? throw new InvalidOperationException($"User {userId} not found.");

            var role = Roles.SingleOrDefault(x => x.Id == roleId)
                ?? throw new InvalidOperationException($"Role {roleId} not found.");

            // Update the domain object collections directly
            if (!role.Children.Contains(user))
            {
                role.Children.Add(user);
            }
            
            if (!user.Parents.Contains(role))
            {
                user.Parents.Add(role);
            }
        }
    }
}
