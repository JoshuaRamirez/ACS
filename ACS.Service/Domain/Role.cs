using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using ACS.Service.Delegates.Normalizers;
using ACS.Service.Domain.Validation;

namespace ACS.Service.Domain;

[LeastPrivilegeBusinessRule(
    ProhibitedCombinations = new[] { "/admin/users,/admin/system", "/finance/payments,/finance/approve" },
    RequiresJustification = new[] { "/admin/system", "/security/keys", "/config/secrets" })]
public class Role : Entity
{
    public ReadOnlyCollection<Group> GroupMemberships => Parents.OfType<Group>().ToList().AsReadOnly();
    public ReadOnlyCollection<User> Users => Children.OfType<User>().ToList().AsReadOnly();

    public void AssignUser(User user)
    {
        AddChild(user);
        // Note: Persistence should now be handled through proper service layer
        // AssignUserToRoleNormalizer.Execute(user.Id, this.Id);
    }

    public void UnAssignUser(User user)
    {
        RemoveChild(user);
        // Note: Persistence should now be handled through proper service layer
        // UnAssignUserFromRoleNormalizer.Execute(user.Id, this.Id);
    }

    public void AddToGroup(Group group)
    {

        group.AddRole(this);
    }

    public void RemoveFromGroup(Group group)
    {
        group.RemoveRole(this);
    }
}