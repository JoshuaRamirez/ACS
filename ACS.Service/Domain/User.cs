using System.Collections.ObjectModel;
using ACS.Service.Delegates.Normalizers;

namespace ACS.Service.Domain;

public class User : Entity
{
    public ReadOnlyCollection<Group> GroupMemberships => Parents.OfType<Group>().ToList().AsReadOnly();
    public ReadOnlyCollection<Role> RoleMemberships => Parents.OfType<Role>().ToList().AsReadOnly();

    public void AssignToRole(Role role)
    {
        role.AssignUser(this);
    }

    public void UnAssignFromRole(Role role)
    {
        role.UnAssignUser(this);
    }

    public void AddToGroup(Group group)
    {
        group.AddUser(this);
    }

    public void RemoveFromGroup(Group group)
    {
        group.RemoveUser(this);
    }
}