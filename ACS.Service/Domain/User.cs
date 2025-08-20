using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using ACS.Service.Delegates.Normalizers;
using ACS.Service.Domain.Validation;

namespace ACS.Service.Domain;

[MaxUserRolesBusinessRule(5)]
[SegregationOfDutiesBusinessRule("Administrator")]
[DataRetentionBusinessRule(RequiresConsentForStorage = true, PersonalDataFields = new[] { "Name", "Email" })]
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