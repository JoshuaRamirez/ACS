using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using ACS.Service.Data;
using ACS.Service.Infrastructure;
using ACS.Service.Domain;
using Moq;
using DomainServices = ACS.Service.Services;

Console.WriteLine("Starting test...");

// Simple test to see if we can create the service
var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseInMemoryDatabase(databaseName: "TestDb")
    .Options;
    
using var dbContext = new ApplicationDbContext(options);
Console.WriteLine("DbContext created");

var logger = Mock.Of<ILogger<DomainServices.AccessControlDomainService>>();
var entityGraphLogger = Mock.Of<ILogger<InMemoryEntityGraph>>();

var entityGraph = new InMemoryEntityGraph(dbContext, entityGraphLogger);
Console.WriteLine("EntityGraph created");

var tenantConfig = new TenantConfiguration { TenantId = "test" };
var persistenceService = new DomainServices.TenantDatabasePersistenceService(
    dbContext, 
    Mock.Of<ILogger<DomainServices.TenantDatabasePersistenceService>>());
Console.WriteLine("PersistenceService created");

var eventPersistenceService = new DomainServices.EventPersistenceService(
    dbContext, 
    tenantConfig, 
    Mock.Of<ILogger<DomainServices.EventPersistenceService>>());
Console.WriteLine("EventPersistenceService created");

var deadLetterQueue = new DomainServices.DeadLetterQueueService(
    Mock.Of<ILogger<DomainServices.DeadLetterQueueService>>(), 
    tenantConfig);
Console.WriteLine("DeadLetterQueue created");

var errorRecovery = new DomainServices.ErrorRecoveryService(
    Mock.Of<ILogger<DomainServices.ErrorRecoveryService>>(), 
    tenantConfig);
Console.WriteLine("ErrorRecovery created");

var healthMonitoring = new DomainServices.HealthMonitoringService(
    Mock.Of<ILogger<DomainServices.HealthMonitoringService>>(), 
    errorRecovery, 
    tenantConfig);
Console.WriteLine("HealthMonitoring created");

var cacheMock = new Mock<ACS.Service.Caching.IEntityCache>();
cacheMock.Setup(x => x.SetUserAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
cacheMock.Setup(x => x.InvalidateGroupAsync(It.IsAny<int>())).Returns(Task.CompletedTask);
cacheMock.Setup(x => x.InvalidateRoleAsync(It.IsAny<int>())).Returns(Task.CompletedTask);
Console.WriteLine("Cache mock created");

var domainService = new DomainServices.AccessControlDomainService(
    entityGraph,
    dbContext,
    persistenceService,
    eventPersistenceService,
    deadLetterQueue,
    errorRecovery,
    healthMonitoring,
    cacheMock.Object,
    logger,
    startBackgroundProcessing: false);
Console.WriteLine("DomainService created successfully!");

// Try to create a user
var command = new DomainServices.CreateUserCommand { Name = "Test User" };

try
{
    Console.WriteLine("Executing command...");
    var task = domainService.ExecuteCommandAsync(command);
    Console.WriteLine("Task created, waiting...");
    
    if (await Task.WhenAny(task, Task.Delay(5000)) == task)
    {
        var result = await task;
        Console.WriteLine($"User created: {result.Name} with ID {result.Id}");
    }
    else
    {
        Console.WriteLine("Command execution timed out after 5 seconds");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
}

domainService.Dispose();
Console.WriteLine("Test completed!");