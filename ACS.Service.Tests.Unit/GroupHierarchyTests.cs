using ACS.Service.Domain;

namespace ACS.Service.Tests.Unit;

[TestClass]
public class GroupHierarchyTests
{
    [TestMethod]
    public void AddGroup_AddsChildWhenValid()
    {
        var parent = new Group { Id = 1, Name = "Parent" };
        var child = new Group { Id = 2, Name = "Child" };

        parent.AddGroup(child);

        Assert.AreEqual(1, parent.Groups.Count);
        Assert.AreSame(child, parent.Groups.Single());
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void AddGroup_ThrowsWhenAddingSelf()
    {
        var group = new Group { Id = 3, Name = "Self" };
        group.AddGroup(group);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void AddGroup_ThrowsWhenCycleDetected()
    {
        var groupA = new Group { Id = 4, Name = "A" };
        var groupB = new Group { Id = 5, Name = "B" };
        var groupC = new Group { Id = 6, Name = "C" };

        groupA.AddGroup(groupB);
        groupB.AddGroup(groupC);

        groupC.AddGroup(groupA);
    }
}
