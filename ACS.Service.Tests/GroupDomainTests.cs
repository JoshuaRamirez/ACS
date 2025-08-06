using ACS.Service.Domain;
using ACS.Service.Delegates.Normalizers;
using GroupDM = ACS.Service.Data.Models.Group;
using UserDM = ACS.Service.Data.Models.User;
using RoleDM = ACS.Service.Data.Models.Role;

namespace ACS.Service.Tests
{
    [TestClass]
    public class GroupDomainTests
    {
        [TestMethod]
        public void AddGroup_ShouldAddGroupSuccessfully()
        {
            // Arrange
            var parentGroup = new Group { Id = 1, Name = "ParentGroup" };
            var childGroup = new Group { Id = 2, Name = "ChildGroup" };
            AddGroupToGroupNormalizer.Groups = new List<GroupDM>
            {
                new GroupDM { Id = 1, ChildGroups = new List<GroupDM>() },
                new GroupDM { Id = 2, ChildGroups = new List<GroupDM>() }
            };

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
            AddGroupToGroupNormalizer.Groups = new List<GroupDM>
            {
                new GroupDM { Id = 1, ChildGroups = new List<GroupDM>() },
                new GroupDM { Id = 2, ChildGroups = new List<GroupDM>() },
                new GroupDM { Id = 3, ChildGroups = new List<GroupDM>() }
            };

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
            var groups = new List<GroupDM>
            {
                new GroupDM { Id = 1, ChildGroups = new List<GroupDM>() },
                new GroupDM { Id = 2, ChildGroups = new List<GroupDM>() }
            };
            AddGroupToGroupNormalizer.Groups = groups;
            RemoveGroupFromGroupNormalizer.Groups = groups;

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
            var groups = new List<GroupDM> { new GroupDM { Id = 1, Users = new List<UserDM>() } };
            var users = new List<UserDM> { new UserDM { Id = 1 } };
            AddUserToGroupNormalizer.Groups = groups;
            AddUserToGroupNormalizer.Users = users;

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
            var groups = new List<GroupDM> { new GroupDM { Id = 1, Users = new List<UserDM>() } };
            var users = new List<UserDM> { new UserDM { Id = 1 } };
            AddUserToGroupNormalizer.Groups = groups;
            AddUserToGroupNormalizer.Users = users;
            RemoveUserFromGroupNormalizer.Groups = groups;
            RemoveUserFromGroupNormalizer.Users = users;

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
            var groups = new List<GroupDM> { new GroupDM { Id = 1, Roles = new List<RoleDM>() } };
            var roles = new List<RoleDM> { new RoleDM { Id = 1 } };
            AddRoleToGroupNormalizer.Groups = groups;
            AddRoleToGroupNormalizer.Roles = roles;

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
            var groups = new List<GroupDM> { new GroupDM { Id = 1, Roles = new List<RoleDM>() } };
            var roles = new List<RoleDM> { new RoleDM { Id = 1 } };
            AddRoleToGroupNormalizer.Groups = groups;
            AddRoleToGroupNormalizer.Roles = roles;
            RemoveRoleFromGroupNormalizer.Groups = groups;
            RemoveRoleFromGroupNormalizer.Roles = roles;

            group.AddRole(role);

            // Act
            group.RemoveRole(role);

            // Assert
            Assert.IsFalse(group.Roles.Contains(role));
            Assert.IsFalse(role.GroupMemberships.Contains(group));
        }
    }
}
