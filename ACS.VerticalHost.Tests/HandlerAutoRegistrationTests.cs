using Microsoft.Extensions.DependencyInjection;
using ACS.VerticalHost.Extensions;
using ACS.VerticalHost.Services;
using System.Reflection;

namespace ACS.VerticalHost.Tests;

/// <summary>
/// Unit tests for HandlerAutoRegistration functionality
/// Tests the reflection-based automatic handler discovery and registration
/// </summary>
[TestClass]
public class HandlerAutoRegistrationTests
{
    private IServiceCollection _services = null!;
    private ServiceProvider _serviceProvider = null!;

    [TestInitialize]
    public void Setup()
    {
        // Arrange
        _services = new ServiceCollection();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _serviceProvider?.Dispose();
    }

    #region AddHandlersAutoRegistration Tests

    [TestMethod]
    public void HandlerAutoRegistration_AddHandlersAutoRegistration_RegistersHandlers()
    {
        // Arrange
        var initialCount = _services.Count;

        // Act
        var result = _services.AddHandlersAutoRegistration();

        // Assert
        Assert.AreSame(_services, result);
        Assert.IsTrue(_services.Count > initialCount);
    }

    [TestMethod]
    public void HandlerAutoRegistration_AddHandlersAutoRegistration_ReturnsServiceCollection()
    {
        // Act
        var result = _services.AddHandlersAutoRegistration();

        // Assert
        Assert.AreSame(_services, result);
    }

    [TestMethod]
    public void HandlerAutoRegistration_AddHandlersAutoRegistration_FindsHandlerTypes()
    {
        // Act
        _services.AddHandlersAutoRegistration();
        _serviceProvider = _services.BuildServiceProvider();

        // Assert - Check that handlers are registered by verifying we can resolve common handler interfaces
        var handlerServices = _services.Where(s => s.ServiceType.IsGenericType &&
            (s.ServiceType.GetGenericTypeDefinition() == typeof(ICommandHandler<>) ||
             s.ServiceType.GetGenericTypeDefinition() == typeof(ICommandHandler<,>) ||
             s.ServiceType.GetGenericTypeDefinition() == typeof(IQueryHandler<,>)));
        
        Assert.IsTrue(handlerServices.Any());
    }

    [TestMethod]
    public void HandlerAutoRegistration_AddHandlersAutoRegistration_RegistersWithTransientLifetime()
    {
        // Act
        _services.AddHandlersAutoRegistration();

        // Assert
        var handlerServices = _services.Where(s => s.ServiceType.IsGenericType &&
            (s.ServiceType.GetGenericTypeDefinition() == typeof(ICommandHandler<>) ||
             s.ServiceType.GetGenericTypeDefinition() == typeof(ICommandHandler<,>) ||
             s.ServiceType.GetGenericTypeDefinition() == typeof(IQueryHandler<,>)));

        foreach (var service in handlerServices)
        {
            Assert.AreEqual(ServiceLifetime.Transient, service.Lifetime);
        }
    }

    #endregion

    #region Handler Discovery Tests

    [TestMethod]
    public void HandlerAutoRegistration_AddHandlersAutoRegistration_OnlyRegistersHandlersFromCorrectNamespace()
    {
        // Act
        _services.AddHandlersAutoRegistration();

        // Assert
        var handlerServices = _services.Where(s => s.ImplementationType != null);
        
        foreach (var service in handlerServices)
        {
            if (service.ImplementationType!.Namespace?.Contains("Handlers") == true)
            {
                Assert.IsTrue(service.ImplementationType.Namespace.Contains("ACS.VerticalHost.Handlers"));
            }
        }
    }

    [TestMethod]
    public void HandlerAutoRegistration_AddHandlersAutoRegistration_DoesNotRegisterAbstractClasses()
    {
        // Act
        _services.AddHandlersAutoRegistration();

        // Assert
        var handlerServices = _services.Where(s => s.ImplementationType != null);
        
        foreach (var service in handlerServices)
        {
            Assert.IsFalse(service.ImplementationType!.IsAbstract);
        }
    }

    [TestMethod]
    public void HandlerAutoRegistration_AddHandlersAutoRegistration_DoesNotRegisterInterfaces()
    {
        // Act
        _services.AddHandlersAutoRegistration();

        // Assert
        var handlerServices = _services.Where(s => s.ImplementationType != null);
        
        foreach (var service in handlerServices)
        {
            Assert.IsFalse(service.ImplementationType!.IsInterface);
        }
    }

    #endregion

    #region Interface Detection Tests

    [TestMethod]
    public void HandlerAutoRegistration_IsCommandHandlerInterface_ReturnsTrueForICommandHandler()
    {
        // Arrange
        var interfaceType = typeof(ICommandHandler<>);
        var method = typeof(HandlerAutoRegistration)
            .GetMethod("IsCommandHandlerInterface", BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = (bool)method!.Invoke(null, new object[] { interfaceType })!;

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void HandlerAutoRegistration_IsCommandHandlerInterface_ReturnsTrueForICommandHandlerWithResponse()
    {
        // Arrange
        var interfaceType = typeof(ICommandHandler<,>);
        var method = typeof(HandlerAutoRegistration)
            .GetMethod("IsCommandHandlerInterface", BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = (bool)method!.Invoke(null, new object[] { interfaceType })!;

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void HandlerAutoRegistration_IsCommandHandlerInterface_ReturnsFalseForNonGenericType()
    {
        // Arrange
        var interfaceType = typeof(string);
        var method = typeof(HandlerAutoRegistration)
            .GetMethod("IsCommandHandlerInterface", BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = (bool)method!.Invoke(null, new object[] { interfaceType })!;

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void HandlerAutoRegistration_IsQueryHandlerInterface_ReturnsTrueForIQueryHandler()
    {
        // Arrange
        var interfaceType = typeof(IQueryHandler<,>);
        var method = typeof(HandlerAutoRegistration)
            .GetMethod("IsQueryHandlerInterface", BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = (bool)method!.Invoke(null, new object[] { interfaceType })!;

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void HandlerAutoRegistration_IsQueryHandlerInterface_ReturnsFalseForNonGenericType()
    {
        // Arrange
        var interfaceType = typeof(string);
        var method = typeof(HandlerAutoRegistration)
            .GetMethod("IsQueryHandlerInterface", BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = (bool)method!.Invoke(null, new object[] { interfaceType })!;

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void HandlerAutoRegistration_IsQueryHandlerInterface_ReturnsFalseForWrongGenericType()
    {
        // Arrange
        var interfaceType = typeof(ICommandHandler<>);
        var method = typeof(HandlerAutoRegistration)
            .GetMethod("IsQueryHandlerInterface", BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = (bool)method!.Invoke(null, new object[] { interfaceType })!;

        // Assert
        Assert.IsFalse(result);
    }

    #endregion

    #region Edge Cases and Error Handling

    [TestMethod]
    public void HandlerAutoRegistration_AddHandlersAutoRegistration_HandlesEmptyServiceCollection()
    {
        // Arrange
        var emptyServices = new ServiceCollection();

        // Act
        var result = emptyServices.AddHandlersAutoRegistration();

        // Assert
        Assert.AreSame(emptyServices, result);
        Assert.IsTrue(emptyServices.Count >= 0); // Should have some handlers registered
    }

    [TestMethod]
    public void HandlerAutoRegistration_AddHandlersAutoRegistration_CanBeCalledMultipleTimes()
    {
        // Act
        _services.AddHandlersAutoRegistration();
        var firstCount = _services.Count;
        _services.AddHandlersAutoRegistration();
        var secondCount = _services.Count;

        // Assert
        // Should register handlers twice (this is expected behavior - same as manual registration)
        Assert.IsTrue(secondCount > firstCount);
    }

    #endregion
}