namespace ACS.Infrastructure.Optimization;

/// <summary>
/// Service for content minification
/// </summary>
public interface IMinificationService
{
    /// <summary>
    /// Minifies JSON content
    /// </summary>
    string MinifyJson(string json);
    
    /// <summary>
    /// Minifies JavaScript content
    /// </summary>
    string MinifyJavaScript(string javascript);
    
    /// <summary>
    /// Minifies CSS content
    /// </summary>
    string MinifyCss(string css);
    
    /// <summary>
    /// Minifies HTML content
    /// </summary>
    string MinifyHtml(string html);
    
    /// <summary>
    /// Determines if content should be minified
    /// </summary>
    bool ShouldMinify(string contentType, bool isDevelopment);
    
    /// <summary>
    /// Minifies content based on content type
    /// </summary>
    Task<string> MinifyAsync(string content, string contentType);
}