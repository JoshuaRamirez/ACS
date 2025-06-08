using ACS.Service.Data.Models;
using System.Reflection;

namespace ACS.Service.Tests.Integration;

[TestClass]
public class AddUserToGroupNormalizerTests
{
    [TestMethod]
    public void Execute_ShouldAddUserToGroupAndSetBackReference()
    {
        var user = new User { Id = 1 };
        var group = new Group { Id = 10, Users = new List<User>() };

        var normalizerType = Type.GetType("ACS.Service.Delegates.Normalizers.AddUserToGroupNormalizer, ACS.Service");
        Assert.IsNotNull(normalizerType, "Normalizer type should exist");

        normalizerType!.GetProperty("Users")!.SetValue(null, new List<User> { user });
        normalizerType.GetProperty("Groups")!.SetValue(null, new List<Group> { group });

        var method = normalizerType.GetMethod("Execute", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        method!.Invoke(null, new object[] { user.Id, group.Id });

        Assert.IsTrue(group.Users.Contains(user), "Group should contain user after execution");
        Assert.AreEqual(group, user.Group, "User should reference the group");
    }
}
