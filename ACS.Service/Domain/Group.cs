using System.Collections.ObjectModel;
using ACS.Service.Delegates.Normalizers;

namespace ACS.Service.Domain;

public class Group : Entity
{
    public ReadOnlyCollection<Group> Groups => Children.OfType<Group>().ToList().AsReadOnly();

    public ReadOnlyCollection<Group> ParentGroups => Parents.OfType<Group>().ToList().AsReadOnly();

    public ReadOnlyCollection<User> Users => Children.OfType<User>().ToList().AsReadOnly();

    public ReadOnlyCollection<Role> Roles => Children.OfType<Role>().ToList().AsReadOnly();

    public void AddUser(User user)
    {
        AddChild(user);
        // Note: Persistence should now be handled through proper service layer
        // AddUserToGroupNormalizer.Execute(user.Id, this.Id);
    }

    public void RemoveUser(User user)
    {
        RemoveChild(user);
        // Note: Persistence should now be handled through proper service layer
        // RemoveUserFromGroupNormalizer.Execute(user.Id, this.Id);
    }

    public void AddRole(Role role)
    {
        AddChild(role);
        // Note: Persistence should now be handled through proper service layer
        // AddRoleToGroupNormalizer.Execute(role.Id, this.Id);
    }

    public void RemoveRole(Role role)
    {
        RemoveChild(role);
        // Note: Persistence should now be handled through proper service layer
        // RemoveRoleFromGroupNormalizer.Execute(role.Id, this.Id);
    }

    public void AddGroup(Group group)
    {
        if (group == this || ContainsGroup(group, this))
        {
            throw new InvalidOperationException("Cannot add group to itself or create a cyclical hierarchy.");
        }
        AddChild(group);
        // Note: Persistence should now be handled through proper service layer
        // AddGroupToGroupNormalizer.Execute(group.Id, this.Id);
    }

    public void RemoveGroup(Group group)
    {
        RemoveChild(group);
        // Note: Persistence should now be handled through proper service layer
        // RemoveGroupFromGroupNormalizer.Execute(group.Id, this.Id);
    }

    public void AddToGroup(Group parent)
    {
        parent.AddGroup(this);
    }

    public void RemoveFromGroup(Group parent)
    {
        parent.RemoveGroup(this);
    }

    private bool ContainsGroup(Group parentGroup, Group groupToCheck)
    {
        var queue = new Queue<Group>();
        queue.Enqueue(parentGroup);

        while (queue.Count > 0)
        {
            var currentGroup = queue.Dequeue();
            if (currentGroup == groupToCheck)
            {
                return true;
            }

            foreach (var child in currentGroup.Children)
            {
                if (child is Group childGroup)
                {
                    queue.Enqueue(childGroup);
                }
            }
        }

        return false;
    }
}