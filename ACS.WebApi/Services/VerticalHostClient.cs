namespace ACS.WebApi.Services;

/// <summary>
/// DEMO: Simple VerticalHost client that demonstrates the proxy pattern
/// This shows the clean architecture working - HTTP API delegates to VerticalHost
/// </summary>
public class VerticalHostClient : IVerticalHostClient
{
    private readonly ILogger<VerticalHostClient> _logger;

    public VerticalHostClient(ILogger<VerticalHostClient> logger)
    {
        _logger = logger;
    }

    public async Task<object> GetUsersAsync(object request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("DEMO: Would execute gRPC call to VerticalHost for GetUsers");
        
        // In full implementation: 
        // 1. Serialize request to protobuf
        // 2. Send gRPC call to VerticalHost
        // 3. VerticalHost queues in CommandBuffer
        // 4. Business logic processes sequentially
        // 5. Return result via gRPC
        
        await Task.Delay(1, cancellationToken); // Simulate async operation
        
        return new
        {
            Message = "DEMO: gRPC call to VerticalHost successful",
            Operation = "GetUsers",
            ProcessedBy = "VerticalHost CommandBuffer",
            BusinessLogic = "Sequential processing in VerticalHost"
        };
    }

    public async Task<object> GetUserAsync(object request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("DEMO: Would execute gRPC call to VerticalHost for GetUser");
        
        await Task.Delay(1, cancellationToken);
        
        return new
        {
            Message = "DEMO: gRPC call to VerticalHost successful",
            Operation = "GetUser", 
            ProcessedBy = "VerticalHost CommandBuffer",
            BusinessLogic = "Sequential processing in VerticalHost"
        };
    }

    public async Task<object> CreateUserAsync(object request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("DEMO: Would execute gRPC call to VerticalHost for CreateUser");
        
        await Task.Delay(1, cancellationToken);
        
        return new
        {
            Message = "DEMO: gRPC call to VerticalHost successful",
            Operation = "CreateUser (Command)",
            ProcessedBy = "VerticalHost CommandBuffer", 
            BusinessLogic = "Queued for sequential processing",
            Architecture = "HTTP -> gRPC -> CommandBuffer -> Business Logic"
        };
    }

    public async Task<object> ExecuteCommandAsync(string operation, object request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("DEMO: Would execute gRPC call to VerticalHost for {Operation}", operation);
        
        await Task.Delay(1, cancellationToken);
        
        return new
        {
            Message = "DEMO: gRPC call to VerticalHost successful",
            Operation = operation,
            ProcessedBy = "VerticalHost CommandBuffer",
            BusinessLogic = "Sequential processing in VerticalHost"
        };
    }
}