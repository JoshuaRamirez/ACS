using ACS.Service.Data.Models;

namespace ACS.Service.Delegates.Normalizers
{
    internal static class AddGroupToGroupNormalizer
    {
        public static List<Group> Groups { get; set; }
        public static void Execute(int childGroupId, int parentGroupId)
        {
            var parent = Groups.Single(x => x.Id == parentGroupId);
            var child = Groups.Single(x => x.Id == childGroupId);
            parent.ChildGroups.Add(child);
            child.ParentGroup = parent;
            child.ParentGroupId = parent.Id;
        }
    }
}
