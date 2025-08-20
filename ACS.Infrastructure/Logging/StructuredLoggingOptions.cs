namespace ACS.Infrastructure.Logging;

/// <summary>
/// Configuration options for structured logging
/// </summary>
public class StructuredLoggingOptions
{
    /// <summary>
    /// Whether structured logging is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether to include correlation ID in log entries
    /// </summary>
    public bool IncludeCorrelationId { get; set; } = true;

    /// <summary>
    /// Whether to include request path in log entries
    /// </summary>
    public bool IncludeRequestPath { get; set; } = true;

    /// <summary>
    /// Whether to include user context in log entries
    /// </summary>
    public bool IncludeUserContext { get; set; } = true;

    /// <summary>
    /// Whether to include timestamp in log entries
    /// </summary>
    public bool IncludeTimestamp { get; set; } = true;

    /// <summary>
    /// Log output format (Json, Text, Compact)
    /// </summary>
    public string Format { get; set; } = "Json";

    /// <summary>
    /// List of log levels to exclude from correlation enrichment
    /// </summary>
    public string[] ExcludedLogLevels { get; set; } = Array.Empty<string>();

    /// <summary>
    /// List of categories to exclude from correlation enrichment
    /// </summary>
    public string[] ExcludedCategories { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Maximum length for correlation ID (0 = no limit)
    /// </summary>
    public int MaxCorrelationIdLength { get; set; } = 0;

    /// <summary>
    /// Whether to sanitize sensitive data in logs
    /// </summary>
    public bool SanitizeSensitiveData { get; set; } = true;

    /// <summary>
    /// List of property names considered sensitive
    /// </summary>
    public string[] SensitiveProperties { get; set; } = new[]
    {
        "password",
        "token",
        "secret",
        "key",
        "authorization",
        "credentials",
        "ssn",
        "credit_card",
        "phone",
        "email"
    };
}