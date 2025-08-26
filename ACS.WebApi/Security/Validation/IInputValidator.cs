using System.ComponentModel.DataAnnotations;

namespace ACS.WebApi.Security.Validation;

/// <summary>
/// Interface for input validation and sanitization service
/// </summary>
public interface IInputValidator
{
    /// <summary>
    /// Validate and sanitize input string
    /// </summary>
    ValidationResult ValidateAndSanitize(string? input, ValidationContext context);

    /// <summary>
    /// Validate input against XSS patterns
    /// </summary>
    bool ContainsXssPattern(string? input);

    /// <summary>
    /// Validate input against SQL injection patterns
    /// </summary>
    bool ContainsSqlInjectionPattern(string? input);

    /// <summary>
    /// Validate input against command injection patterns
    /// </summary>
    bool ContainsCommandInjectionPattern(string? input);

    /// <summary>
    /// Validate input against path traversal patterns
    /// </summary>
    bool ContainsPathTraversalPattern(string? input);

    /// <summary>
    /// Sanitize HTML content
    /// </summary>
    string SanitizeHtml(string? html);

    /// <summary>
    /// Sanitize SQL input
    /// </summary>
    string SanitizeSql(string? sql);

    /// <summary>
    /// Sanitize file path
    /// </summary>
    string SanitizePath(string? path);

    /// <summary>
    /// Sanitize URL
    /// </summary>
    string SanitizeUrl(string? url);

    /// <summary>
    /// Sanitize JSON content
    /// </summary>
    string SanitizeJson(string? json);

    /// <summary>
    /// Validate email format
    /// </summary>
    bool IsValidEmail(string? email);

    /// <summary>
    /// Validate URL format
    /// </summary>
    bool IsValidUrl(string? url);

    /// <summary>
    /// Validate file name
    /// </summary>
    bool IsValidFileName(string? fileName);

    /// <summary>
    /// Validate and sanitize complex object
    /// </summary>
    T? ValidateAndSanitizeObject<T>(T? obj) where T : class;

    /// <summary>
    /// Get validation errors for an object
    /// </summary>
    IEnumerable<ValidationResult> GetValidationErrors(object obj);
}

/// <summary>
/// Input validation options
/// </summary>
public class InputValidationOptions
{
    /// <summary>
    /// Maximum allowed string length
    /// </summary>
    public int MaxStringLength { get; set; } = 4000;

    /// <summary>
    /// Maximum allowed URL length
    /// </summary>
    public int MaxUrlLength { get; set; } = 2048;

    /// <summary>
    /// Maximum allowed file name length
    /// </summary>
    public int MaxFileNameLength { get; set; } = 255;

    /// <summary>
    /// Allow HTML tags in input
    /// </summary>
    public bool AllowHtmlTags { get; set; } = false;

    /// <summary>
    /// Allowed HTML tags if HTML is permitted
    /// </summary>
    public HashSet<string> AllowedHtmlTags { get; set; } = new()
    {
        "p", "br", "strong", "em", "u", "ol", "ul", "li", "a", "span"
    };

    /// <summary>
    /// Allowed HTML attributes
    /// </summary>
    public HashSet<string> AllowedHtmlAttributes { get; set; } = new()
    {
        "href", "title", "class", "id"
    };

    /// <summary>
    /// Block dangerous file extensions
    /// </summary>
    public HashSet<string> BlockedFileExtensions { get; set; } = new()
    {
        ".exe", ".dll", ".bat", ".cmd", ".com", ".pif", ".scr", ".vbs", ".js", ".jar", ".zip", ".rar"
    };

    /// <summary>
    /// XSS patterns to detect
    /// </summary>
    public List<string> XssPatterns { get; set; } = new()
    {
        @"<script[\s\S]*?>[\s\S]*?</script>",
        @"javascript:",
        @"on\w+\s*=",
        @"<iframe[\s\S]*?>",
        @"<embed[\s\S]*?>",
        @"<object[\s\S]*?>",
        @"eval\s*\(",
        @"expression\s*\(",
        @"vbscript:",
        @"onload\s*=",
        @"onerror\s*=",
        @"onclick\s*=",
        @"<svg[\s\S]*?onload[\s\S]*?>"
    };

    /// <summary>
    /// SQL injection patterns to detect
    /// </summary>
    public List<string> SqlInjectionPatterns { get; set; } = new()
    {
        @"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|EXEC|EXECUTE|UNION|FROM|WHERE|ORDER BY|GROUP BY|HAVING)\b)",
        @"(--|\#|\/\*|\*\/)",
        @"(;|\||&&)",
        @"(\bOR\b|\bAND\b)[\s]*'[\s]*=",
        @"(\bOR\b|\bAND\b)[\s]*""[\s]*=",
        @"(\bOR\b|\bAND\b)[\s]*'[\s]*<",
        @"(\bOR\b|\bAND\b)[\s]*""[\s]*<",
        @"(\bOR\b|\bAND\b)[\s]*'[\s]*>",
        @"(\bOR\b|\bAND\b)[\s]*""[\s]*>",
        @"'[\s]*(\bOR\b|\bAND\b)",
        @"""[\s]*(\bOR\b|\bAND\b)",
        @"(xp_|sp_|@@)",
        @"(WAITFOR|DELAY|BENCHMARK)",
        @"(INTO\s+(OUTFILE|DUMPFILE))"
    };

    /// <summary>
    /// Command injection patterns to detect
    /// </summary>
    public List<string> CommandInjectionPatterns { get; set; } = new()
    {
        @"[;&|`$]",
        @">\s*\/",
        @"<\s*\/",
        @"\$\(",
        @"\$\{",
        @"&&",
        @"\|\|",
        @"cmd\.exe",
        @"powershell",
        @"bash",
        @"sh\s+-c"
    };

    /// <summary>
    /// Path traversal patterns to detect
    /// </summary>
    public List<string> PathTraversalPatterns { get; set; } = new()
    {
        @"\.\.[/\\]",
        @"\.\.%2[fF]",
        @"\.\.%5[cC]",
        @"%2[eE]\.",
        @"%252[eE]",
        @"\.\./",
        @"\.\.\\",
        @"/etc/passwd",
        @"C:\\Windows",
        @"C:\\\\Windows"
    };
}