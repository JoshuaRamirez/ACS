using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ACS.WebApi.Tests.Integration;

[TestClass]
public class UsersEndpointTests
{
    [TestMethod]
    public async Task GetUsers_ReturnsSuccess()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/users");
        response.EnsureSuccessStatusCode();
    }

    [TestMethod]
    public async Task PostUser_CreatesUser()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var newUser = new ACS.Service.Domain.User { Name = "Test" };
        var response = await client.PostAsJsonAsync("/api/users", newUser);
        response.EnsureSuccessStatusCode();
    }
}
