using System.Collections.ObjectModel;
using ACS.Service.Delegates.Normalizers;

namespace ACS.Service.Domain;

public class Group : Entity
{

        public ReadOnlyCollection<Group> Groups => Children.OfType<Group>().ToList().AsReadOnly();

        public ReadOnlyCollection<User> Users => Children.OfType<User>().ToList().AsReadOnly();

        public ReadOnlyCollection<Role> Roles => Children.OfType<Role>().ToList().AsReadOnly();

    public void AddUser(User user)
    {
        AddChild(user);
        //AddUserToGroupNormalizer.Execute(user.Id, this.Id);
    }

    public void RemoveUser(User user)
    {
        RemoveChild(user);
        //RemoveUserFromGroupNormalizer.Execute(user.Id, this.Id);
    }

    public void AddRole(Role role)
    {
        AddChild(role);
        //AddRoleToGroupNormalizer.Execute(role.Id, this.Id);
    }

    public void RemoveRole(Role role)
    {
        RemoveChild(role);
        //RemoveRoleFromGroupNormalizer.Execute(role.Id, this.Id);
    }

    public void AddGroup(Group group)
    {
        if (group == this || ContainsGroup(group, this))
        {
            throw new InvalidOperationException("Cannot add group to itself or create a cyclical hierarchy.");
        }
        AddChild(group);
    }

    public void RemoveGroup(Group group)
    {
        RemoveChild(group);
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