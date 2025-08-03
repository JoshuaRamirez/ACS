using System;
using System.Linq;
using ACS.Service.Data.Models;

namespace ACS.Service.Delegates.Normalizers
{
    internal static class AddGroupToGroupNormalizer
    {
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

            parent.ChildGroups.Add(child);
            child.ParentGroup = parent;
            child.ParentGroupId = parent.Id;
        }
    }
}
