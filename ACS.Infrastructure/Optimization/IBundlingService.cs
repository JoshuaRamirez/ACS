namespace ACS.Infrastructure.Optimization;

/// <summary>
/// Service for bundling and optimizing static assets
/// </summary>
public interface IBundlingService
{
    /// <summary>
    /// Creates a bundle from multiple files
    /// </summary>
    Task<Bundle> CreateBundleAsync(string name, IEnumerable<string> filePaths, BundleType type);
    
    /// <summary>
    /// Gets a bundle by name
    /// </summary>
    Task<Bundle> GetBundleAsync(string name);
    
    /// <summary>
    /// Gets the content of a bundle
    /// </summary>
    Task<string> GetBundleContentAsync(string name);
    
    /// <summary>
    /// Invalidates a bundle cache
    /// </summary>
    Task InvalidateBundleAsync(string name);
    
    /// <summary>
    /// Registers bundles from configuration
    /// </summary>
    Task RegisterBundlesAsync();
    
    /// <summary>
    /// Gets bundle URL with version for cache busting
    /// </summary>
    string GetBundleUrl(string name);
}

/// <summary>
/// Represents a bundle of files
/// </summary>
public class Bundle
{
    public string Name { get; set; } = string.Empty;
    public BundleType Type { get; set; }
    public List<string> Files { get; set; } = new();
    public string Content { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsMinified { get; set; }
    public bool IsCompressed { get; set; }
    public long OriginalSize { get; set; }
    public long BundledSize { get; set; }
}

/// <summary>
/// Bundle types
/// </summary>
public enum BundleType
{
    JavaScript,
    Css,
    Mixed
}