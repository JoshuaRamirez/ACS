# Handler Development Guide

## Overview

The ACS system uses a sophisticated handler-based architecture with automatic registration, standardized error handling, and comprehensive telemetry integration. This guide covers how to develop, test, and maintain handlers within the vertical slice architecture.

## Handler Architecture

### Core Handler Interfaces

The system defines three primary handler interfaces for CQRS operations:

```csharp
// Void Commands (no return value)
public interface ICommandHandler<TCommand> where TCommand : ICommand
{
    Task HandleAsync(TCommand command, CancellationToken cancellationToken);
}

// Commands with Result
public interface ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

// Query Handlers
public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
```

### Command and Query Contracts

```csharp
// Base interfaces for commands and queries
public interface ICommand { }
public interface ICommand<TResult> { }
public interface IQuery<TResult> { }
```

## Auto-Registration System

### How It Works

The system automatically discovers and registers all handlers using reflection-based convention:

```csharp
// Located: ACS.VerticalHost.Extensions.HandlerAutoRegistration.cs
public static IServiceCollection AddHandlersAutoRegistration(this IServiceCollection services)
{
    var assembly = Assembly.GetExecutingAssembly();
    var handlerTypes = assembly.GetTypes()
        .Where(type => type.IsClass && !type.IsAbstract)
        .Where(type => type.Namespace == "ACS.VerticalHost.Handlers")
        .ToList();

    var registeredCount = 0;
    foreach (var handlerType in handlerTypes)
    {
        var interfaces = handlerType.GetInterfaces();
        
        foreach (var interfaceType in interfaces)
        {
            if (IsCommandHandlerInterface(interfaceType) || IsQueryHandlerInterface(interfaceType))
            {
                services.AddTransient(interfaceType, handlerType);
                registeredCount++;
                Console.WriteLine($"✅ Auto-registered: {interfaceType.Name} -> {handlerType.Name}");
            }
        }
    }
    
    Console.WriteLine($"🎯 Total handlers auto-registered: {registeredCount}");
    return services;
}
```

### Registration Requirements

For a handler to be auto-registered, it must:

1. **Be located in the correct namespace**: `ACS.VerticalHost.Handlers`
2. **Implement a recognized interface**: `ICommandHandler<>`, `ICommandHandler<,>`, or `IQueryHandler<,>`
3. **Be a concrete class**: Not abstract or interface
4. **Have proper constructor dependencies**: Services will be injected via DI

### Current Handler Count

The system currently auto-registers **67+ handlers** across these categories:
- User management (Create, Update, Delete, Query operations)
- Group management and hierarchy operations
- Role and permission management
- Authentication and authorization
- System metrics and diagnostics
- Database backup and maintenance
- Index analysis and optimization
- Rate limiting and monitoring

## Creating New Handlers

### 1. Define Your Command/Query

```csharp
// Commands/MyFeatureCommands.cs
public class CreateWidgetCommand : ICommand<Widget>
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
}

public class GetWidgetQuery : IQuery<Widget>
{
    public int WidgetId { get; set; }
    public bool IncludeDetails { get; set; }
}
```

### 2. Implement Command Handler

```csharp
// Handlers/WidgetHandlers.cs
using ACS.VerticalHost.Services;
using static ACS.VerticalHost.Services.HandlerErrorHandling;
using static ACS.VerticalHost.Services.HandlerExtensions;

public class CreateWidgetCommandHandler : ICommandHandler<CreateWidgetCommand, Widget>
{
    private readonly IWidgetService _widgetService;
    private readonly ILogger<CreateWidgetCommandHandler> _logger;

    public CreateWidgetCommandHandler(
        IWidgetService widgetService, 
        ILogger<CreateWidgetCommandHandler> logger)
    {
        _widgetService = widgetService;
        _logger = logger;
    }

    public async Task<Widget> HandleAsync(
        CreateWidgetCommand command, 
        CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(CreateWidgetCommandHandler), nameof(HandleAsync));
        
        // 1. Log operation start with structured logging
        LogOperationStart(_logger, context, new { Name = command.Name }, correlationId);
        
        try
        {
            // 2. Validate command parameters
            if (string.IsNullOrWhiteSpace(command.Name))
                throw new ArgumentException("Widget name is required", nameof(command.Name));
            
            // 3. Create service request
            var request = new CreateWidgetRequest
            {
                Name = command.Name,
                Description = command.Description,
                CreatedBy = command.CreatedBy
            };
            
            // 4. Execute business logic through service layer
            var response = await _widgetService.CreateAsync(request);
            var widget = response.Widget;

            if (widget == null) 
                throw new InvalidOperationException("Widget creation failed - null widget returned");
            
            // 5. Log successful completion
            LogCommandSuccess(_logger, context, new { WidgetId = widget.Id, Name = widget.Name }, correlationId);
            
            return widget;
        }
        catch (Exception ex)
        {
            // 6. Standardized error handling (logs and re-throws)
            return HandleCommandError<Widget>(_logger, ex, context, correlationId);
        }
    }
}
```

### 3. Implement Query Handler

```csharp
public class GetWidgetQueryHandler : IQueryHandler<GetWidgetQuery, Widget>
{
    private readonly IWidgetService _widgetService;
    private readonly ILogger<GetWidgetQueryHandler> _logger;

    public GetWidgetQueryHandler(
        IWidgetService widgetService, 
        ILogger<GetWidgetQueryHandler> logger)
    {
        _widgetService = widgetService;
        _logger = logger;
    }

    public async Task<Widget> HandleAsync(GetWidgetQuery query, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetWidgetQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { WidgetId = query.WidgetId }, correlationId);
        
        try
        {
            // Execute query through service layer
            var request = new GetWidgetRequest { WidgetId = query.WidgetId };
            var response = await _widgetService.GetByIdAsync(request);
            var widget = response.Widget;
            
            if (widget == null)
            {
                _logger.LogWarning("Widget {WidgetId} not found. CorrelationId: {CorrelationId}", 
                    query.WidgetId, correlationId);
                throw new InvalidOperationException($"Widget with ID {query.WidgetId} not found");
            }
            
            LogQuerySuccess(_logger, context, new { WidgetId = widget.Id, Name = widget.Name }, correlationId);
            return widget;
        }
        catch (Exception ex)
        {
            return HandleQueryError<Widget>(_logger, ex, context, correlationId);
        }
    }
}
```

### 4. No Registration Required!

Once you place your handler in the `ACS.VerticalHost.Handlers` namespace and implement the correct interface, it will be automatically discovered and registered when the application starts.

You'll see output like:
```
✅ Auto-registered: ICommandHandler`2 -> CreateWidgetCommandHandler
✅ Auto-registered: IQueryHandler`2 -> GetWidgetQueryHandler
🎯 Total handlers auto-registered: 69
```

## Error Handling Patterns

### HandlerErrorHandling Utility

The system provides standardized error handling in `ACS.VerticalHost.Services.HandlerErrorHandling`:

```csharp
/// <summary>
/// Standard error handling pattern for command handlers.
/// Logs the error appropriately and re-throws to maintain clean architecture.
/// </summary>
public static TResult HandleCommandError<TResult>(
    ILogger logger, 
    Exception exception, 
    string context, 
    string? correlationId = null)
{
    // Adds telemetry tags, logs with appropriate level, and re-throws
    using var activity = Activity.Current;
    activity?.SetTag("error.type", exception.GetType().Name);
    activity?.SetTag("error.context", context);
    
    var logLevel = GetLogLevel(exception); // Business vs System errors
    logger.Log(logLevel, exception, 
        "Error in {Context}. CorrelationId: {CorrelationId}", 
        context, correlationId ?? "none");

    throw exception; // Always re-throw - don't swallow exceptions
}
```

### Logging Standards

The error handling system uses structured logging with consistent patterns:

- **Debug**: Query results, operation parameters, detailed execution info
- **Information**: Command completions, major state changes, business events
- **Warning**: Business rule violations, authentication failures, validation errors
- **Error**: System failures, infrastructure issues, unexpected exceptions

### Exception Classification

```csharp
private static LogLevel GetLogLevel(Exception exception)
{
    return exception switch
    {
        ArgumentNullException => LogLevel.Warning,
        ArgumentException => LogLevel.Warning,
        InvalidOperationException => LogLevel.Warning,
        NotSupportedException => LogLevel.Warning,
        UnauthorizedAccessException => LogLevel.Warning,
        _ => LogLevel.Error
    };
}
```

## Correlation ID Usage

### Automatic Correlation ID Generation

```csharp
public static string GetCorrelationId()
{
    return Activity.Current?.Id ?? Guid.NewGuid().ToString();
}
```

### Usage in Handlers

```csharp
public async Task<Result> HandleAsync(Command command, CancellationToken cancellationToken)
{
    var correlationId = GetCorrelationId();
    
    // All logging includes correlation ID for traceability
    LogOperationStart(_logger, "MyHandler.HandleAsync", command, correlationId);
    
    try
    {
        // ... business logic
        LogCommandSuccess(_logger, "MyHandler.HandleAsync", result, correlationId);
        return result;
    }
    catch (Exception ex)
    {
        return HandleCommandError<Result>(_logger, ex, "MyHandler.HandleAsync", correlationId);
    }
}
```

## Telemetry Integration

### OpenTelemetry Activities

Handlers automatically participate in distributed tracing:

```csharp
// Telemetry is automatically added by the command buffer and gRPC service
using var activity = VerticalHostTelemetryService.StartCommandProcessingActivity(
    request.CommandType, request.CorrelationId);

activity?.SetTag("operation.type", "command");
activity?.SetTag("command.has_result", true);
activity?.SetTag("command.result_type", result?.GetType().Name ?? "null");
```

### Performance Metrics

The system automatically tracks:
- Command processing duration
- Success/failure rates
- Queue depth and processing rates
- Memory usage and resource utilization

## Async/Await Best Practices

### Always Use ConfigureAwait(false)

```csharp
// ❌ Don't do this - can cause deadlocks
var result = await _service.GetDataAsync();

// ✅ Do this - prevents deadlocks
var result = await _service.GetDataAsync().ConfigureAwait(false);
```

### Proper Cancellation Token Usage

```csharp
public async Task<Result> HandleAsync(Command command, CancellationToken cancellationToken)
{
    // Pass cancellation token through the entire chain
    var serviceResult = await _service.ProcessAsync(request, cancellationToken);
    var dbResult = await _repository.SaveAsync(data, cancellationToken);
    
    return result;
}
```

### Avoid Blocking Calls

```csharp
// ❌ Don't do this - blocks threads
var result = _service.GetDataAsync().Result;

// ✅ Do this - proper async/await
var result = await _service.GetDataAsync();
```

## Testing Approaches

### Unit Testing Handlers

```csharp
[TestClass]
public class CreateWidgetCommandHandlerTests
{
    private Mock<IWidgetService> _mockWidgetService;
    private Mock<ILogger<CreateWidgetCommandHandler>> _mockLogger;
    private CreateWidgetCommandHandler _handler;

    [TestInitialize]
    public void Setup()
    {
        _mockWidgetService = new Mock<IWidgetService>();
        _mockLogger = new Mock<ILogger<CreateWidgetCommandHandler>>();
        _handler = new CreateWidgetCommandHandler(_mockWidgetService.Object, _mockLogger.Object);
    }

    [TestMethod]
    public async Task HandleAsync_ValidCommand_ReturnsWidget()
    {
        // Arrange
        var command = new CreateWidgetCommand { Name = "Test Widget", CreatedBy = "test-user" };
        var expectedWidget = new Widget { Id = 1, Name = "Test Widget" };
        var response = new CreateWidgetResponse { Widget = expectedWidget };

        _mockWidgetService
            .Setup(s => s.CreateAsync(It.IsAny<CreateWidgetRequest>()))
            .ReturnsAsync(response);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(expectedWidget.Id, result.Id);
        Assert.AreEqual(expectedWidget.Name, result.Name);

        // Verify service was called correctly
        _mockWidgetService.Verify(s => s.CreateAsync(It.Is<CreateWidgetRequest>(r => 
            r.Name == command.Name && r.CreatedBy == command.CreatedBy)), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_ServiceReturnsNull_ThrowsException()
    {
        // Arrange
        var command = new CreateWidgetCommand { Name = "Test Widget", CreatedBy = "test-user" };
        var response = new CreateWidgetResponse { Widget = null };

        _mockWidgetService
            .Setup(s => s.CreateAsync(It.IsAny<CreateWidgetRequest>()))
            .ReturnsAsync(response);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _handler.HandleAsync(command, CancellationToken.None));
    }
}
```

### Integration Testing

```csharp
[TestClass]
public class WidgetHandlerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    
    public WidgetHandlerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [TestMethod]
    public async Task CreateWidget_EndToEnd_Success()
    {
        // Arrange
        var client = _factory.CreateClient();
        var command = new CreateWidgetCommand { Name = "Integration Test Widget" };

        // Act - Send through HTTP API which routes to gRPC service and handlers
        var response = await client.PostAsJsonAsync("/api/widgets", command);

        // Assert
        response.EnsureSuccessStatusCode();
        var widget = await response.Content.ReadFromJsonAsync<Widget>();
        Assert.IsNotNull(widget);
        Assert.AreEqual(command.Name, widget.Name);
    }
}
```

## Handler Validation Patterns

### Input Validation

```csharp
public async Task<Result> HandleAsync(Command command, CancellationToken cancellationToken)
{
    // 1. Null checks
    if (command == null) throw new ArgumentNullException(nameof(command));
    
    // 2. Required field validation
    if (string.IsNullOrWhiteSpace(command.Name))
        throw new ArgumentException("Name is required", nameof(command.Name));
    
    // 3. Business rule validation
    if (command.StartDate > command.EndDate)
        throw new ArgumentException("Start date must be before end date");
    
    // 4. Continue with business logic...
}
```

### Response Validation

```csharp
var response = await _service.ProcessAsync(request);

// Always validate service responses
if (response == null)
    throw new InvalidOperationException("Service returned null response");
    
if (response.Data == null)
    throw new InvalidOperationException("Service returned null data");

return response.Data;
```

## Performance Considerations

### Handler Performance Guidelines

1. **Minimize Database Calls**: Batch operations where possible
2. **Use Async/Await Properly**: Don't block threads
3. **Cache Frequently Used Data**: Leverage in-memory entity graph
4. **Implement Pagination**: For queries returning large datasets
5. **Monitor Memory Usage**: Especially in long-running handlers

### Example Performance Optimization

```csharp
// ❌ Poor performance - multiple database calls
public async Task<List<UserWithDetails>> GetUsersWithDetails(GetUsersQuery query)
{
    var users = await _userService.GetAllAsync();
    var result = new List<UserWithDetails>();
    
    foreach (var user in users)
    {
        var groups = await _groupService.GetUserGroups(user.Id); // N+1 problem
        var roles = await _roleService.GetUserRoles(user.Id);   // N+1 problem
        result.Add(new UserWithDetails { User = user, Groups = groups, Roles = roles });
    }
    
    return result;
}

// ✅ Optimized - single call with includes
public async Task<List<UserWithDetails>> GetUsersWithDetails(GetUsersQuery query)
{
    var request = new GetUsersRequest 
    { 
        IncludeGroups = true, 
        IncludeRoles = true,
        Page = query.Page,
        PageSize = query.PageSize
    };
    
    var response = await _userService.GetAllWithDetailsAsync(request);
    return response.Users.Select(u => new UserWithDetails 
    { 
        User = u, 
        Groups = u.Groups, 
        Roles = u.Roles 
    }).ToList();
}
```

## Troubleshooting

### Common Issues

1. **Handler Not Found**: Ensure handler is in `ACS.VerticalHost.Handlers` namespace
2. **Multiple Handlers**: Only one handler per command/query type allowed
3. **Service Dependencies**: Ensure all constructor dependencies are registered in DI
4. **Async Deadlocks**: Always use `ConfigureAwait(false)` in service layers

### Debug Handler Registration

Add logging to see which handlers are being registered:

```csharp
// In Program.cs startup
using var scope = app.Services.CreateScope();
var serviceProvider = scope.ServiceProvider;

// List all registered handlers
var commandHandlers = serviceProvider.GetServices<ICommandHandler<CreateUserCommand, User>>();
Console.WriteLine($"Found {commandHandlers.Count()} handlers for CreateUserCommand");
```

### Monitor Handler Performance

```csharp
// Check command buffer statistics
var commandBuffer = serviceProvider.GetRequiredService<ICommandBuffer>();
var stats = commandBuffer.GetStats();

Console.WriteLine($"Commands processed: {stats.CommandsProcessed}");
Console.WriteLine($"Commands per second: {stats.CommandsPerSecond}");
Console.WriteLine($"Commands in flight: {stats.CommandsInFlight}");
```

This handler system provides enterprise-grade patterns for building maintainable, testable, and high-performance CQRS operations with minimal boilerplate code.