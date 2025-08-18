using ACS.Service.Data.Models;
using ACS.Service.Delegates.Normalizers;
using DomainGroup = ACS.Service.Domain.Group;
using DomainRole = ACS.Service.Domain.Role;
using DomainUser = ACS.Service.Domain.User;
using ACS.Service.Domain;
using EntityDM = ACS.Service.Data.Models.Entity;
using RoleDM = ACS.Service.Data.Models.Role;
using UserDM = ACS.Service.Data.Models.User;
using GroupDM = ACS.Service.Data.Models.Group;

namespace ACS.Service.Tests.Unit;

[TestClass]
public class DomainIntegrationTests
{
    [TestMethod]
    public void Group_AddAndRemoveUser_UpdatesDomainAndDataModel()
    {
        var groups = new List<GroupDM> { new GroupDM { Id = 1, Users = new List<UserDM>() } };
        var users = new List<UserDM> { new UserDM { Id = 2 } };

        AddUserToGroupNormalizer.Groups = groups;
        AddUserToGroupNormalizer.Users = users;
        RemoveUserFromGroupNormalizer.Groups = groups;
        RemoveUserFromGroupNormalizer.Users = users;

        var group = new DomainGroup { Id = 1 };
        var user = new DomainUser { Id = 2 };

        group.AddUser(user);

        Assert.IsTrue(group.Users.Contains(user));
        Assert.IsTrue(user.GroupMemberships.Contains(group));
        Assert.IsTrue(groups[0].Users.Contains(users[0]));
        Assert.AreEqual(groups[0], users[0].Group);
        Assert.AreEqual(1, users[0].GroupId);

        group.RemoveUser(user);

        Assert.IsFalse(group.Users.Contains(user));
        Assert.IsFalse(user.GroupMemberships.Contains(group));
        Assert.IsFalse(groups[0].Users.Contains(users[0]));
        Assert.IsNull(users[0].Group);
        Assert.AreEqual(0, users[0].GroupId);
    }

    [TestMethod]
    public void Role_AssignAndUnassignUser_UpdatesDomainAndDataModel()
    {
        var roles = new List<RoleDM> { new RoleDM { Id = 1, Users = new List<UserDM>() } };
        var users = new List<UserDM> { new UserDM { Id = 2 } };

        AssignUserToRoleNormalizer.Roles = roles;
        AssignUserToRoleNormalizer.Users = users;
        UnAssignUserFromRoleNormalizer.Roles = roles;
        UnAssignUserFromRoleNormalizer.Users = users;

        var role = new DomainRole { Id = 1 };
        var user = new DomainUser { Id = 2 };

        role.AssignUser(user);

        Assert.IsTrue(role.Users.Contains(user));
        Assert.IsTrue(user.RoleMemberships.Contains(role));
        Assert.IsTrue(roles[0].Users.Contains(users[0]));
        Assert.AreEqual(roles[0], users[0].Role);
        Assert.AreEqual(1, users[0].RoleId);

        role.UnAssignUser(user);

        Assert.IsFalse(role.Users.Contains(user));
        Assert.IsFalse(user.RoleMemberships.Contains(role));
        Assert.IsFalse(roles[0].Users.Contains(users[0]));
        Assert.IsNull(users[0].Role);
        Assert.AreEqual(0, users[0].RoleId);
    }

    [TestMethod]
    public void Entity_AddAndRemovePermission_UpdatesDomainAndDataModel()
    {
        var entities = new List<EntityDM> { new EntityDM { Id = 1, EntityPermissions = new List<PermissionScheme>() } };
        var permissionSchemes = new List<PermissionScheme>();
        var resources = new List<Resource>();
        var uriAccesses = new List<UriAccess>();
        var schemeTypes = new List<SchemeType> { new SchemeType { Id = 1, SchemeName = "ApiUriAuthorization" } };
        var verbTypes = new List<VerbType> { new VerbType { Id = 1, VerbName = "GET" } };

        AddPermissionToEntity.PermissionSchemes = permissionSchemes;
        AddPermissionToEntity.Entities = entities;
        AddPermissionToEntity.Resources = resources;
        AddPermissionToEntity.UriAccessList = uriAccesses;
        AddPermissionToEntity.SchemeTypes = schemeTypes;

        CreatePermissionSchemeNormalizer.PermissionSchemes = permissionSchemes;
        CreateResourceNormalizer.Resources = resources;
        CreateUriAccessNormalizer.VerbTypes = verbTypes;
        CreateUriAccessNormalizer.UriAccessList = uriAccesses;

        RemovePermissionFromEntity.PermissionSchemes = permissionSchemes;
        RemovePermissionFromEntity.Entities = entities;
        RemovePermissionFromEntity.Resources = resources;
        RemovePermissionFromEntity.UriAccessList = uriAccesses;
        RemovePermissionFromEntity.SchemeTypes = schemeTypes;

        var role = new DomainRole { Id = 1 };
        var permission = new Permission
        {
            Uri = "/data",
            HttpVerb = HttpVerb.GET,
            Grant = true,
            Deny = false,
            Scheme = Scheme.ApiUriAuthorization
        };

        role.AddPermission(permission);

        Assert.IsTrue(role.HasPermission("/data", HttpVerb.GET));
        Assert.AreEqual(1, permissionSchemes.Count);
        Assert.AreEqual(1, resources.Count);
        Assert.AreEqual(1, uriAccesses.Count);

        role.RemovePermission(permission);

        Assert.IsFalse(role.HasPermission("/data", HttpVerb.GET));
        Assert.AreEqual(0, permissionSchemes.Count);
        Assert.AreEqual(1, resources.Count);
        Assert.AreEqual(0, uriAccesses.Count);
    }

    [TestMethod]
    public void Group_AddAndRemoveGroup_UpdatesDomainAndDataModel()
    {
        var groups = new List<GroupDM>
        {
            new GroupDM { Id = 1, ChildGroups = new List<GroupDM>(), Users = new List<UserDM>(), Roles = new List<RoleDM>() },
            new GroupDM { Id = 2, ChildGroups = new List<GroupDM>(), Users = new List<UserDM>(), Roles = new List<RoleDM>() }
        };

        AddGroupToGroupNormalizer.Groups = groups;
        RemoveGroupFromGroupNormalizer.Groups = groups;

        var parent = new DomainGroup { Id = 1 };
        var child = new DomainGroup { Id = 2 };

        parent.AddGroup(child);

        Assert.IsTrue(parent.Groups.Contains(child));
        Assert.IsTrue(child.ParentGroups.Contains(parent));
        Assert.IsTrue(groups[0].ChildGroups.Contains(groups[1]));
        Assert.AreEqual(groups[0], groups[1].ParentGroup);
        Assert.AreEqual(1, groups[1].ParentGroupId);

        parent.RemoveGroup(child);

        Assert.IsFalse(parent.Groups.Contains(child));
        Assert.IsFalse(child.ParentGroups.Contains(parent));
        Assert.IsFalse(groups[0].ChildGroups.Contains(groups[1]));
        Assert.IsNull(groups[1].ParentGroup);
        Assert.AreEqual(0, groups[1].ParentGroupId);
    }

    [TestMethod]
    public void Group_AddToGroupAndRemoveFromGroup_UpdatesDomainAndDataModel()
    {
        var groups = new List<GroupDM>
        {
            new GroupDM { Id = 1, ChildGroups = new List<GroupDM>(), Users = new List<UserDM>(), Roles = new List<RoleDM>() },
            new GroupDM { Id = 2, ChildGroups = new List<GroupDM>(), Users = new List<UserDM>(), Roles = new List<RoleDM>() }
        };

        AddGroupToGroupNormalizer.Groups = groups;
        RemoveGroupFromGroupNormalizer.Groups = groups;

        var parent = new DomainGroup { Id = 1 };
        var child = new DomainGroup { Id = 2 };

        child.AddToGroup(parent);

        Assert.IsTrue(parent.Groups.Contains(child));
        Assert.IsTrue(child.ParentGroups.Contains(parent));
        Assert.IsTrue(groups[0].ChildGroups.Contains(groups[1]));
        Assert.AreEqual(groups[0], groups[1].ParentGroup);
        Assert.AreEqual(1, groups[1].ParentGroupId);

        child.RemoveFromGroup(parent);

        Assert.IsFalse(parent.Groups.Contains(child));
        Assert.IsFalse(child.ParentGroups.Contains(parent));
        Assert.IsFalse(groups[0].ChildGroups.Contains(groups[1]));
        Assert.IsNull(groups[1].ParentGroup);
        Assert.AreEqual(0, groups[1].ParentGroupId);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Group_AddGroup_PreventsCycles()
    {
        var groups = new List<GroupDM>
        {
            new GroupDM { Id = 1, ChildGroups = new List<GroupDM>(), Users = new List<UserDM>(), Roles = new List<RoleDM>() },
            new GroupDM { Id = 2, ChildGroups = new List<GroupDM>(), Users = new List<UserDM>(), Roles = new List<RoleDM>() }
        };

        AddGroupToGroupNormalizer.Groups = groups;

        var parent = new DomainGroup { Id = 1 };
        var child = new DomainGroup { Id = 2 };

        parent.AddGroup(child);
        // Attempting to create a cycle should throw
        parent.AddToGroup(child);
    }
}
