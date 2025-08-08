using System.Net.Http.Json;
using ACS.WebApi.Models.Users;
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

        var users = await response.Content.ReadFromJsonAsync<UsersResponse>();
        Assert.IsNotNull(users);
    }

    [TestMethod]
    public async Task PostUser_CreatesUser()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var request = new CreateUserRequest
        {
            User = new CreateUserRequest.UserResource
            {
                Name = "Test"
            }
        };

        var response = await client.PostAsJsonAsync("/api/users", request);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.IsNotNull(created);
        Assert.AreEqual("Test", created.User.Name);
    }
}
