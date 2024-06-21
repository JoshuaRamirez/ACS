using ACS.Service.Domain;

namespace ACS.Service.Tests
{
    [TestClass]
    public class GroupTests
    {
        [TestMethod]
        public void AddGroup_ShouldAddGroupSuccessfully()
        {
            // Arrange
            var parentGroup = new Group { Id = 1, Name = "ParentGroup" };
            var childGroup = new Group { Id = 2, Name = "ChildGroup" };

            // Act
            parentGroup.AddGroup(childGroup);

            // Assert
            Assert.IsTrue(parentGroup.Groups.Contains(childGroup));
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void AddGroup_ShouldThrowException_WhenAddingGroupToItself()
        {
            // Arrange
            var group = new Group { Id = 1, Name = "Group" };

            // Act
            group.AddGroup(group);

            // Assert is handled by ExpectedException
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void AddGroup_ShouldThrowException_WhenCreatingCyclicalHierarchy()
        {
            // Arrange
            var parentGroup = new Group { Id = 1, Name = "ParentGroup" };
            var childGroup = new Group { Id = 2, Name = "ChildGroup" };
            var grandChildGroup = new Group { Id = 3, Name = "GrandChildGroup" };

            // Act
            parentGroup.AddGroup(childGroup);
            childGroup.AddGroup(grandChildGroup);
            grandChildGroup.AddGroup(parentGroup);

            // Assert is handled by ExpectedException
        }

        [TestMethod]
        public void RemoveGroup_ShouldRemoveGroupSuccessfully()
        {
            // Arrange
            var parentGroup = new Group { Id = 1, Name = "ParentGroup" };
            var childGroup = new Group { Id = 2, Name = "ChildGroup" };

            parentGroup.AddGroup(childGroup);

            // Act
            parentGroup.RemoveGroup(childGroup);

            // Assert
            Assert.IsFalse(parentGroup.Groups.Contains(childGroup));
        }

        [TestMethod]
        public void AddUser_ShouldAddUserSuccessfully()
        {
            // Arrange
            var group = new Group { Id = 1, Name = "Group" };
            var user = new Domain.User { Id = 1, Name = "User" };

            // Act
            group.AddUser(user);

            // Assert
            Assert.IsTrue(group.Users.Contains(user));
            Assert.IsTrue(user.GroupMemberships.Contains(group));
        }

        [TestMethod]
        public void RemoveUser_ShouldRemoveUserSuccessfully()
        {
            // Arrange
            var group = new Group { Id = 1, Name = "Group" };
            var user = new Domain.User { Id = 1, Name = "User" };

            group.AddUser(user);

            // Act
            group.RemoveUser(user);

            // Assert
            Assert.IsFalse(group.Users.Contains(user));
            Assert.IsFalse(user.GroupMemberships.Contains(group));
        }

        [TestMethod]
        public void AddRole_ShouldAddRoleSuccessfully()
        {
            // Arrange
            var group = new Group { Id = 1, Name = "Group" };
            var role = new Role { Id = 1, Name = "Role" };

            // Act
            group.AddRole(role);

            // Assert
            Assert.IsTrue(group.Roles.Contains(role));
            Assert.IsTrue(role.GroupMemberships.Contains(group));
        }

        [TestMethod]
        public void RemoveRole_ShouldRemoveRoleSuccessfully()
        {
            // Arrange
            var group = new Group { Id = 1, Name = "Group" };
            var role = new Role { Id = 1, Name = "Role" };

            group.AddRole(role);

            // Act
            group.RemoveRole(role);

            // Assert
            Assert.IsFalse(group.Roles.Contains(role));
            Assert.IsFalse(role.GroupMemberships.Contains(group));
        }
    }
}
