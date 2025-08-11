using System.Net.Http.Json;
using ACS.WebApi.Models.Groups;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ACS.WebApi.Tests.Integration;

[TestClass]
public class GroupsEndpointTests
{
    [TestMethod]
    public async Task GetGroups_ReturnsSuccess()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/groups");
        response.EnsureSuccessStatusCode();

        var groups = await response.Content.ReadFromJsonAsync<GroupsResponse>();
        Assert.IsNotNull(groups);
    }

    [TestMethod]
    public async Task PostGroup_CreatesGroup()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var request = new CreateGroupRequest
        {
            Group = new CreateGroupRequest.GroupResource
            {
                Name = "TestGroup",
            }
        };

        var response = await client.PostAsJsonAsync("/api/groups", request);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<GroupResponse>();
        Assert.IsNotNull(created);
        Assert.AreEqual("TestGroup", created.Group.Name);
    }
}
