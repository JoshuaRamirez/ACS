using System;
using ACS.Service.Data.Models;
using ACS.Service.Delegates.Normalizers;
using ACS.Service.Domain;
using EntityDM = ACS.Service.Data.Models.Entity;

namespace ACS.Service.Tests.Unit;

[TestClass]
public class NormalizersTests_Phase2
{
    // Phase 2 Note: This test file contains updated tests for Phase 2 domain-first approach
    // The original NormalizersTests.cs file needs extensive updates to work with Domain objects
    
    [TestMethod]
    public void AddPermissionAndRemovePermission_NormalizersUpdateCollections()
    {
        // Permission normalizers still work with Data models (database layer)
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

    // LEGACY TEST - COMMENTED OUT: Static collection pattern replaced with async database-backed normalizers
    /*
    [TestMethod]
    public void Phase2_DomainRelationshipNormalizers_WorkWithDomainObjects()
    {
        // Test that relationship normalizers now work with Domain objects (Phase 2 requirement)
        var domainUsers = new List<ACS.Service.Domain.User>
        {
            new ACS.Service.Domain.User { Id = 1, Name = "User1", Parents = new List<ACS.Service.Domain.Entity>(), Children = new List<ACS.Service.Domain.Entity>(), Permissions = new List<Permission>() }
        };
        
        var domainGroups = new List<ACS.Service.Domain.Group>
        {
            new ACS.Service.Domain.Group { Id = 2, Name = "Group1", Parents = new List<ACS.Service.Domain.Entity>(), Children = new List<ACS.Service.Domain.Entity>(), Permissions = new List<Permission>() }
        };

        // Test that normalizers accept Domain object collections
        AddUserToGroupNormalizer.Users = domainUsers;
        AddUserToGroupNormalizer.Groups = domainGroups;

        AddUserToGroupNormalizer.Execute(1, 2);

        // Verify domain relationships were updated
        Assert.IsTrue(domainGroups[0].Children.Contains(domainUsers[0]));
        Assert.IsTrue(domainUsers[0].Parents.Contains(domainGroups[0]));
    }
    */

    // LEGACY TEST - COMMENTED OUT: Static collection pattern replaced with async database-backed normalizers
    /*
    [TestMethod]
    public void Phase2_DomainNormalizersValidation_ThrowsOnNullCollections()
    {
        // Test that normalizers throw when collections are not initialized (Phase 2 safety)
        AddUserToGroupNormalizer.Users = null!;
        AddUserToGroupNormalizer.Groups = null!;

        Assert.ThrowsException<InvalidOperationException>(() => AddUserToGroupNormalizer.Execute(1, 2));
    }
    */
}