# ACS Developer Getting Started Guide

## Overview

This guide will get you up and running with the ACS (Access Control System) development environment. You'll learn the vertical slice architecture, create your first handler, and understand the CQRS patterns used throughout the system.

## Prerequisites

### Required Software

1. **.NET 8.0 SDK** - Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)
2. **SQL Server** - LocalDB (included with Visual Studio) or SQL Server Express
3. **IDE** - Visual Studio 2022, VS Code, or JetBrains Rider
4. **Git** - For version control

### Optional Tools

- **Docker Desktop** - For containerized development
- **SQL Server Management Studio** - For database management
- **Postman** - For API testing
- **grpcurl** - For gRPC testing

## Quick Setup

### 1. Clone and Build

```bash
# Clone the repository
git clone <repository-url>
cd ACS

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run tests to verify setup
dotnet test
```

### 2. Database Setup

The system uses LocalDB by default. No manual setup required - databases are created automatically.

```bash
# Verify LocalDB is running
sqllocaldb info

# If not installed, run the setup script
scripts/setup_dotnet.sh
```

### 3. Start the Development Environment

```bash
# Terminal 1: Start the VerticalHost (business logic layer)
cd ACS.VerticalHost
dotnet run --tenant development --port 50051

# Terminal 2: Start the HTTP API (in a new terminal)
cd ACS.WebApi
dotnet run
```

### 4. Verify Everything Works

```bash
# Test HTTP API health check
curl http://localhost:5000/health

# Test gRPC VerticalHost health check
curl http://localhost:50051/health

# Expected response: {"Healthy":true,"UptimeSeconds":...}
```

## Development Workflow

### Project Structure

```
ACS/
├── ACS.WebApi/              # HTTP REST API layer
├── ACS.VerticalHost/        # gRPC service with CQRS handlers
│   ├── Commands/            # Command and Query definitions
│   ├── Handlers/            # Auto-registered CQRS handlers
│   └── Services/            # gRPC service implementation
├── ACS.Service/             # Business logic and domain services
│   ├── Domain/              # Domain models
│   ├── Services/            # Business service interfaces
│   └── Data/                # Entity Framework and repositories
├── ACS.Infrastructure/      # Cross-cutting concerns
└── Tests/                   # Unit and integration tests
```

### Understanding the Request Flow

```
1. HTTP Request  → ACS.WebApi (REST endpoint)
2. gRPC Call     → ACS.VerticalHost (Command/Query routing)
3. Command Buffer → Sequential processing
4. Handler       → Auto-registered CQRS handler
5. Service Layer → Business logic (ACS.Service)
6. Database      → Entity Framework persistence
7. Response      ← Returns through the same layers
```

## Creating Your First Feature

Let's create a simple "Widget" management feature to understand the patterns.

### Step 1: Define Your Domain Model

```csharp
// ACS.Service/Domain/Widget.cs
namespace ACS.Service.Domain;

public class Widget
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
```

### Step 2: Define Commands and Queries

```csharp
// ACS.VerticalHost/Commands/WidgetCommands.cs
using ACS.VerticalHost.Services;

namespace ACS.VerticalHost.Commands;

// Command to create a new widget
public class CreateWidgetCommand : ICommand<Widget>
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
}

// Query to get a widget by ID
public class GetWidgetQuery : IQuery<Widget>
{
    public int WidgetId { get; set; }
}

// Query to get all widgets with pagination
public class GetWidgetsQuery : IQuery<List<Widget>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
}
```

### Step 3: Create Service Interface and Requests/Responses

```csharp
// ACS.Service/Services/IWidgetService.cs
using ACS.Service.Domain;
using ACS.Service.Requests;
using ACS.Service.Responses;

namespace ACS.Service.Services;

public interface IWidgetService
{
    Task<CreateWidgetResponse> CreateAsync(CreateWidgetRequest request);
    Task<GetWidgetResponse> GetByIdAsync(GetWidgetRequest request);
    Task<GetWidgetsResponse> GetAllAsync(GetWidgetsRequest request);
}

// ACS.Service/Requests/WidgetRequests.cs
namespace ACS.Service.Requests;

public class CreateWidgetRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
}

public class GetWidgetRequest
{
    public int WidgetId { get; set; }
}

public class GetWidgetsRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
}

// ACS.Service/Responses/WidgetResponses.cs
namespace ACS.Service.Responses;

public class CreateWidgetResponse
{
    public Widget? Widget { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class GetWidgetResponse
{
    public Widget? Widget { get; set; }
    public bool Success { get; set; }
}

public class GetWidgetsResponse
{
    public List<Widget> Widgets { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
```

### Step 4: Implement the Service

```csharp
// ACS.Service/Services/WidgetService.cs
using ACS.Service.Domain;
using ACS.Service.Requests;
using ACS.Service.Responses;
using Microsoft.Extensions.Logging;

namespace ACS.Service.Services;

public class WidgetService : IWidgetService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WidgetService> _logger;

    public WidgetService(IUnitOfWork unitOfWork, ILogger<WidgetService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<CreateWidgetResponse> CreateAsync(CreateWidgetRequest request)
    {
        try
        {
            // Input validation
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("Widget name is required");

            // Create domain entity
            var widget = new Widget
            {
                Name = request.Name,
                Description = request.Description,
                CreatedBy = request.CreatedBy,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            // Persist to database (assuming you have a repository)
            // await _unitOfWork.Widgets.AddAsync(widget);
            // await _unitOfWork.CompleteAsync();

            // For demo purposes, set a mock ID
            widget.Id = Random.Shared.Next(1, 1000);

            _logger.LogInformation("Widget {WidgetId} created: {Name}", widget.Id, widget.Name);

            return new CreateWidgetResponse { Widget = widget, Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating widget: {Name}", request.Name);
            return new CreateWidgetResponse 
            { 
                Success = false, 
                ErrorMessage = ex.Message 
            };
        }
    }

    public async Task<GetWidgetResponse> GetByIdAsync(GetWidgetRequest request)
    {
        try
        {
            // For demo purposes, create a mock widget
            // In real implementation: var widget = await _unitOfWork.Widgets.GetByIdAsync(request.WidgetId);
            var widget = new Widget
            {
                Id = request.WidgetId,
                Name = $"Demo Widget {request.WidgetId}",
                Description = "This is a demo widget",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                CreatedBy = "system",
                IsActive = true
            };

            return new GetWidgetResponse { Widget = widget, Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting widget {WidgetId}", request.WidgetId);
            return new GetWidgetResponse { Success = false };
        }
    }

    public async Task<GetWidgetsResponse> GetAllAsync(GetWidgetsRequest request)
    {
        // Demo implementation - create mock widgets
        var widgets = Enumerable.Range(1, request.PageSize)
            .Select(i => new Widget
            {
                Id = (request.Page - 1) * request.PageSize + i,
                Name = $"Widget {i}",
                Description = $"Description for widget {i}",
                CreatedAt = DateTime.UtcNow.AddDays(-i),
                CreatedBy = "system",
                IsActive = true
            })
            .ToList();

        return new GetWidgetsResponse
        {
            Widgets = widgets,
            TotalCount = 100, // Mock total
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
```

### Step 5: Create CQRS Handlers

```csharp
// ACS.VerticalHost/Handlers/WidgetHandlers.cs
using ACS.VerticalHost.Services;
using ACS.VerticalHost.Commands;
using ACS.Service.Domain;
using ACS.Service.Services;
using static ACS.VerticalHost.Services.HandlerErrorHandling;
using static ACS.VerticalHost.Services.HandlerExtensions;

namespace ACS.VerticalHost.Handlers;

// Command Handler - Creates a new widget
public class CreateWidgetCommandHandler : ICommandHandler<CreateWidgetCommand, Widget>
{
    private readonly IWidgetService _widgetService;
    private readonly ILogger<CreateWidgetCommandHandler> _logger;

    public CreateWidgetCommandHandler(IWidgetService widgetService, ILogger<CreateWidgetCommandHandler> logger)
    {
        _widgetService = widgetService;
        _logger = logger;
    }

    public async Task<Widget> HandleAsync(CreateWidgetCommand command, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(CreateWidgetCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { Name = command.Name }, correlationId);
        
        try
        {
            var request = new ACS.Service.Requests.CreateWidgetRequest
            {
                Name = command.Name,
                Description = command.Description,
                CreatedBy = command.CreatedBy
            };
            
            var response = await _widgetService.CreateAsync(request);
            
            if (!response.Success || response.Widget == null)
                throw new InvalidOperationException(response.ErrorMessage ?? "Widget creation failed");
            
            LogCommandSuccess(_logger, context, new { WidgetId = response.Widget.Id }, correlationId);
            return response.Widget;
        }
        catch (Exception ex)
        {
            return HandleCommandError<Widget>(_logger, ex, context, correlationId);
        }
    }
}

// Query Handler - Gets a widget by ID
public class GetWidgetQueryHandler : IQueryHandler<GetWidgetQuery, Widget>
{
    private readonly IWidgetService _widgetService;
    private readonly ILogger<GetWidgetQueryHandler> _logger;

    public GetWidgetQueryHandler(IWidgetService widgetService, ILogger<GetWidgetQueryHandler> logger)
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
            var request = new ACS.Service.Requests.GetWidgetRequest
            {
                WidgetId = query.WidgetId
            };
            
            var response = await _widgetService.GetByIdAsync(request);
            
            if (!response.Success || response.Widget == null)
                throw new InvalidOperationException($"Widget {query.WidgetId} not found");
            
            LogQuerySuccess(_logger, context, new { WidgetId = response.Widget.Id }, correlationId);
            return response.Widget;
        }
        catch (Exception ex)
        {
            return HandleQueryError<Widget>(_logger, ex, context, correlationId);
        }
    }
}

// Query Handler - Gets all widgets with pagination
public class GetWidgetsQueryHandler : IQueryHandler<GetWidgetsQuery, List<Widget>>
{
    private readonly IWidgetService _widgetService;
    private readonly ILogger<GetWidgetsQueryHandler> _logger;

    public GetWidgetsQueryHandler(IWidgetService widgetService, ILogger<GetWidgetsQueryHandler> logger)
    {
        _widgetService = widgetService;
        _logger = logger;
    }

    public async Task<List<Widget>> HandleAsync(GetWidgetsQuery query, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetWidgetsQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { Page = query.Page, PageSize = query.PageSize }, correlationId);
        
        try
        {
            var request = new ACS.Service.Requests.GetWidgetsRequest
            {
                Page = query.Page,
                PageSize = query.PageSize,
                Search = query.Search
            };
            
            var response = await _widgetService.GetAllAsync(request);
            
            LogQuerySuccess(_logger, context, new { Count = response.Widgets.Count }, correlationId);
            return response.Widgets;
        }
        catch (Exception ex)
        {
            return HandleQueryError<List<Widget>>(_logger, ex, context, correlationId);
        }
    }
}
```

### Step 6: Register the Service

```csharp
// In ACS.Service or wherever services are registered
// Add this to your service registration:

services.AddScoped<IWidgetService, WidgetService>();
```

### Step 7: Test Your Handlers

The handlers are automatically registered! When you start the VerticalHost, you should see:

```
✅ Auto-registered: ICommandHandler`2 -> CreateWidgetCommandHandler
✅ Auto-registered: IQueryHandler`2 -> GetWidgetQueryHandler  
✅ Auto-registered: IQueryHandler`2 -> GetWidgetsQueryHandler
🎯 Total handlers auto-registered: 70
```

## Testing Your Implementation

### Unit Testing

```csharp
// Tests/WidgetHandlerTests.cs
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
        var command = new CreateWidgetCommand 
        { 
            Name = "Test Widget", 
            Description = "Test Description",
            CreatedBy = "test-user" 
        };
        
        var expectedWidget = new Widget { Id = 1, Name = "Test Widget" };
        var response = new CreateWidgetResponse { Widget = expectedWidget, Success = true };

        _mockWidgetService
            .Setup(s => s.CreateAsync(It.IsAny<CreateWidgetRequest>()))
            .ReturnsAsync(response);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(expectedWidget.Id, result.Id);
        Assert.AreEqual(expectedWidget.Name, result.Name);
    }
}
```

### Integration Testing with TestServer

```csharp
// Tests/WidgetIntegrationTests.cs
[TestClass]
public class WidgetIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public WidgetIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [TestMethod]
    public async Task CreateWidget_EndToEnd_Success()
    {
        // Arrange
        var client = _factory.CreateClient();
        var command = new CreateWidgetCommand 
        { 
            Name = "Integration Test Widget",
            Description = "Created via integration test",
            CreatedBy = "test-system"
        };

        // Act - This would go through HTTP API → gRPC → Handler chain
        var response = await client.PostAsJsonAsync("/api/widgets", command);

        // Assert
        response.EnsureSuccessStatusCode();
        var widget = await response.Content.ReadFromJsonAsync<Widget>();
        Assert.IsNotNull(widget);
        Assert.AreEqual(command.Name, widget.Name);
    }
}
```

## Debugging and Development Tips

### 1. Enable Detailed Logging

```json
// appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "ACS.VerticalHost.Handlers": "Debug",
      "ACS.Service.Services": "Debug",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  }
}
```

### 2. Use Health Checks for Diagnostics

```bash
# Check command buffer health
curl http://localhost:50051/health | jq '.entries.command_buffer'

# Check entity graph health  
curl http://localhost:50051/health | jq '.entries.entity_graph'
```

### 3. Monitor Handler Registration

Watch the console output when VerticalHost starts to see your handlers being registered:

```
✅ Auto-registered: ICommandHandler`2 -> CreateWidgetCommandHandler
✅ Auto-registered: IQueryHandler`2 -> GetWidgetQueryHandler
```

### 4. Debug Handler Execution

Add breakpoints in your handlers and step through the execution:

1. Set breakpoint in `HandleAsync` method
2. Make request through HTTP API or directly to gRPC
3. Step through business logic execution

### 5. Test gRPC Service Directly

```bash
# Install grpcurl for testing
go install github.com/fullstorydev/grpcurl/cmd/grpcurl@latest

# Test health check
grpcurl -plaintext localhost:50051 grpc.health.v1.Health/Check
```

## Code Standards and Best Practices

### 1. Handler Patterns

- **Always use error handling utilities**: `HandlerErrorHandling.HandleCommandError<T>()`
- **Include correlation IDs**: For request tracing
- **Log operation start and success**: Using structured logging
- **Validate inputs**: At the handler level
- **Delegate to services**: Handlers orchestrate, services contain business logic

### 2. Service Layer Patterns

- **Single responsibility**: One service per domain aggregate
- **Async all the way**: Use `async`/`await` consistently
- **Return response objects**: Don't return domain models directly
- **Handle exceptions gracefully**: Log and return error responses

### 3. Testing Standards

- **Unit test handlers**: Mock service dependencies
- **Integration test end-to-end**: HTTP API through to database
- **Test error conditions**: Not just happy path
- **Use meaningful test names**: Describe what's being tested

### 4. Performance Considerations

- **Batch operations**: For bulk data processing
- **Use pagination**: For large result sets
- **Leverage entity graph**: For fast in-memory lookups
- **Monitor metrics**: Command processing rates, memory usage

## Next Steps

### Explore Existing Features

1. **User Management**: Study `UserCommandHandlers.cs` and `UserService.cs`
2. **Group Hierarchy**: Understand the group relationship management
3. **Permission Evaluation**: See how permissions are cached and evaluated
4. **Audit Logging**: Learn the audit trail patterns

### Advanced Topics

1. **Custom Validation**: Implement domain-specific validation rules
2. **Event Sourcing**: Add event publishing to your handlers
3. **Caching Strategies**: Implement custom caching for your services
4. **Performance Optimization**: Use the in-memory entity graph effectively

### Contribute to the System

1. **Add new domain features**: Following the patterns you've learned
2. **Improve error handling**: Add more specific error types
3. **Enhance testing**: Add more comprehensive test coverage
4. **Optimize performance**: Profile and improve slow operations

Congratulations! You now understand the ACS vertical slice architecture and can create new features following the established patterns. The auto-registration system will discover your handlers automatically, and the CQRS patterns provide a clean separation between read and write operations.