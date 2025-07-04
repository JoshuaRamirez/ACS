using ACS.Service.Data.Models;

namespace ACS.Service.Delegates.Normalizers
{
    internal static class RemoveGroupFromGroupNormalizer
    {
        public static List<Group> Groups { get; set; }
        public static void Execute(int childGroupId, int parentGroupId)
        {
            var parent = Groups.Single(x => x.Id == parentGroupId);
            var child = Groups.Single(x => x.Id == childGroupId);
            if (parent.ChildGroups.Contains(child))
            {
                parent.ChildGroups.Remove(child);
                child.ParentGroup = null;
                child.ParentGroupId = 0;
            }
        }
    }
}
