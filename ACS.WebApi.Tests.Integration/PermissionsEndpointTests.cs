using System.Net.Http.Json;
using ACS.WebApi.Models.Permissions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ACS.WebApi.Tests.Integration;

[TestClass]
public class PermissionsEndpointTests
{
    [TestMethod]
    public async Task GetPermissions_ReturnsSuccess()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/permissions");
        response.EnsureSuccessStatusCode();

        var permissions = await response.Content.ReadFromJsonAsync<PermissionsResponse>();
        Assert.IsNotNull(permissions);
    }

    [TestMethod]
    public async Task PostPermission_CreatesPermission()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var request = new CreatePermissionRequest
        {
            Permission = new CreatePermissionRequest.PermissionResource
            {
                Uri = "/data",
                HttpVerb = "GET",
                Grant = true
            }
        };

        var response = await client.PostAsJsonAsync("/api/permissions", request);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<PermissionResponse>();
        Assert.IsNotNull(created);
        Assert.AreEqual("/data", created.Permission.Uri);
        Assert.AreEqual("GET", created.Permission.HttpVerb);
        Assert.IsTrue(created.Permission.Grant);
    }
}
