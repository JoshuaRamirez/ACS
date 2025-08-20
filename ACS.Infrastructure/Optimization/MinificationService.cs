using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ACS.Infrastructure.Optimization;

/// <summary>
/// Implementation of minification service
/// </summary>
public class MinificationService : IMinificationService
{
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MinificationService> _logger;
    private readonly MinificationSettings _settings;
    
    // Regex patterns for minification
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex CommentRegex = new(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex LineCommentRegex = new(@"//.*?$", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex HtmlCommentRegex = new(@"<!--.*?-->", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex ExtraSpacesRegex = new(@"\s{2,}", RegexOptions.Compiled);

    public MinificationService(
        IHostEnvironment environment,
        IConfiguration configuration,
        ILogger<MinificationService> logger)
    {
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
        _settings = LoadMinificationSettings();
    }

    public string MinifyJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return json;

        try
        {
            // Parse and re-serialize without formatting
            using var document = JsonDocument.Parse(json);
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Indented = false,
                SkipValidation = false
            });
            
            document.WriteTo(writer);
            writer.Flush();
            
            var minified = Encoding.UTF8.GetString(stream.ToArray());
            
            _logger.LogTrace("Minified JSON from {Original} to {Minified} bytes", 
                json.Length, minified.Length);
            
            return minified;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to minify JSON, returning original");
            return json;
        }
    }

    public string MinifyJavaScript(string javascript)
    {
        if (string.IsNullOrWhiteSpace(javascript))
            return javascript;

        try
        {
            var minified = javascript;
            
            // Remove comments
            minified = CommentRegex.Replace(minified, string.Empty);
            minified = LineCommentRegex.Replace(minified, string.Empty);
            
            // Remove unnecessary whitespace
            minified = minified.Replace("\r\n", "\n");
            minified = minified.Replace("\r", "\n");
            
            // Remove whitespace around operators
            minified = Regex.Replace(minified, @"\s*([=+\-*/(){},;:])\s*", "$1");
            
            // Remove leading/trailing whitespace
            minified = minified.Trim();
            
            // Collapse multiple spaces
            minified = ExtraSpacesRegex.Replace(minified, " ");
            
            _logger.LogTrace("Minified JavaScript from {Original} to {Minified} bytes", 
                javascript.Length, minified.Length);
            
            return minified;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to minify JavaScript, returning original");
            return javascript;
        }
    }

    public string MinifyCss(string css)
    {
        if (string.IsNullOrWhiteSpace(css))
            return css;

        try
        {
            var minified = css;
            
            // Remove comments
            minified = CommentRegex.Replace(minified, string.Empty);
            
            // Remove unnecessary whitespace
            minified = minified.Replace("\r\n", string.Empty);
            minified = minified.Replace("\r", string.Empty);
            minified = minified.Replace("\n", string.Empty);
            minified = minified.Replace("\t", string.Empty);
            
            // Remove whitespace around CSS operators
            minified = Regex.Replace(minified, @"\s*([{}:;,])\s*", "$1");
            
            // Remove unnecessary semicolons
            minified = minified.Replace(";}", "}");
            
            // Remove leading/trailing whitespace
            minified = minified.Trim();
            
            // Collapse multiple spaces
            minified = ExtraSpacesRegex.Replace(minified, " ");
            
            _logger.LogTrace("Minified CSS from {Original} to {Minified} bytes", 
                css.Length, minified.Length);
            
            return minified;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to minify CSS, returning original");
            return css;
        }
    }

    public string MinifyHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return html;

        try
        {
            var minified = html;
            
            // Remove HTML comments
            minified = HtmlCommentRegex.Replace(minified, string.Empty);
            
            // Remove whitespace between tags
            minified = Regex.Replace(minified, @">\s+<", "><");
            
            // Remove unnecessary whitespace
            minified = minified.Replace("\r\n", " ");
            minified = minified.Replace("\r", " ");
            minified = minified.Replace("\n", " ");
            minified = minified.Replace("\t", " ");
            
            // Collapse multiple spaces
            minified = ExtraSpacesRegex.Replace(minified, " ");
            
            // Remove leading/trailing whitespace
            minified = minified.Trim();
            
            _logger.LogTrace("Minified HTML from {Original} to {Minified} bytes", 
                html.Length, minified.Length);
            
            return minified;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to minify HTML, returning original");
            return html;
        }
    }

    public bool ShouldMinify(string contentType, bool isDevelopment)
    {
        // Don't minify in development unless explicitly enabled
        if (isDevelopment && !_settings.MinifyInDevelopment)
            return false;
        
        if (!_settings.EnableMinification)
            return false;
        
        if (string.IsNullOrEmpty(contentType))
            return false;
        
        var lowerType = contentType.ToLowerInvariant();
        
        return lowerType.Contains("json") ||
               lowerType.Contains("javascript") ||
               lowerType.Contains("css") ||
               lowerType.Contains("html") ||
               lowerType.Contains("xml");
    }

    public async Task<string> MinifyAsync(string content, string contentType)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content;
        
        var lowerType = contentType?.ToLowerInvariant() ?? string.Empty;
        
        return await Task.Run(() =>
        {
            if (lowerType.Contains("json"))
                return MinifyJson(content);
            
            if (lowerType.Contains("javascript") || lowerType.Contains("x-javascript"))
                return MinifyJavaScript(content);
            
            if (lowerType.Contains("css"))
                return MinifyCss(content);
            
            if (lowerType.Contains("html"))
                return MinifyHtml(content);
            
            return content;
        });
    }

    private MinificationSettings LoadMinificationSettings()
    {
        var settings = new MinificationSettings();
        _configuration.GetSection("Minification").Bind(settings);
        return settings;
    }
}

/// <summary>
/// Minification configuration settings
/// </summary>
public class MinificationSettings
{
    public bool EnableMinification { get; set; } = true;
    public bool MinifyInDevelopment { get; set; } = false;
    public bool MinifyJson { get; set; } = true;
    public bool MinifyJavaScript { get; set; } = true;
    public bool MinifyCss { get; set; } = true;
    public bool MinifyHtml { get; set; } = true;
}