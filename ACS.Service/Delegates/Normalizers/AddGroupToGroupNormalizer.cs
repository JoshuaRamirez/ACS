using System;
using System.Linq;
using ACS.Service.Domain;

namespace ACS.Service.Delegates.Normalizers
{
    internal static class AddGroupToGroupNormalizer
    {
        // These now reference the same Domain objects as the entity graph
        public static List<Group> Groups { get; set; } = null!;
        
        public static void Execute(int childGroupId, int parentGroupId)
        {
            if (Groups is null)
            {
                throw new InvalidOperationException("Groups collection has not been initialized.");
            }

            var parent = Groups.SingleOrDefault(x => x.Id == parentGroupId)
                ?? throw new InvalidOperationException($"Parent group {parentGroupId} not found.");

            var child = Groups.SingleOrDefault(x => x.Id == childGroupId)
                ?? throw new InvalidOperationException($"Child group {childGroupId} not found.");

            // Update the domain object collections directly
            if (!parent.Children.Contains(child))
            {
                parent.Children.Add(child);
            }
            
            if (!child.Parents.Contains(parent))
            {
                child.Parents.Add(parent);
            }
        }
    }
}
