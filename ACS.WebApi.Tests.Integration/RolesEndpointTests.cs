using System.Net.Http.Json;
using ACS.WebApi.Models.Roles;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ACS.WebApi.Tests.Integration;

[TestClass]
public class RolesEndpointTests
{
    [TestMethod]
    public async Task GetRoles_ReturnsSuccess()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/roles");
        response.EnsureSuccessStatusCode();

        var roles = await response.Content.ReadFromJsonAsync<RolesResponse>();
        Assert.IsNotNull(roles);
    }

    [TestMethod]
    public async Task PostRole_CreatesRole()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var request = new CreateRoleRequest
        {
            Role = new CreateRoleRequest.RoleResource
            {
                Name = "TestRole",
            }
        };

        var response = await client.PostAsJsonAsync("/api/roles", request);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<RoleResponse>();
        Assert.IsNotNull(created);
        Assert.AreEqual("TestRole", created.Role.Name);
    }
}
