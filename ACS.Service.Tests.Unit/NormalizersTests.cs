using System;
using ACS.Service.Data.Models;
using ACS.Service.Delegates.Normalizers;
using ACS.Service.Domain;
using EntityDM = ACS.Service.Data.Models.Entity;
using RoleDM = ACS.Service.Data.Models.Role;
using UserDM = ACS.Service.Data.Models.User;
using GroupDM = ACS.Service.Data.Models.Group;

namespace ACS.Service.Tests.Unit;

[TestClass]
public class NormalizersTests
{
    [TestMethod]
    public void AddPermissionAndRemovePermission_NormalizersUpdateCollections()
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

        var permission = new Permission
        {
            Uri = "/data",
            HttpVerb = HttpVerb.GET,
            Grant = true,
            Deny = false,
            Scheme = Scheme.ApiUriAuthorization
        };

        AddPermissionToEntity.Execute(permission, 1);

        Assert.AreEqual(1, permissionSchemes.Count);
        Assert.AreEqual(1, resources.Count);
        Assert.AreEqual(1, uriAccesses.Count);
        Assert.AreEqual(permissionSchemes[0], entities[0].EntityPermissions.First());

        RemovePermissionFromEntity.PermissionSchemes = permissionSchemes;
        RemovePermissionFromEntity.Entities = entities;
        RemovePermissionFromEntity.Resources = resources;
        RemovePermissionFromEntity.UriAccessList = uriAccesses;
        RemovePermissionFromEntity.SchemeTypes = schemeTypes;

        RemovePermissionFromEntity.Execute(permission, 1);

        Assert.AreEqual(0, uriAccesses.Count);
        Assert.AreEqual(0, permissionSchemes.Count);
        Assert.AreEqual(0, entities[0].EntityPermissions.Count);
    }

    [TestMethod]
    public void AssignUserToRole_NormalizerSetsBothSides()
    {
        var roles = new List<RoleDM> { new RoleDM { Id = 1, Users = new List<UserDM>() } };
        var users = new List<UserDM> { new UserDM { Id = 2 } };

        AssignUserToRoleNormalizer.Roles = roles;
        AssignUserToRoleNormalizer.Users = users;

        AssignUserToRoleNormalizer.Execute(2, 1);

        Assert.IsTrue(roles[0].Users.Contains(users[0]));
        Assert.AreEqual(roles[0], users[0].Role);
        Assert.AreEqual(1, users[0].RoleId);
    }

    [TestMethod]
    public void AddAndRemoveUserFromGroup_NormalizersUpdateCollections()
    {
        var groups = new List<GroupDM> { new GroupDM { Id = 1, Users = new List<UserDM>() } };
        var users = new List<UserDM> { new UserDM { Id = 2 } };

        AddUserToGroupNormalizer.Groups = groups;
        AddUserToGroupNormalizer.Users = users;

        AddUserToGroupNormalizer.Execute(2, 1);

        Assert.IsTrue(groups[0].Users.Contains(users[0]));
        Assert.AreEqual(groups[0], users[0].Group);
        Assert.AreEqual(1, users[0].GroupId);

        RemoveUserFromGroupNormalizer.Groups = groups;
        RemoveUserFromGroupNormalizer.Users = users;

        RemoveUserFromGroupNormalizer.Execute(2, 1);

        Assert.IsFalse(groups[0].Users.Contains(users[0]));
        Assert.IsNull(users[0].Group);
        Assert.AreEqual(0, users[0].GroupId);
    }

    [TestMethod]
    public void AddAndRemoveRoleFromGroup_NormalizersUpdateCollections()
    {
        var groups = new List<GroupDM> { new GroupDM { Id = 1, Roles = new List<RoleDM>() } };
        var roles = new List<RoleDM> { new RoleDM { Id = 3 } };

        AddRoleToGroupNormalizer.Groups = groups;
        AddRoleToGroupNormalizer.Roles = roles;

        AddRoleToGroupNormalizer.Execute(3, 1);

        Assert.IsTrue(groups[0].Roles.Contains(roles[0]));
        Assert.AreEqual(groups[0], roles[0].Group);
        Assert.AreEqual(1, roles[0].GroupId);

        RemoveRoleFromGroupNormalizer.Groups = groups;
        RemoveRoleFromGroupNormalizer.Roles = roles;

        RemoveRoleFromGroupNormalizer.Execute(3, 1);

        Assert.IsFalse(groups[0].Roles.Contains(roles[0]));
        Assert.IsNull(roles[0].Group);
        Assert.AreEqual(0, roles[0].GroupId);
    }

    [TestMethod]
    public void UnAssignUserFromRole_NormalizerClearsBothSides()
    {
        var roles = new List<RoleDM> { new RoleDM { Id = 1, Users = new List<UserDM>() } };
        var users = new List<UserDM> { new UserDM { Id = 2, RoleId = 1, Role = roles[0] } };
        roles[0].Users.Add(users[0]);

        UnAssignUserFromRoleNormalizer.Roles = roles;
        UnAssignUserFromRoleNormalizer.Users = users;

        UnAssignUserFromRoleNormalizer.Execute(2, 1);

        Assert.IsFalse(roles[0].Users.Contains(users[0]));
        Assert.IsNull(users[0].Role);
        Assert.AreEqual(0, users[0].RoleId);
    }

    [TestMethod]
    public void AddAndRemoveGroupFromGroup_NormalizersUpdateCollections()
    {
        var groups = new List<GroupDM>
        {
            new GroupDM { Id = 1, ChildGroups = new List<GroupDM>() },
            new GroupDM { Id = 2, ChildGroups = new List<GroupDM>() }
        };

        AddGroupToGroupNormalizer.Groups = groups;
        RemoveGroupFromGroupNormalizer.Groups = groups;

        AddGroupToGroupNormalizer.Execute(2, 1);

        Assert.IsTrue(groups[0].ChildGroups.Contains(groups[1]));
        Assert.AreEqual(groups[0], groups[1].ParentGroup);
        Assert.AreEqual(1, groups[1].ParentGroupId);

        RemoveGroupFromGroupNormalizer.Execute(2, 1);

        Assert.IsFalse(groups[0].ChildGroups.Contains(groups[1]));
        Assert.IsNull(groups[1].ParentGroup);
        Assert.AreEqual(0, groups[1].ParentGroupId);
    }

    [TestMethod]
    public void AddGroupToGroupNormalizer_ThrowsWhenChildMissing()
    {
        var groups = new List<GroupDM> { new GroupDM { Id = 1, ChildGroups = new List<GroupDM>() } };
        AddGroupToGroupNormalizer.Groups = groups;
        Assert.ThrowsException<InvalidOperationException>(() => AddGroupToGroupNormalizer.Execute(2, 1));
    }

    [TestMethod]
    public void AddRoleToGroupNormalizer_ThrowsWhenGroupMissing()
    {
        var roles = new List<RoleDM> { new RoleDM { Id = 3 } };
        AddRoleToGroupNormalizer.Groups = new List<GroupDM>();
        AddRoleToGroupNormalizer.Roles = roles;
        Assert.ThrowsException<InvalidOperationException>(() => AddRoleToGroupNormalizer.Execute(3, 1));
    }

    [TestMethod]
    public void AddUserToGroupNormalizer_ThrowsWhenUserMissing()
    {
        var groups = new List<GroupDM> { new GroupDM { Id = 1, Users = new List<UserDM>() } };
        AddUserToGroupNormalizer.Groups = groups;
        AddUserToGroupNormalizer.Users = new List<UserDM>();
        Assert.ThrowsException<InvalidOperationException>(() => AddUserToGroupNormalizer.Execute(2, 1));
    }

    [TestMethod]
    public void RemoveGroupFromGroupNormalizer_ThrowsWhenNotMember()
    {
        var groups = new List<GroupDM>
        {
            new GroupDM { Id = 1, ChildGroups = new List<GroupDM>() },
            new GroupDM { Id = 2, ChildGroups = new List<GroupDM>() }
        };

        RemoveGroupFromGroupNormalizer.Groups = groups;
        Assert.ThrowsException<InvalidOperationException>(() => RemoveGroupFromGroupNormalizer.Execute(2, 1));
    }

    [TestMethod]
    public void RemoveRoleFromGroupNormalizer_ThrowsWhenNotMember()
    {
        var groups = new List<GroupDM> { new GroupDM { Id = 1, Roles = new List<RoleDM>() } };
        var roles = new List<RoleDM> { new RoleDM { Id = 3 } };

        RemoveRoleFromGroupNormalizer.Groups = groups;
        RemoveRoleFromGroupNormalizer.Roles = roles;

        Assert.ThrowsException<InvalidOperationException>(() => RemoveRoleFromGroupNormalizer.Execute(3, 1));
    }

    [TestMethod]
    public void RemoveUserFromGroupNormalizer_ThrowsWhenNotMember()
    {
        var groups = new List<GroupDM> { new GroupDM { Id = 1, Users = new List<UserDM>() } };
        var users = new List<UserDM> { new UserDM { Id = 2 } };

        RemoveUserFromGroupNormalizer.Groups = groups;
        RemoveUserFromGroupNormalizer.Users = users;

        Assert.ThrowsException<InvalidOperationException>(() => RemoveUserFromGroupNormalizer.Execute(2, 1));
    }

    [TestMethod]
    public void AddGroupToGroupNormalizer_ThrowsWhenGroupsNull()
    {
        AddGroupToGroupNormalizer.Groups = null!;
        Assert.ThrowsException<InvalidOperationException>(() => AddGroupToGroupNormalizer.Execute(2, 1));
    }

    [TestMethod]
    public void AddRoleToGroupNormalizer_ThrowsWhenRolesNull()
    {
        AddRoleToGroupNormalizer.Groups = new List<GroupDM>();
        AddRoleToGroupNormalizer.Roles = null!;
        Assert.ThrowsException<InvalidOperationException>(() => AddRoleToGroupNormalizer.Execute(3, 1));
    }

    [TestMethod]
    public void AddUserToGroupNormalizer_ThrowsWhenUsersNull()
    {
        AddUserToGroupNormalizer.Groups = new List<GroupDM>();
        AddUserToGroupNormalizer.Users = null!;
        Assert.ThrowsException<InvalidOperationException>(() => AddUserToGroupNormalizer.Execute(2, 1));
    }

    [TestMethod]
    public void RemoveGroupFromGroupNormalizer_ThrowsWhenGroupsNull()
    {
        RemoveGroupFromGroupNormalizer.Groups = null!;
        Assert.ThrowsException<InvalidOperationException>(() => RemoveGroupFromGroupNormalizer.Execute(2, 1));
    }

    [TestMethod]
    public void RemoveRoleFromGroupNormalizer_ThrowsWhenRolesNull()
    {
        RemoveRoleFromGroupNormalizer.Groups = new List<GroupDM>();
        RemoveRoleFromGroupNormalizer.Roles = null!;
        Assert.ThrowsException<InvalidOperationException>(() => RemoveRoleFromGroupNormalizer.Execute(3, 1));
    }

    [TestMethod]
    public void RemoveUserFromGroupNormalizer_ThrowsWhenUsersNull()
    {
        RemoveUserFromGroupNormalizer.Groups = new List<GroupDM>();
        RemoveUserFromGroupNormalizer.Users = null!;
        Assert.ThrowsException<InvalidOperationException>(() => RemoveUserFromGroupNormalizer.Execute(2, 1));
    }
}
