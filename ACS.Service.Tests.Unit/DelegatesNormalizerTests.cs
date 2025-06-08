using ACS.Service.Data.Models;
using ACS.Service.Delegates.Normalizers;

namespace ACS.Service.Tests.Unit;

[TestClass]
public class DelegatesNormalizerTests
{
    [TestMethod]
    public void AddUserToGroup_AddsUser()
    {
        var user = new User { Id = 1, Name = "Alice" };
        var group = new Group { Id = 10, Name = "Admins", Users = new List<User>() };

        AddUserToGroupNormalizer.Users = new List<User> { user };
        AddUserToGroupNormalizer.Groups = new List<Group> { group };

        AddUserToGroupNormalizer.Execute(1, 10);

        Assert.AreEqual(1, group.Users.Count);
        Assert.AreSame(user, group.Users.Single());
    }

    [TestMethod]
    public void RemoveUserFromGroup_RemovesExistingUser()
    {
        var user = new User { Id = 2, Name = "Bob" };
        var group = new Group { Id = 11, Name = "Users", Users = new List<User>{ user } };

        RemoveUserFromGroupNormalizer.Users = new List<User> { user };
        RemoveUserFromGroupNormalizer.Groups = new List<Group> { group };

        RemoveUserFromGroupNormalizer.Execute(2, 11);

        Assert.AreEqual(0, group.Users.Count);
    }

    [TestMethod]
    public void AddRoleToGroup_AddsRole()
    {
        var role = new Role { Id = 3, Name = "Admin" };
        var group = new Group { Id = 12, Name = "Admins", Roles = new List<Role>() };

        AddRoleToGroupNormalizer.Roles = new List<Role> { role };
        AddRoleToGroupNormalizer.Groups = new List<Group> { group };

        AddRoleToGroupNormalizer.Execute(3, 12);

        Assert.AreEqual(1, group.Roles.Count);
        Assert.AreSame(role, group.Roles.Single());
    }

    [TestMethod]
    public void RemoveRoleFromGroup_RemovesExistingRole()
    {
        var role = new Role { Id = 4, Name = "Member" };
        var group = new Group { Id = 13, Name = "Members", Roles = new List<Role>{ role } };

        RemoveRoleFromGroupNormalizer.Roles = new List<Role> { role };
        RemoveRoleFromGroupNormalizer.Groups = new List<Group> { group };

        RemoveRoleFromGroupNormalizer.Execute(4, 13);

        Assert.AreEqual(0, group.Roles.Count);
    }
}
