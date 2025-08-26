namespace ACS.Service.Services;

/// <summary>
/// Interface for cache invalidation service
/// </summary>
public interface ICacheInvalidationService
{
    Task InvalidateAsync(string key, CancellationToken cancellationToken = default);
    Task InvalidateByPatternAsync(string pattern, CancellationToken cancellationToken = default);
    Task InvalidateByTagAsync(string tag, CancellationToken cancellationToken = default);
    Task ClearAllAsync(CancellationToken cancellationToken = default);
}