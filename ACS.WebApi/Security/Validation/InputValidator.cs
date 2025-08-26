using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ACS.WebApi.Security.Validation;

/// <summary>
/// Service for input validation and sanitization
/// </summary>
public class InputValidator : IInputValidator
{
    private readonly InputValidationOptions _options;
    private readonly ILogger<InputValidator> _logger;

    public InputValidator(IOptions<InputValidationOptions> options, ILogger<InputValidator> logger)
    {
        _options = options?.Value ?? new InputValidationOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
    }


    public ValidationResult ValidateAndSanitize(string? input, ValidationContext context)
    {
        if (string.IsNullOrEmpty(input))
            return ValidationResult.Success!;

        // Check length
        if (input.Length > _options.MaxStringLength)
        {
            return new ValidationResult($"Input exceeds maximum length of {_options.MaxStringLength} characters");
        }

        // Check for XSS
        if (ContainsXssPattern(input))
        {
            _logger.LogWarning("XSS pattern detected in input for {MemberName}", context.MemberName);
            return new ValidationResult("Input contains potentially dangerous content");
        }

        // Check for SQL injection
        if (ContainsSqlInjectionPattern(input))
        {
            _logger.LogWarning("SQL injection pattern detected in input for {MemberName}", context.MemberName);
            return new ValidationResult("Input contains invalid SQL patterns");
        }

        // Check for command injection
        if (ContainsCommandInjectionPattern(input))
        {
            _logger.LogWarning("Command injection pattern detected in input for {MemberName}", context.MemberName);
            return new ValidationResult("Input contains invalid command patterns");
        }

        // Check for path traversal
        if (ContainsPathTraversalPattern(input))
        {
            _logger.LogWarning("Path traversal pattern detected in input for {MemberName}", context.MemberName);
            return new ValidationResult("Input contains invalid path patterns");
        }

        return ValidationResult.Success!;
    }

    public bool ContainsXssPattern(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var lowerInput = input.ToLowerInvariant();
        
        foreach (var pattern in _options.XssPatterns)
        {
            if (Regex.IsMatch(lowerInput, pattern, RegexOptions.IgnoreCase))
            {
                _logger.LogDebug("XSS pattern '{Pattern}' matched", pattern);
                return true;
            }
        }

        return false;
    }

    public bool ContainsSqlInjectionPattern(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        foreach (var pattern in _options.SqlInjectionPatterns)
        {
            if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
            {
                _logger.LogDebug("SQL injection pattern '{Pattern}' matched", pattern);
                return true;
            }
        }

        return false;
    }

    public bool ContainsCommandInjectionPattern(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        foreach (var pattern in _options.CommandInjectionPatterns)
        {
            if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
            {
                _logger.LogDebug("Command injection pattern '{Pattern}' matched", pattern);
                return true;
            }
        }

        return false;
    }

    public bool ContainsPathTraversalPattern(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        foreach (var pattern in _options.PathTraversalPatterns)
        {
            if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
            {
                _logger.LogDebug("Path traversal pattern '{Pattern}' matched", pattern);
                return true;
            }
        }

        return false;
    }

    public string SanitizeHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        if (!_options.AllowHtmlTags)
        {
            // Strip all HTML if not allowed
            return HttpUtility.HtmlEncode(html);
        }

        // Use HTML sanitizer to clean the HTML
        return HttpUtility.HtmlEncode(html);
    }

    public string SanitizeSql(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return string.Empty;

        // Basic SQL sanitization - in production, always use parameterized queries
        var sanitized = sql
            .Replace("'", "''")
            .Replace(";", "")
            .Replace("--", "")
            .Replace("/*", "")
            .Replace("*/", "")
            .Replace("xp_", "")
            .Replace("sp_", "");

        return sanitized;
    }

    public string SanitizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        // Remove path traversal attempts
        var sanitized = path
            .Replace("..", "")
            .Replace("~", "")
            .Replace("//", "/")
            .Replace("\\\\", "\\");

        // Remove invalid path characters
        var invalidChars = Path.GetInvalidPathChars();
        foreach (var c in invalidChars)
        {
            sanitized = sanitized.Replace(c.ToString(), "");
        }

        return sanitized;
    }

    public string SanitizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        // Check URL length
        if (url.Length > _options.MaxUrlLength)
        {
            _logger.LogWarning("URL exceeds maximum length of {MaxLength}", _options.MaxUrlLength);
            return string.Empty;
        }

        // Validate URL format
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        // Only allow HTTP and HTTPS
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            _logger.LogWarning("Invalid URL scheme: {Scheme}", uri.Scheme);
            return string.Empty;
        }

        // Remove dangerous characters
        var sanitized = url
            .Replace("<", "%3C")
            .Replace(">", "%3E")
            .Replace("\"", "%22")
            .Replace("'", "%27")
            .Replace("javascript:", "")
            .Replace("vbscript:", "")
            .Replace("data:", "");

        return sanitized;
    }

    public string SanitizeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "{}";

        try
        {
            // Parse and re-serialize to ensure valid JSON
            var parsed = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(parsed.RootElement);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON detected");
            return "{}";
        }
    }

    public bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    public bool IsValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (url.Length > _options.MaxUrlLength)
            return false;

        return Uri.TryCreate(url, UriKind.Absolute, out var result) &&
               (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }

    public bool IsValidFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        if (fileName.Length > _options.MaxFileNameLength)
            return false;

        // Check for invalid characters
        var invalidChars = Path.GetInvalidFileNameChars();
        if (fileName.Any(c => invalidChars.Contains(c)))
            return false;

        // Check for blocked extensions
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        if (!string.IsNullOrEmpty(extension) && _options.BlockedFileExtensions.Contains(extension))
        {
            _logger.LogWarning("Blocked file extension detected: {Extension}", extension);
            return false;
        }

        // Check for path traversal in filename
        if (fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\"))
            return false;

        return true;
    }

    public T? ValidateAndSanitizeObject<T>(T? obj) where T : class
    {
        if (obj == null)
            return obj;

        var type = obj.GetType();
        var properties = type.GetProperties()
            .Where(p => p.CanRead && p.CanWrite && p.PropertyType == typeof(string));

        foreach (var property in properties)
        {
            var value = property.GetValue(obj) as string;
            if (!string.IsNullOrEmpty(value))
            {
                // Sanitize based on property attributes or default sanitization
                var sanitized = SanitizeString(value, property.Name);
                property.SetValue(obj, sanitized);
            }
        }

        // Validate the object
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(obj);
        
        if (!Validator.TryValidateObject(obj, validationContext, validationResults, true))
        {
            var errors = string.Join(", ", validationResults.Select(r => r.ErrorMessage));
            throw new ValidationException($"Validation failed: {errors}");
        }

        return obj;
    }

    public IEnumerable<ValidationResult> GetValidationErrors(object obj)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(obj);
        Validator.TryValidateObject(obj, validationContext, validationResults, true);
        return validationResults;
    }

    private string SanitizeString(string input, string propertyName)
    {
        // Default sanitization - encode HTML
        var sanitized = HttpUtility.HtmlEncode(input);

        // Additional sanitization based on property name patterns
        if (propertyName.Contains("Email", StringComparison.OrdinalIgnoreCase))
        {
            // Email-specific sanitization
            sanitized = input.Trim().ToLowerInvariant();
        }
        else if (propertyName.Contains("Url", StringComparison.OrdinalIgnoreCase) ||
                 propertyName.Contains("Uri", StringComparison.OrdinalIgnoreCase))
        {
            // URL-specific sanitization
            sanitized = SanitizeUrl(input);
        }
        else if (propertyName.Contains("Path", StringComparison.OrdinalIgnoreCase) ||
                 propertyName.Contains("File", StringComparison.OrdinalIgnoreCase))
        {
            // Path-specific sanitization
            sanitized = SanitizePath(input);
        }

        return sanitized;
    }
}