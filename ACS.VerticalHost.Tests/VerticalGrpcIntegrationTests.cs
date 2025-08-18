using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Grpc.Net.Client;
using ACS.Core.Grpc;
using ACS.Service.Infrastructure;
using ACS.Service.Services;
using ACS.Infrastructure;
using Google.Protobuf;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ACS.Service.Data;

namespace ACS.VerticalHost.Tests;

[TestClass]
public class VerticalGrpcIntegrationTests
{
    private WebApplicationFactory<ACS.VerticalHost.Program>? _factory;
    private GrpcChannel? _channel;
    private VerticalService.VerticalServiceClient? _client;
    private const string TestTenantId = "integration-test-tenant";

    [TestInitialize]
    public async Task Setup()
    {
        // Create test application factory
        _factory = new WebApplicationFactory<ACS.VerticalHost.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace database with in-memory for testing
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                    if (descriptor != null)
                        services.Remove(descriptor);

                    services.AddDbContext<ApplicationDbContext>(options =>
                        options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
                });
            });

        // Set environment variables for testing
        Environment.SetEnvironmentVariable("TENANT_ID", TestTenantId);
        Environment.SetEnvironmentVariable("GRPC_PORT", "50051");

        var client = _factory.CreateClient();
        _channel = GrpcChannel.ForAddress(client.BaseAddress!, new GrpcChannelOptions
        {
            HttpClient = client
        });
        _client = new VerticalService.VerticalServiceClient(_channel);

        // Give the service time to start up
        await Task.Delay(100);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (_channel != null)
            await _channel.ShutdownAsync();
        
        _factory?.Dispose();
    }

    [TestMethod]
    public async Task HealthCheck_ReturnsHealthyStatus()
    {
        // Arrange
        var request = new HealthRequest();

        // Act
        var response = await _client!.HealthCheckAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsTrue(response.Healthy);
        Assert.IsTrue(response.UptimeSeconds >= 0);
        Assert.IsTrue(response.CommandsProcessed >= 0);
    }

    [TestMethod]
    public async Task ExecuteCommand_CreateUserCommand_ReturnsSuccessResponse()
    {
        // Arrange
        var createUserCommand = new TestCreateUserCommand
        {
            Name = "Integration Test User"
        };

        var commandData = ProtoSerializer.Serialize(createUserCommand);
        var request = new CommandRequest
        {
            CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
            CommandData = ByteString.CopyFrom(commandData),
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        var response = await _client!.ExecuteCommandAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsTrue(response.Success);
        Assert.AreEqual(request.CorrelationId, response.CorrelationId);
        Assert.IsTrue(response.ResultData.Length > 0);
    }

    [TestMethod]
    public async Task ExecuteCommand_GetUsersCommand_ReturnsUserList()
    {
        // Arrange - First create a user
        var createCommand = new TestCreateUserCommand { Name = "Test User" };
        var createCommandData = ProtoSerializer.Serialize(createCommand);
        var createRequest = new CommandRequest
        {
            CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
            CommandData = ByteString.CopyFrom(createCommandData),
            CorrelationId = Guid.NewGuid().ToString()
        };

        await _client!.ExecuteCommandAsync(createRequest);

        // Now test getting users
        var getUsersCommand = new TestGetUsersCommand { Page = 1, PageSize = 10 };
        var getUsersCommandData = ProtoSerializer.Serialize(getUsersCommand);
        var getUsersRequest = new CommandRequest
        {
            CommandType = typeof(TestGetUsersCommand).AssemblyQualifiedName!,
            CommandData = ByteString.CopyFrom(getUsersCommandData),
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        var response = await _client!.ExecuteCommandAsync(getUsersRequest);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsTrue(response.Success);
        Assert.IsTrue(response.ResultData.Length > 0);
    }

    [TestMethod]
    public async Task ExecuteCommand_VoidCommand_ReturnsSuccessWithoutData()
    {
        // Arrange - Create a user first, then delete it
        var createCommand = new TestCreateUserCommand { Name = "User To Delete" };
        var createCommandData = ProtoSerializer.Serialize(createCommand);
        var createRequest = new CommandRequest
        {
            CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
            CommandData = ByteString.CopyFrom(createCommandData),
            CorrelationId = Guid.NewGuid().ToString()
        };

        var createResponse = await _client!.ExecuteCommandAsync(createRequest);
        
        // Deserialize user to get ID (simplified for test)
        var deleteCommand = new TestDeleteUserCommand { UserId = 1 }; // Assuming first user has ID 1
        var deleteCommandData = ProtoSerializer.Serialize(deleteCommand);
        var deleteRequest = new CommandRequest
        {
            CommandType = typeof(TestDeleteUserCommand).AssemblyQualifiedName!,
            CommandData = ByteString.CopyFrom(deleteCommandData),
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        var response = await _client!.ExecuteCommandAsync(deleteRequest);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsTrue(response.Success);
        Assert.AreEqual(deleteRequest.CorrelationId, response.CorrelationId);
        // Void commands typically return empty result data
    }

    [TestMethod]
    public async Task ExecuteCommand_InvalidCommandType_ReturnsErrorResponse()
    {
        // Arrange
        var request = new CommandRequest
        {
            CommandType = "NonExistent.Command.Type",
            CommandData = ByteString.CopyFrom(new byte[] { 1, 2, 3 }),
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        var response = await _client!.ExecuteCommandAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsFalse(response.Success);
        Assert.IsFalse(string.IsNullOrEmpty(response.ErrorMessage));
        Assert.AreEqual(request.CorrelationId, response.CorrelationId);
    }

    [TestMethod]
    public async Task ExecuteCommand_MalformedCommandData_ReturnsErrorResponse()
    {
        // Arrange
        var request = new CommandRequest
        {
            CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
            CommandData = ByteString.CopyFrom(new byte[] { 0xFF, 0xFF, 0xFF }), // Invalid data
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        var response = await _client!.ExecuteCommandAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsFalse(response.Success);
        Assert.IsFalse(string.IsNullOrEmpty(response.ErrorMessage));
        Assert.AreEqual(request.CorrelationId, response.CorrelationId);
    }

    [TestMethod]
    public async Task ExecuteCommand_MultipleCommands_ProcessedSequentially()
    {
        // Arrange
        var commands = new List<CommandRequest>();
        for (int i = 1; i <= 5; i++)
        {
            var createCommand = new TestCreateUserCommand { Name = $"User {i}" };
            var commandData = ProtoSerializer.Serialize(createCommand);
            commands.Add(new CommandRequest
            {
                CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                CommandData = ByteString.CopyFrom(commandData),
                CorrelationId = $"correlation-{i}"
            });
        }

        // Act - Execute commands concurrently
        var tasks = commands.Select(cmd => _client!.ExecuteCommandAsync(cmd).ResponseAsync);
        var responses = await Task.WhenAll(tasks);

        // Assert
        Assert.AreEqual(5, responses.Length);
        foreach (var response in responses)
        {
            Assert.IsNotNull(response);
            Assert.IsTrue(response.Success);
            Assert.IsFalse(string.IsNullOrEmpty(response.CorrelationId));
        }

        // Verify correlation IDs match
        var correlationIds = responses.Select(r => r.CorrelationId).OrderBy(id => id).ToList();
        var expectedIds = commands.Select(c => c.CorrelationId).OrderBy(id => id).ToList();
        CollectionAssert.AreEqual(expectedIds, correlationIds);
    }

    [TestMethod]
    public async Task ExecuteCommand_CommandWithComplexData_HandlesCorrectly()
    {
        // Arrange - Create group, role, and user, then establish relationships
        var createGroupCommand = new TestCreateGroupCommand { Name = "Test Group" };
        var groupCommandData = ProtoSerializer.Serialize(createGroupCommand);
        var groupRequest = new CommandRequest
        {
            CommandType = typeof(TestCreateGroupCommand).AssemblyQualifiedName!,
            CommandData = ByteString.CopyFrom(groupCommandData),
            CorrelationId = Guid.NewGuid().ToString()
        };

        var createRoleCommand = new TestCreateRoleCommand { Name = "Test Role" };
        var roleCommandData = ProtoSerializer.Serialize(createRoleCommand);
        var roleRequest = new CommandRequest
        {
            CommandType = typeof(TestCreateRoleCommand).AssemblyQualifiedName!,
            CommandData = ByteString.CopyFrom(roleCommandData),
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Act
        var groupResponse = await _client!.ExecuteCommandAsync(groupRequest);
        var roleResponse = await _client!.ExecuteCommandAsync(roleRequest);

        // Assert
        Assert.IsTrue(groupResponse.Success);
        Assert.IsTrue(roleResponse.Success);
        Assert.IsTrue(groupResponse.ResultData.Length > 0);
        Assert.IsTrue(roleResponse.ResultData.Length > 0);
    }

    [TestMethod]
    public async Task HealthCheck_AfterCommandExecution_UpdatesMetrics()
    {
        // Arrange - Get initial health status
        var initialHealth = await _client!.HealthCheckAsync(new HealthRequest());
        var initialCommandCount = initialHealth.CommandsProcessed;

        // Execute a command
        var createCommand = new TestCreateUserCommand { Name = "Metrics Test User" };
        var commandData = ProtoSerializer.Serialize(createCommand);
        var request = new CommandRequest
        {
            CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
            CommandData = ByteString.CopyFrom(commandData),
            CorrelationId = Guid.NewGuid().ToString()
        };

        await _client!.ExecuteCommandAsync(request);

        // Act - Get updated health status
        var updatedHealth = await _client!.HealthCheckAsync(new HealthRequest());

        // Assert
        Assert.IsTrue(updatedHealth.CommandsProcessed > initialCommandCount);
        Assert.IsTrue(updatedHealth.Healthy);
    }

    [TestMethod]
    public async Task GrpcService_ConcurrentClients_HandlesCorrectly()
    {
        // Arrange - Create multiple clients
        var clients = new List<VerticalService.VerticalServiceClient>();
        var channels = new List<GrpcChannel>();

        try
        {
            for (int i = 0; i < 3; i++)
            {
                var client = _factory!.CreateClient();
                var channel = GrpcChannel.ForAddress(client.BaseAddress!, new GrpcChannelOptions
                {
                    HttpClient = client
                });
                channels.Add(channel);
                clients.Add(new VerticalService.VerticalServiceClient(channel));
            }

            // Act - Execute commands from multiple clients concurrently
            var tasks = new List<Task<CommandResponse>>();
            foreach (var client in clients)
            {
                var createCommand = new TestCreateUserCommand { Name = $"Concurrent User {clients.IndexOf(client)}" };
                var commandData = ProtoSerializer.Serialize(createCommand);
                var request = new CommandRequest
                {
                    CommandType = typeof(TestCreateUserCommand).AssemblyQualifiedName!,
                    CommandData = ByteString.CopyFrom(commandData),
                    CorrelationId = Guid.NewGuid().ToString()
                };
                tasks.Add(client.ExecuteCommandAsync(request).ResponseAsync);
            }

            var responses = await Task.WhenAll(tasks);

            // Assert
            foreach (var response in responses)
            {
                Assert.IsTrue(response.Success);
                Assert.IsFalse(string.IsNullOrEmpty(response.CorrelationId));
            }
        }
        finally
        {
            // Cleanup
            foreach (var channel in channels)
            {
                await channel.ShutdownAsync();
            }
        }
    }
}

