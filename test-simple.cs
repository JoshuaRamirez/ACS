using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using ACS.Service.Data;
using ACS.Service.Infrastructure;
using ACS.Service.Domain;
using Moq;
using DomainServices = ACS.Service.Services;

// Simple test to see if we can create the service
var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseInMemoryDatabase(databaseName: "TestDb")
    .Options;
    
using var dbContext = new ApplicationDbContext(options);

var logger = Mock.Of<ILogger<DomainServices.AccessControlDomainService>>();
var entityGraphLogger = Mock.Of<ILogger<InMemoryEntityGraph>>();

var entityGraph = new InMemoryEntityGraph(dbContext, entityGraphLogger);

var tenantConfig = new TenantConfiguration { TenantId = "test" };
var persistenceService = new DomainServices.TenantDatabasePersistenceService(
    dbContext, 
    Mock.Of<ILogger<DomainServices.TenantDatabasePersistenceService>>());

var eventPersistenceService = new DomainServices.EventPersistenceService(
    dbContext, 
    tenantConfig, 
    Mock.Of<ILogger<DomainServices.EventPersistenceService>>());

var deadLetterQueue = new DomainServices.DeadLetterQueueService(
    Mock.Of<ILogger<DomainServices.DeadLetterQueueService>>(), 
    tenantConfig);

var errorRecovery = new DomainServices.ErrorRecoveryService(
    Mock.Of<ILogger<DomainServices.ErrorRecoveryService>>(), 
    tenantConfig);

var healthMonitoring = new DomainServices.HealthMonitoringService(
    Mock.Of<ILogger<DomainServices.HealthMonitoringService>>(), 
    errorRecovery, 
    tenantConfig);

var cacheMock = new Mock<ACS.Service.Caching.IEntityCache>();
cacheMock.Setup(x => x.SetUserAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

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

Console.WriteLine("Service created successfully!");

// Try to create a user
var command = new DomainServices.CreateUserCommand { Name = "Test User" };

try
{
    Console.WriteLine("Executing command...");
    var result = await domainService.ExecuteCommandAsync(command);
    Console.WriteLine($"User created: {result.Name} with ID {result.Id}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
}

domainService.Dispose();
Console.WriteLine("Test completed!");