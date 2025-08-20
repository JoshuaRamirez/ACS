using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace ACS.WebApi.Security.Validation;

/// <summary>
/// Validates that input doesn't contain XSS patterns
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class NoXssAttribute : ValidationAttribute
{
    private static readonly string[] XssPatterns = new[]
    {
        @"<script[\s\S]*?>",
        @"javascript:",
        @"on\w+\s*=",
        @"<iframe[\s\S]*?>",
        @"eval\s*\(",
        @"expression\s*\("
    };

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null) return ValidationResult.Success;

        var stringValue = value.ToString();
        if (string.IsNullOrWhiteSpace(stringValue)) return ValidationResult.Success;

        foreach (var pattern in XssPatterns)
        {
            if (Regex.IsMatch(stringValue, pattern, RegexOptions.IgnoreCase))
            {
                return new ValidationResult($"The field {validationContext.DisplayName} contains potentially dangerous content.");
            }
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates that input doesn't contain SQL injection patterns
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class NoSqlInjectionAttribute : ValidationAttribute
{
    private static readonly string[] SqlPatterns = new[]
    {
        @"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|EXEC|EXECUTE|UNION)\b)",
        @"(--|\#|\/\*|\*\/)",
        @"(\bOR\b|\bAND\b)[\s]*['\"]?[\s]*[=<>]"
    };

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null) return ValidationResult.Success;

        var stringValue = value.ToString();
        if (string.IsNullOrWhiteSpace(stringValue)) return ValidationResult.Success;

        foreach (var pattern in SqlPatterns)
        {
            if (Regex.IsMatch(stringValue, pattern, RegexOptions.IgnoreCase))
            {
                return new ValidationResult($"The field {validationContext.DisplayName} contains invalid SQL patterns.");
            }
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates that input is a safe file name
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class SafeFileNameAttribute : ValidationAttribute
{
    private static readonly string[] BlockedExtensions = new[]
    {
        ".exe", ".dll", ".bat", ".cmd", ".com", ".pif", ".scr", ".vbs", ".js", ".jar"
    };

    public int MaxLength { get; set; } = 255;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null) return ValidationResult.Success;

        var fileName = value.ToString();
        if (string.IsNullOrWhiteSpace(fileName)) return ValidationResult.Success;

        // Check length
        if (fileName.Length > MaxLength)
        {
            return new ValidationResult($"File name cannot exceed {MaxLength} characters.");
        }

        // Check for invalid characters
        var invalidChars = Path.GetInvalidFileNameChars();
        if (fileName.Any(c => invalidChars.Contains(c)))
        {
            return new ValidationResult("File name contains invalid characters.");
        }

        // Check for path traversal
        if (fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\"))
        {
            return new ValidationResult("File name contains invalid path characters.");
        }

        // Check for blocked extensions
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        if (!string.IsNullOrEmpty(extension) && BlockedExtensions.Contains(extension))
        {
            return new ValidationResult($"File type '{extension}' is not allowed.");
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates that input is a safe URL
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class SafeUrlAttribute : ValidationAttribute
{
    public bool RequireHttps { get; set; } = false;
    public int MaxLength { get; set; } = 2048;
    public string[]? AllowedDomains { get; set; }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null) return ValidationResult.Success;

        var url = value.ToString();
        if (string.IsNullOrWhiteSpace(url)) return ValidationResult.Success;

        // Check length
        if (url.Length > MaxLength)
        {
            return new ValidationResult($"URL cannot exceed {MaxLength} characters.");
        }

        // Validate URL format
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return new ValidationResult("Invalid URL format.");
        }

        // Check scheme
        if (RequireHttps && uri.Scheme != Uri.UriSchemeHttps)
        {
            return new ValidationResult("URL must use HTTPS.");
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return new ValidationResult("URL must use HTTP or HTTPS.");
        }

        // Check for dangerous patterns
        if (url.Contains("javascript:", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("vbscript:", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("data:", StringComparison.OrdinalIgnoreCase))
        {
            return new ValidationResult("URL contains potentially dangerous content.");
        }

        // Check allowed domains
        if (AllowedDomains != null && AllowedDomains.Length > 0)
        {
            if (!AllowedDomains.Any(d => uri.Host.Equals(d, StringComparison.OrdinalIgnoreCase)))
            {
                return new ValidationResult($"URL domain '{uri.Host}' is not allowed.");
            }
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates that input doesn't contain path traversal patterns
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class NoPathTraversalAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null) return ValidationResult.Success;

        var path = value.ToString();
        if (string.IsNullOrWhiteSpace(path)) return ValidationResult.Success;

        // Check for path traversal patterns
        var patterns = new[]
        {
            @"\.\.[/\\]",
            @"\.\.%2[fF]",
            @"\.\.%5[cC]",
            @"%2[eE]\.",
            @"\.\./",
            @"\.\.\\",
            @"/etc/",
            @"C:\\",
            @"C:\\\\"
        };

        foreach (var pattern in patterns)
        {
            if (Regex.IsMatch(path, pattern, RegexOptions.IgnoreCase))
            {
                return new ValidationResult($"The field {validationContext.DisplayName} contains invalid path patterns.");
            }
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates password complexity requirements
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class SecurePasswordAttribute : ValidationAttribute
{
    public int MinLength { get; set; } = 8;
    public int MaxLength { get; set; } = 128;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireDigit { get; set; } = true;
    public bool RequireSpecialChar { get; set; } = true;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null) return new ValidationResult("Password is required.");

        var password = value.ToString();
        if (string.IsNullOrWhiteSpace(password)) return new ValidationResult("Password is required.");

        // Check length
        if (password.Length < MinLength)
        {
            return new ValidationResult($"Password must be at least {MinLength} characters long.");
        }

        if (password.Length > MaxLength)
        {
            return new ValidationResult($"Password cannot exceed {MaxLength} characters.");
        }

        // Check complexity requirements
        if (RequireUppercase && !password.Any(char.IsUpper))
        {
            return new ValidationResult("Password must contain at least one uppercase letter.");
        }

        if (RequireLowercase && !password.Any(char.IsLower))
        {
            return new ValidationResult("Password must contain at least one lowercase letter.");
        }

        if (RequireDigit && !password.Any(char.IsDigit))
        {
            return new ValidationResult("Password must contain at least one digit.");
        }

        if (RequireSpecialChar && !Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]"))
        {
            return new ValidationResult("Password must contain at least one special character.");
        }

        // Check for common weak passwords
        var weakPasswords = new[]
        {
            "password", "12345678", "qwerty", "abc123", "password123", "admin", "letmein"
        };

        if (weakPasswords.Any(weak => password.Contains(weak, StringComparison.OrdinalIgnoreCase)))
        {
            return new ValidationResult("Password is too weak. Please choose a stronger password.");
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates that input is alphanumeric with optional allowed characters
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class AlphanumericAttribute : ValidationAttribute
{
    public string AllowedCharacters { get; set; } = "-_.";
    public bool AllowSpaces { get; set; } = false;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null) return ValidationResult.Success;

        var input = value.ToString();
        if (string.IsNullOrWhiteSpace(input)) return ValidationResult.Success;

        var pattern = $@"^[a-zA-Z0-9{Regex.Escape(AllowedCharacters)}";
        if (AllowSpaces) pattern += @"\s";
        pattern += "]+$";

        if (!Regex.IsMatch(input, pattern))
        {
            var allowed = "letters, numbers";
            if (!string.IsNullOrEmpty(AllowedCharacters)) allowed += $", and '{AllowedCharacters}'";
            if (AllowSpaces) allowed += ", and spaces";
            
            return new ValidationResult($"The field {validationContext.DisplayName} can only contain {allowed}.");
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates input length with configurable limits
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class SafeLengthAttribute : ValidationAttribute
{
    public int MinLength { get; set; } = 0;
    public int MaxLength { get; set; } = 4000;
    public bool TrimWhitespace { get; set; } = true;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null) return ValidationResult.Success;

        var input = value.ToString();
        if (TrimWhitespace) input = input?.Trim() ?? string.Empty;

        if (input.Length < MinLength)
        {
            return new ValidationResult($"The field {validationContext.DisplayName} must be at least {MinLength} characters long.");
        }

        if (input.Length > MaxLength)
        {
            return new ValidationResult($"The field {validationContext.DisplayName} cannot exceed {MaxLength} characters.");
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates JSON structure and content
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class ValidJsonAttribute : ValidationAttribute
{
    public int MaxDepth { get; set; } = 10;
    public int MaxLength { get; set; } = 65536;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null) return ValidationResult.Success;

        var json = value.ToString();
        if (string.IsNullOrWhiteSpace(json)) return ValidationResult.Success;

        // Check length
        if (json.Length > MaxLength)
        {
            return new ValidationResult($"JSON cannot exceed {MaxLength} characters.");
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            
            // Check depth
            var depth = GetJsonDepth(document.RootElement);
            if (depth > MaxDepth)
            {
                return new ValidationResult($"JSON depth cannot exceed {MaxDepth} levels.");
            }

            return ValidationResult.Success;
        }
        catch (System.Text.Json.JsonException)
        {
            return new ValidationResult($"The field {validationContext.DisplayName} must contain valid JSON.");
        }
    }

    private int GetJsonDepth(System.Text.Json.JsonElement element, int currentDepth = 0)
    {
        if (currentDepth > MaxDepth) return currentDepth;

        var maxDepth = currentDepth;

        if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var depth = GetJsonDepth(property.Value, currentDepth + 1);
                maxDepth = Math.Max(maxDepth, depth);
            }
        }
        else if (element.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var depth = GetJsonDepth(item, currentDepth + 1);
                maxDepth = Math.Max(maxDepth, depth);
            }
        }

        return maxDepth;
    }
}