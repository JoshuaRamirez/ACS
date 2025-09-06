using ACS.Service.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace ACS.Service.Tests.Unit;

/// <summary>
/// Unit tests for Service Layer Dependency Injection
/// Tests service registration patterns, lifetimes, and dependency resolution
/// </summary>
[TestClass]
public class ServiceCollectionExtensionsTests
{
    private IServiceCollection _services = null!;
    private ServiceProvider _serviceProvider = null!;

    [TestInitialize]
    public void Setup()
    {
        // Arrange
        _services = new ServiceCollection();
        
        // Add required base services
        _services.AddLogging();
        _services.AddMemoryCache();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _serviceProvider?.Dispose();
    }

    #region Service Registration Tests

    [TestMethod]
    public void ServiceDI_ManualServiceRegistration_RegistersAuditService()
    {
        // Arrange
        _services.AddDbContext<ACS.Service.Data.ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        // Act
        _services.AddScoped<IAuditService, AuditService>();
        _serviceProvider = _services.BuildServiceProvider();

        // Assert
        var auditService = _serviceProvider.GetService<IAuditService>();
        Assert.IsNotNull(auditService);
        Assert.IsInstanceOfType<AuditService>(auditService);
    }

    [TestMethod]
    public void ServiceDI_ManualServiceRegistration_RegistersSystemMetricsService()
    {
        // Arrange
        _services.AddDbContext<ACS.Service.Data.ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        _services.AddSingleton<ACS.Service.Infrastructure.InMemoryEntityGraph>();

        // Act
        _services.AddScoped<ISystemMetricsService, SystemMetricsService>();
        _serviceProvider = _services.BuildServiceProvider();

        // Assert
        var systemMetricsService = _serviceProvider.GetService<ISystemMetricsService>();
        Assert.IsNotNull(systemMetricsService);
        Assert.IsInstanceOfType<SystemMetricsService>(systemMetricsService);
    }

    [TestMethod]
    public void ServiceDI_ServiceLifetimes_ScopedServicesCreateNewInstances()
    {
        // Arrange
        _services.AddDbContext<ACS.Service.Data.ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        _services.AddScoped<IAuditService, AuditService>();
        _serviceProvider = _services.BuildServiceProvider();

        // Act
        var scope1 = _serviceProvider.CreateScope();
        var scope2 = _serviceProvider.CreateScope();

        var auditService1 = scope1.ServiceProvider.GetRequiredService<IAuditService>();
        var auditService2 = scope2.ServiceProvider.GetRequiredService<IAuditService>();

        // Assert
        Assert.AreNotSame(auditService1, auditService2);

        // Cleanup
        scope1.Dispose();
        scope2.Dispose();
    }

    [TestMethod]
    public void ServiceDI_ServiceLifetimes_VerifiesCorrectLifetimeRegistration()
    {
        // Act
        _services.AddScoped<IAuditService, AuditService>();
        _services.AddScoped<ISystemMetricsService, SystemMetricsService>();
        _services.AddSingleton<ACS.Service.Infrastructure.InMemoryEntityGraph>();

        // Assert
        var auditServiceDescriptor = _services.FirstOrDefault(s => s.ServiceType == typeof(IAuditService));
        var systemMetricsServiceDescriptor = _services.FirstOrDefault(s => s.ServiceType == typeof(ISystemMetricsService));
        var entityGraphDescriptor = _services.FirstOrDefault(s => s.ServiceType == typeof(ACS.Service.Infrastructure.InMemoryEntityGraph));

        Assert.IsNotNull(auditServiceDescriptor);
        Assert.IsNotNull(systemMetricsServiceDescriptor);
        Assert.IsNotNull(entityGraphDescriptor);
        
        Assert.AreEqual(ServiceLifetime.Scoped, auditServiceDescriptor.Lifetime);
        Assert.AreEqual(ServiceLifetime.Scoped, systemMetricsServiceDescriptor.Lifetime);
        Assert.AreEqual(ServiceLifetime.Singleton, entityGraphDescriptor.Lifetime);
    }

    #endregion

    #region Distributed Cache Tests

    [TestMethod]
    public void ServiceDI_AddDistributedMemoryCache_RegistersCache()
    {
        // Act
        _services.AddDistributedMemoryCache();
        _serviceProvider = _services.BuildServiceProvider();

        // Assert
        var distributedCache = _serviceProvider.GetService<IDistributedCache>();
        Assert.IsNotNull(distributedCache);
    }

    [TestMethod]
    public void ServiceDI_DistributedCache_SupportsBasicOperations()
    {
        // Arrange
        _services.AddDistributedMemoryCache();
        _serviceProvider = _services.BuildServiceProvider();
        var cache = _serviceProvider.GetRequiredService<IDistributedCache>();
        
        // Act
        var testKey = "test-key";
        var testValue = System.Text.Encoding.UTF8.GetBytes("test-value");
        
        cache.Set(testKey, testValue);
        var retrieved = cache.Get(testKey);
        
        // Assert
        Assert.IsNotNull(retrieved);
        Assert.AreEqual("test-value", System.Text.Encoding.UTF8.GetString(retrieved));
    }

    [TestMethod]
    public async Task ServiceDI_DistributedCache_SupportsAsyncOperations()
    {
        // Arrange
        _services.AddDistributedMemoryCache();
        _serviceProvider = _services.BuildServiceProvider();
        var cache = _serviceProvider.GetRequiredService<IDistributedCache>();
        
        // Act
        var testKey = "async-test";
        var testValue = System.Text.Encoding.UTF8.GetBytes("async-value");
        
        await cache.SetAsync(testKey, testValue);
        var retrieved = await cache.GetAsync(testKey);
        
        // Assert
        Assert.IsNotNull(retrieved);
        Assert.AreEqual("async-value", System.Text.Encoding.UTF8.GetString(retrieved));
    }

    [TestMethod]
    public void ServiceDI_DistributedCache_RegistersWithSingletonLifetime()
    {
        // Act
        _services.AddDistributedMemoryCache();

        // Assert
        var cacheServiceDescriptor = _services.FirstOrDefault(s => s.ServiceType == typeof(IDistributedCache));
        Assert.IsNotNull(cacheServiceDescriptor);
        Assert.AreEqual(ServiceLifetime.Singleton, cacheServiceDescriptor.Lifetime);
    }

    #endregion

    #region Integration Tests

    [TestMethod]
    public void ServiceDI_FullServiceStack_ResolvesAllDependencies()
    {
        // Arrange
        _services.AddDbContext<ACS.Service.Data.ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        _services.AddSingleton<ACS.Service.Infrastructure.InMemoryEntityGraph>();
        _services.AddDistributedMemoryCache();

        // Act - Register all core services
        _services.AddScoped<IAuditService, AuditService>();
        _services.AddScoped<ISystemMetricsService, SystemMetricsService>();
        _serviceProvider = _services.BuildServiceProvider();

        // Assert - All services should resolve without throwing dependency injection errors
        using var scope = _serviceProvider.CreateScope();
        
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
        var systemMetricsService = scope.ServiceProvider.GetRequiredService<ISystemMetricsService>();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
        
        Assert.IsNotNull(auditService);
        Assert.IsNotNull(systemMetricsService);
        Assert.IsNotNull(cache);
    }

    #endregion

    #region Error Handling Tests

    [TestMethod]
    public void ServiceDI_MissingDependency_ThrowsInvalidOperationException()
    {
        // Arrange - Register service without its dependencies
        _services.AddScoped<IAuditService, AuditService>();
        _serviceProvider = _services.BuildServiceProvider();

        // Act & Assert
        Assert.ThrowsException<InvalidOperationException>(() =>
        {
            var auditService = _serviceProvider.GetRequiredService<IAuditService>();
        });
    }

    [TestMethod]
    public void ServiceDI_ServiceRegistration_CanBeCalledMultipleTimes()
    {
        // Act
        _services.AddScoped<IAuditService, AuditService>();
        var countAfterFirst = _services.Count;
        
        _services.AddScoped<IAuditService, AuditService>();
        var countAfterSecond = _services.Count;

        // Assert - Services should be registered again (last registration wins)
        Assert.IsTrue(countAfterSecond > countAfterFirst);
    }

    #endregion

    #region Service Provider Factory Tests

    [TestMethod]
    public void ServiceDI_ServiceProviderFactory_CreatesValidProvider()
    {
        // Act
        _services.AddLogging();
        _serviceProvider = _services.BuildServiceProvider();

        // Assert
        Assert.IsNotNull(_serviceProvider);
        
        var logger = _serviceProvider.GetService<ILogger<ServiceCollectionExtensionsTests>>();
        Assert.IsNotNull(logger);
    }

    [TestMethod]
    public void ServiceDI_ServiceScope_DisposesCorrectly()
    {
        // Arrange
        _services.AddScoped<TestDisposableService>();
        _serviceProvider = _services.BuildServiceProvider();

        // Act
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<TestDisposableService>();
        
        // Assert - Service should be created
        Assert.IsNotNull(service);
        Assert.IsFalse(service.IsDisposed);
        
        // After scope disposal, service should be disposed
        scope.Dispose();
        Assert.IsTrue(service.IsDisposed);
    }

    #endregion
}

#region Test Helper Classes

public class TestDisposableService : IDisposable
{
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

#endregion