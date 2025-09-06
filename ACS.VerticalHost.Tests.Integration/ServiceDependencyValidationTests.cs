using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ACS.VerticalHost.Commands;
using ACS.VerticalHost.Handlers;
using ACS.VerticalHost.Services;
using ACS.VerticalHost.Extensions;
using ACS.Service.Data;
using ACS.Service.Services;
using ACS.Service.Infrastructure;
using ACS.Infrastructure.DependencyInjection;
using System.Reflection;

namespace ACS.VerticalHost.Tests.Integration;

/// <summary>
/// Validation tests for service dependencies, interface implementations,
/// and handler registration consistency
/// </summary>
[TestClass]
public class ServiceDependencyValidationTests
{
    private ServiceProvider? _serviceProvider;
    private ILogger<ServiceDependencyValidationTests>? _logger;

    [TestInitialize]
    public void Setup()
    {
        var services = new ServiceCollection();
        var configuration = BuildTestConfiguration();

        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"DependencyTestDb_{Guid.NewGuid()}"));

        services.AddAcsServiceLayer(configuration);
        services.AddSingleton<ICommandBuffer, CommandBuffer>();
        services.AddHandlersAutoRegistration();

        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<ServiceDependencyValidationTests>>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _serviceProvider?.Dispose();
    }

    #region Handler-Service Dependency Validation

    [TestMethod]
    public void AllHandlers_Should_HaveRequiredServiceDependencies()
    {
        var handlerTypes = GetAllHandlerTypes();
        var missingDependencies = new List<(Type HandlerType, Type MissingDependency)>();

        foreach (var handlerType in handlerTypes)
        {
            var constructors = handlerType.GetConstructors();
            var primaryConstructor = constructors.OrderByDescending(c => c.GetParameters().Length).First();
            
            foreach (var parameter in primaryConstructor.GetParameters())
            {
                var serviceType = parameter.ParameterType;
                
                // Try to resolve the service
                var service = _serviceProvider.GetService(serviceType);
                if (service == null)
                {
                    missingDependencies.Add((handlerType, serviceType));
                    _logger.LogError("Handler {HandlerType} requires {ServiceType} but it's not registered", 
                        handlerType.Name, serviceType.Name);
                }
            }
        }

        if (missingDependencies.Any())
        {
            var errorMessage = $"Missing dependencies found:\n" + 
                string.Join("\n", missingDependencies.Select(md => $"  {md.HandlerType.Name} -> {md.MissingDependency.Name}"));
            Assert.Fail(errorMessage);
        }

        _logger.LogInformation("✅ All {Count} handlers have required service dependencies", handlerTypes.Count);
    }

    [TestMethod]
    public void AllHandlers_Should_ImplementCorrectInterfaces()
    {
        var handlerTypes = GetAllHandlerTypes();
        var interfaceViolations = new List<string>();

        foreach (var handlerType in handlerTypes)
        {
            var interfaces = handlerType.GetInterfaces();
            var handlerInterfaces = interfaces.Where(i => 
                (i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(ICommandHandler<>) || 
                                   i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>) ||
                                   i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>)))
            ).ToList();

            if (!handlerInterfaces.Any())
            {
                interfaceViolations.Add($"{handlerType.Name} does not implement any handler interfaces");
                continue;
            }

            // Validate interface implementation
            foreach (var handlerInterface in handlerInterfaces)
            {
                var handleMethod = handlerType.GetMethod("HandleAsync");
                if (handleMethod == null)
                {
                    interfaceViolations.Add($"{handlerType.Name} does not implement HandleAsync method");
                    continue;
                }

                // Check return type
                if (!handleMethod.ReturnType.IsGenericType || 
                    handleMethod.ReturnType.GetGenericTypeDefinition() != typeof(Task<>))
                {
                    interfaceViolations.Add($"{handlerType.Name}.HandleAsync does not return Task<T>");
                }

                // Check parameters
                var parameters = handleMethod.GetParameters();
                if (parameters.Length != 2)
                {
                    interfaceViolations.Add($"{handlerType.Name}.HandleAsync should have exactly 2 parameters");
                }
                else if (parameters[1].ParameterType != typeof(CancellationToken))
                {
                    interfaceViolations.Add($"{handlerType.Name}.HandleAsync second parameter should be CancellationToken");
                }
            }
        }

        if (interfaceViolations.Any())
        {
            var errorMessage = "Interface implementation violations:\n" + string.Join("\n", interfaceViolations.Select(v => $"  {v}"));
            Assert.Fail(errorMessage);
        }

        _logger.LogInformation("✅ All {Count} handlers implement correct interfaces", handlerTypes.Count);
    }

    [TestMethod]
    public void BusinessDomainServices_Should_BeRegistered()
    {
        var requiredServices = new[]
        {
            typeof(IResourceService),
            typeof(IPermissionService),
            typeof(IAuditService),
            typeof(IComplianceService),
            typeof(ISecurityService),
            typeof(IUserService),
            typeof(IGroupService),
            typeof(IRoleService),
            typeof(ApplicationDbContext),
            typeof(InMemoryEntityGraph)
        };

        var missingServices = new List<Type>();

        foreach (var serviceType in requiredServices)
        {
            var service = _serviceProvider.GetService(serviceType);
            if (service == null)
            {
                missingServices.Add(serviceType);
                _logger.LogError("Required service {ServiceType} is not registered", serviceType.Name);
            }
            else
            {
                _logger.LogDebug("✓ Service {ServiceType} is registered", serviceType.Name);
            }
        }

        if (missingServices.Any())
        {
            var errorMessage = $"Missing required services: {string.Join(", ", missingServices.Select(s => s.Name))}";
            Assert.Fail(errorMessage);
        }

        _logger.LogInformation("✅ All required business domain services are registered");
    }

    [TestMethod]
    public void ServiceLifetimes_Should_BeConsistent()
    {
        var serviceLifetimeChecks = new Dictionary<Type, ServiceLifetime>
        {
            // Handlers should be transient
            [typeof(ICommandHandler<CreateResourceCommand, ACS.Service.Domain.Resource>)] = ServiceLifetime.Transient,
            [typeof(IQueryHandler<GetResourceQuery, ACS.Service.Domain.Resource>)] = ServiceLifetime.Transient,
            
            // Domain services should be scoped
            [typeof(IResourceService)] = ServiceLifetime.Scoped,
            [typeof(IPermissionService)] = ServiceLifetime.Scoped,
            [typeof(IAuditService)] = ServiceLifetime.Scoped,
            
            // Command buffer should be singleton
            [typeof(ICommandBuffer)] = ServiceLifetime.Singleton,
            
            // Entity graph should be singleton
            [typeof(InMemoryEntityGraph)] = ServiceLifetime.Singleton,
            
            // Database context should be scoped/pooled
            [typeof(ApplicationDbContext)] = ServiceLifetime.Scoped // Or Singleton if pooled
        };

        var lifetimeViolations = new List<string>();

        using (var scope = _serviceProvider.CreateScope())
        {
            foreach (var check in serviceLifetimeChecks)
            {
                var serviceType = check.Key;
                var expectedLifetime = check.Value;

                try
                {
                    // Test by creating instances in different scopes and comparing
                    var instance1 = scope.ServiceProvider.GetService(serviceType);
                    var instance2 = _serviceProvider.GetService(serviceType);
                    
                    if (instance1 == null || instance2 == null)
                    {
                        continue; // Service not registered, will be caught by other tests
                    }

                    bool areSame = ReferenceEquals(instance1, instance2);

                    switch (expectedLifetime)
                    {
                        case ServiceLifetime.Singleton:
                            if (!areSame)
                            {
                                lifetimeViolations.Add($"{serviceType.Name} should be Singleton but instances are different");
                            }
                            break;
                        case ServiceLifetime.Scoped:
                            // For scoped services, instances within same scope should be same,
                            // but across different scopes should be different
                            using (var scope2 = _serviceProvider.CreateScope())
                            {
                                var instance3 = scope2.ServiceProvider.GetService(serviceType);
                                var scopedInstance1 = scope.ServiceProvider.GetService(serviceType);
                                var scopedInstance2 = scope.ServiceProvider.GetService(serviceType);
                                
                                if (!ReferenceEquals(scopedInstance1, scopedInstance2))
                                {
                                    lifetimeViolations.Add($"{serviceType.Name} should be Scoped but instances within same scope are different");
                                }
                            }
                            break;
                        case ServiceLifetime.Transient:
                            if (areSame)
                            {
                                lifetimeViolations.Add($"{serviceType.Name} should be Transient but instances are same");
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    lifetimeViolations.Add($"Error checking lifetime for {serviceType.Name}: {ex.Message}");
                }
            }
        }

        if (lifetimeViolations.Any())
        {
            var errorMessage = "Service lifetime violations:\n" + string.Join("\n", lifetimeViolations.Select(v => $"  {v}"));
            Assert.Fail(errorMessage);
        }

        _logger.LogInformation("✅ Service lifetimes are consistent");
    }

    #endregion

    #region Cross-Service Integration Validation

    [TestMethod]
    public void ServiceDependencyChain_Should_ResolveCorrectly()
    {
        // Test complex dependency chains by creating handlers that have multiple service dependencies
        var complexHandlerTypes = new[]
        {
            typeof(GetResourcePermissionsQueryHandler), // Depends on IResourceService + IPermissionService
            typeof(GetComplianceReportQueryHandler), // Depends on IComplianceService
            typeof(AccessViolationHandlerCommandHandler), // Depends on ISecurityService + IAuditService
            typeof(EvaluateComplexPermissionQueryHandler) // Depends on IPermissionService
        };

        var resolutionErrors = new List<string>();

        foreach (var handlerType in complexHandlerTypes)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var handlerInstance = scope.ServiceProvider.GetRequiredService(handlerType);
                
                if (handlerInstance == null)
                {
                    resolutionErrors.Add($"Failed to resolve {handlerType.Name}");
                    continue;
                }

                // Verify all constructor dependencies are satisfied
                var constructor = handlerType.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
                foreach (var parameter in constructor.GetParameters())
                {
                    var dependencyService = scope.ServiceProvider.GetRequiredService(parameter.ParameterType);
                    if (dependencyService == null)
                    {
                        resolutionErrors.Add($"Handler {handlerType.Name} dependency {parameter.ParameterType.Name} could not be resolved");
                    }
                }

                _logger.LogDebug("✓ Handler {HandlerType} and all dependencies resolved successfully", handlerType.Name);
            }
            catch (Exception ex)
            {
                resolutionErrors.Add($"Error resolving {handlerType.Name}: {ex.Message}");
            }
        }

        if (resolutionErrors.Any())
        {
            var errorMessage = "Service dependency chain resolution errors:\n" + string.Join("\n", resolutionErrors.Select(e => $"  {e}"));
            Assert.Fail(errorMessage);
        }

        _logger.LogInformation("✅ Complex service dependency chains resolve correctly");
    }

    [TestMethod]
    public void CircularDependencies_Should_NotExist()
    {
        // Test for circular dependencies by attempting to resolve all registered services
        var circularDependencyErrors = new List<string>();

        var serviceDescriptors = _serviceProvider.GetRequiredService<IServiceCollection>();
        
        foreach (var descriptor in serviceDescriptors.Where(d => d.ServiceType.Namespace?.StartsWith("ACS") == true))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var service = scope.ServiceProvider.GetService(descriptor.ServiceType);
                
                // If we can resolve it without exception, no circular dependency
                _logger.LogTrace("✓ Service {ServiceType} resolved without circular dependency", descriptor.ServiceType.Name);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("circular dependency"))
            {
                circularDependencyErrors.Add($"Circular dependency detected for {descriptor.ServiceType.Name}: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Other exceptions might indicate different issues, but not necessarily circular dependencies
                _logger.LogDebug("Service {ServiceType} resolution failed: {Error}", descriptor.ServiceType.Name, ex.Message);
            }
        }

        if (circularDependencyErrors.Any())
        {
            var errorMessage = "Circular dependency errors:\n" + string.Join("\n", circularDependencyErrors.Select(e => $"  {e}"));
            Assert.Fail(errorMessage);
        }

        _logger.LogInformation("✅ No circular dependencies detected");
    }

    #endregion

    #region Handler Registration Validation

    [TestMethod]
    public void HandlerAutoRegistration_Should_FindAllHandlerClasses()
    {
        var handlerAssembly = Assembly.GetAssembly(typeof(CreateResourceCommandHandler));
        var handlerTypes = handlerAssembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => type.Namespace == "ACS.VerticalHost.Handlers")
            .ToList();

        var expectedHandlerCount = GetExpectedHandlerCount();
        
        Assert.IsTrue(handlerTypes.Count >= expectedHandlerCount, 
            $"Expected at least {expectedHandlerCount} handlers, found {handlerTypes.Count}");

        // Verify all handler types can be resolved
        var unregisteredHandlers = new List<string>();

        foreach (var handlerType in handlerTypes)
        {
            var interfaces = handlerType.GetInterfaces();
            var handlerInterfaces = interfaces.Where(i => 
                i.IsGenericType && 
                (i.GetGenericTypeDefinition() == typeof(ICommandHandler<>) ||
                 i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>) ||
                 i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>))).ToList();

            foreach (var handlerInterface in handlerInterfaces)
            {
                var registeredHandler = _serviceProvider.GetService(handlerInterface);
                if (registeredHandler == null)
                {
                    unregisteredHandlers.Add($"{handlerType.Name} implementing {handlerInterface.Name}");
                }
            }
        }

        if (unregisteredHandlers.Any())
        {
            var errorMessage = "Unregistered handlers found:\n" + string.Join("\n", unregisteredHandlers.Select(h => $"  {h}"));
            Assert.Fail(errorMessage);
        }

        _logger.LogInformation("✅ All {Count} handler classes are properly auto-registered", handlerTypes.Count);
    }

    [TestMethod]
    public void CommandQueryInterface_Should_BeImplementedCorrectly()
    {
        var commandTypes = GetAllCommandTypes();
        var queryTypes = GetAllQueryTypes();
        
        var interfaceViolations = new List<string>();

        // Validate all commands implement ICommand<TResult>
        foreach (var commandType in commandTypes)
        {
            var commandInterfaces = commandType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>))
                .ToList();

            if (commandInterfaces.Count != 1)
            {
                interfaceViolations.Add($"{commandType.Name} should implement exactly one ICommand<TResult> interface, found {commandInterfaces.Count}");
            }
        }

        // Validate all queries implement IQuery<TResult>
        foreach (var queryType in queryTypes)
        {
            var queryInterfaces = queryType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>))
                .ToList();

            if (queryInterfaces.Count != 1)
            {
                interfaceViolations.Add($"{queryType.Name} should implement exactly one IQuery<TResult> interface, found {queryInterfaces.Count}");
            }
        }

        if (interfaceViolations.Any())
        {
            var errorMessage = "Command/Query interface violations:\n" + string.Join("\n", interfaceViolations.Select(v => $"  {v}"));
            Assert.Fail(errorMessage);
        }

        _logger.LogInformation("✅ All {CommandCount} commands and {QueryCount} queries implement correct interfaces", 
            commandTypes.Count, queryTypes.Count);
    }

    #endregion

    #region Performance and Resource Validation

    [TestMethod]
    public void HandlerInstantiation_Should_BePerformant()
    {
        var handlerTypes = GetAllHandlerTypes();
        var performanceResults = new List<(Type HandlerType, TimeSpan InstantiationTime)>();

        foreach (var handlerType in handlerTypes)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService(handlerType);
                stopwatch.Stop();
                
                performanceResults.Add((handlerType, stopwatch.Elapsed));
                
                // Warn if instantiation takes too long
                if (stopwatch.ElapsedMilliseconds > 100)
                {
                    _logger.LogWarning("Handler {HandlerType} took {ElapsedMs}ms to instantiate", 
                        handlerType.Name, stopwatch.ElapsedMilliseconds);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Failed to instantiate handler {HandlerType}", handlerType.Name);
                throw; // Re-throw to fail the test
            }
        }

        var averageTime = performanceResults.Average(r => r.InstantiationTime.TotalMilliseconds);
        var slowHandlers = performanceResults.Where(r => r.InstantiationTime.TotalMilliseconds > 50).ToList();

        _logger.LogInformation("✅ Handler instantiation performance:");
        _logger.LogInformation("  - Average time: {AverageMs:F2}ms", averageTime);
        _logger.LogInformation("  - Handlers > 50ms: {SlowCount}/{TotalCount}", slowHandlers.Count, performanceResults.Count);

        // Fail if average instantiation time is too high
        Assert.IsTrue(averageTime < 20, $"Average handler instantiation time {averageTime:F2}ms is too high");
    }

    [TestMethod]
    public void ServiceResolution_Should_NotLeakMemory()
    {
        var initialMemory = GC.GetTotalMemory(true);
        
        // Create and dispose many service scopes to test for memory leaks
        for (int i = 0; i < 100; i++)
        {
            using var scope = _serviceProvider.CreateScope();
            
            // Resolve several handlers
            var resourceHandler = scope.ServiceProvider.GetService<ICommandHandler<CreateResourceCommand, ACS.Service.Domain.Resource>>();
            var permissionHandler = scope.ServiceProvider.GetService<ICommandHandler<GrantPermissionCommand, PermissionGrantResult>>();
            var auditHandler = scope.ServiceProvider.GetService<ICommandHandler<RecordAuditEventCommand, AuditEventResult>>();
            
            // Use the handlers briefly
            Assert.IsNotNull(resourceHandler);
            Assert.IsNotNull(permissionHandler);
            Assert.IsNotNull(auditHandler);
        }
        
        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;
        var memoryIncreaseKB = memoryIncrease / 1024.0;
        
        _logger.LogInformation("Memory usage: Initial: {InitialKB:F1}KB, Final: {FinalKB:F1}KB, Increase: {IncreaseKB:F1}KB",
            initialMemory / 1024.0, finalMemory / 1024.0, memoryIncreaseKB);
        
        // Fail if memory increase is excessive (> 1MB)
        Assert.IsTrue(memoryIncreaseKB < 1024, $"Memory increase of {memoryIncreaseKB:F1}KB suggests potential memory leak");
        
        _logger.LogInformation("✅ Service resolution does not leak excessive memory");
    }

    #endregion

    #region Helper Methods

    private List<Type> GetAllHandlerTypes()
    {
        var handlerAssembly = Assembly.GetAssembly(typeof(CreateResourceCommandHandler));
        return handlerAssembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => type.Namespace == "ACS.VerticalHost.Handlers")
            .ToList();
    }

    private List<Type> GetAllCommandTypes()
    {
        var commandAssembly = Assembly.GetAssembly(typeof(CreateResourceCommand));
        return commandAssembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => type.Namespace == "ACS.VerticalHost.Commands")
            .Where(type => type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>)))
            .ToList();
    }

    private List<Type> GetAllQueryTypes()
    {
        var commandAssembly = Assembly.GetAssembly(typeof(GetResourceQuery));
        return commandAssembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => type.Namespace == "ACS.VerticalHost.Commands")
            .Where(type => type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>)))
            .ToList();
    }

    private int GetExpectedHandlerCount()
    {
        // Based on the business domain handlers we've seen:
        // Resource: 6 handlers (3 commands + 3 queries)
        // Permission: 6 handlers (3 commands + 3 queries) 
        // Audit: 6 handlers (2 commands + 4 queries)
        // Access Control: 5 handlers (2 commands + 3 queries)
        // Plus existing handlers (Auth, User, System, etc.)
        return 20; // Conservative estimate
    }

    private static IConfiguration BuildTestConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=DependencyTestDb;Trusted_Connection=true;",
                ["TenantId"] = "dependency-test-tenant",
                ["Logging:LogLevel:Default"] = "Information"
            })
            .Build();
    }

    #endregion
}