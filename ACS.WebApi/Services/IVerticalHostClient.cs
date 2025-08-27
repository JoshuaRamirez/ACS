namespace ACS.WebApi.Services;

/// <summary>
/// DEMO: Pure gRPC client interface for HTTP API to communicate with VerticalHost
/// HTTP API should ONLY depend on this interface - no business services
/// SIMPLIFIED VERSION for demonstration of clean architecture
/// </summary>
public interface IVerticalHostClient
{
    // Demo methods - in full implementation would have complete gRPC operations
    Task<object> GetUsersAsync(object request, CancellationToken cancellationToken = default);
    Task<object> GetUserAsync(object request, CancellationToken cancellationToken = default);
    Task<object> CreateUserAsync(object request, CancellationToken cancellationToken = default);
    
    // Placeholder for other operations
    Task<object> ExecuteCommandAsync(string operation, object request, CancellationToken cancellationToken = default);
}