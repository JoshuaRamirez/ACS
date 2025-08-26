namespace ACS.WebApi.Models.Responses;

/// <summary>
/// Base response model for bulk operations
/// </summary>
public class BulkOperationResponse
{
    /// <summary>
    /// Total number of items requested for processing
    /// </summary>
    public int TotalRequested { get; set; }

    /// <summary>
    /// Total number of items actually processed
    /// </summary>
    public int TotalProcessed { get; set; }

    /// <summary>
    /// Number of successful operations
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of failed operations
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// Individual operation results
    /// </summary>
    public List<BulkOperationResult> Results { get; set; } = new();

    /// <summary>
    /// Overall operation status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Time taken to complete the operation
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Any warnings generated during the operation
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Success percentage
    /// </summary>
    public double SuccessPercentage => TotalProcessed > 0 ? (double)SuccessCount / TotalProcessed * 100 : 0;

    /// <summary>
    /// Failure percentage
    /// </summary>
    public double FailurePercentage => TotalProcessed > 0 ? (double)FailureCount / TotalProcessed * 100 : 0;

    /// <summary>
    /// Operation summary
    /// </summary>
    public string Summary => $"Processed {TotalProcessed} items: {SuccessCount} succeeded, {FailureCount} failed";
}

/// <summary>
/// Generic bulk operation response with typed results
/// </summary>
/// <typeparam name="T">Type of the result data</typeparam>
public class BulkOperationResponse<T> : BulkOperationResponse
{
    /// <summary>
    /// Typed individual operation results
    /// </summary>
    public new List<BulkOperationResult<T>> Results { get; set; } = new();

    /// <summary>
    /// Validation results for items that failed validation
    /// </summary>
    public List<BulkValidationResult<T>> ValidationResults { get; set; } = new();

    /// <summary>
    /// Successfully created/updated items
    /// </summary>
    public List<T> SuccessfulItems => Results.Where(r => r.Success && r.Data != null).Select(r => r.Data!).ToList();

    /// <summary>
    /// Items that failed processing
    /// </summary>
    public List<BulkOperationResult<T>> FailedItems => Results.Where(r => !r.Success).ToList();
}

/// <summary>
/// Result of a single operation in a bulk request
/// </summary>
public class BulkOperationResult
{
    /// <summary>
    /// ID of the item that was processed
    /// </summary>
    public int ItemId { get; set; }

    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if the operation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Additional details about the operation
    /// </summary>
    public Dictionary<string, object> Details { get; set; } = new();

    /// <summary>
    /// Processing time for this individual operation
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }

    /// <summary>
    /// Warnings specific to this operation
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Generic result of a single operation in a bulk request
/// </summary>
/// <typeparam name="T">Type of the result data</typeparam>
public class BulkOperationResult<T> : BulkOperationResult
{
    /// <summary>
    /// The resulting data from the operation (if successful)
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Validation errors specific to this item
    /// </summary>
    public List<string> ValidationErrors { get; set; } = new();

    /// <summary>
    /// Original item that was being processed (for debugging)
    /// </summary>
    public object? OriginalItem { get; set; }
}

/// <summary>
/// Validation result for bulk operations
/// </summary>
/// <typeparam name="T">Type of the item being validated</typeparam>
public class BulkValidationResult<T>
{
    /// <summary>
    /// Index of the item in the original request
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// The item that was validated
    /// </summary>
    public T Item { get; set; } = default!;

    /// <summary>
    /// Whether the item passed validation
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Validation errors for this item
    /// </summary>
    public List<string> ValidationErrors { get; set; } = new();

    /// <summary>
    /// Validation warnings for this item
    /// </summary>
    public List<string> ValidationWarnings { get; set; } = new();

    /// <summary>
    /// Additional validation context
    /// </summary>
    public Dictionary<string, object> ValidationContext { get; set; } = new();
}

/// <summary>
/// Response model for bulk import operations
/// </summary>
public class BulkImportResponse
{
    /// <summary>
    /// Name of the imported file
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Type of entities imported
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Total number of records in the import file
    /// </summary>
    public int TotalRecords { get; set; }

    /// <summary>
    /// Number of records successfully imported
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of records that failed to import
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// Validation errors encountered during import
    /// </summary>
    public List<ImportValidationError> ValidationErrors { get; set; } = new();

    /// <summary>
    /// Processing errors encountered during import
    /// </summary>
    public List<ImportProcessingError> ProcessingErrors { get; set; } = new();

    /// <summary>
    /// IDs of successfully imported entities
    /// </summary>
    public List<int> ImportedIds { get; set; } = new();

    /// <summary>
    /// Overall import status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Time taken to complete the import
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Import summary information
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Import statistics
    /// </summary>
    public ImportStatistics Statistics { get; set; } = new();

    /// <summary>
    /// Recommendations for improving future imports
    /// </summary>
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Validation error during import
/// </summary>
public class ImportValidationError
{
    /// <summary>
    /// Row number in the import file
    /// </summary>
    public int RowNumber { get; set; }

    /// <summary>
    /// Column name where the error occurred
    /// </summary>
    public string? ColumnName { get; set; }

    /// <summary>
    /// Error message
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Original value that caused the error
    /// </summary>
    public string? OriginalValue { get; set; }

    /// <summary>
    /// Suggested correction (if available)
    /// </summary>
    public string? SuggestedCorrection { get; set; }

    /// <summary>
    /// Severity of the error
    /// </summary>
    public string Severity { get; set; } = "Error";
}

/// <summary>
/// Processing error during import
/// </summary>
public class ImportProcessingError
{
    /// <summary>
    /// Row number in the import file
    /// </summary>
    public int RowNumber { get; set; }

    /// <summary>
    /// Error message
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Exception details (if available)
    /// </summary>
    public string? ExceptionDetails { get; set; }

    /// <summary>
    /// Processing stage where error occurred
    /// </summary>
    public string ProcessingStage { get; set; } = string.Empty;

    /// <summary>
    /// Whether this error is recoverable
    /// </summary>
    public bool IsRecoverable { get; set; }

    /// <summary>
    /// Recommended action to resolve the error
    /// </summary>
    public string? RecommendedAction { get; set; }
}

/// <summary>
/// Import operation statistics
/// </summary>
public class ImportStatistics
{
    /// <summary>
    /// Number of rows processed per second
    /// </summary>
    public double RowsPerSecond { get; set; }

    /// <summary>
    /// File size processed
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Memory usage during import
    /// </summary>
    public long MemoryUsageBytes { get; set; }

    /// <summary>
    /// Database operations performed
    /// </summary>
    public int DatabaseOperations { get; set; }

    /// <summary>
    /// Validation operations performed
    /// </summary>
    public int ValidationOperations { get; set; }

    /// <summary>
    /// Most common error types
    /// </summary>
    public Dictionary<string, int> CommonErrorTypes { get; set; } = new();

    /// <summary>
    /// Data quality metrics
    /// </summary>
    public DataQualityMetrics DataQuality { get; set; } = new();
}

/// <summary>
/// Data quality metrics from import
/// </summary>
public class DataQualityMetrics
{
    /// <summary>
    /// Percentage of complete records (no missing required fields)
    /// </summary>
    public double CompletenessPercentage { get; set; }

    /// <summary>
    /// Percentage of valid format records
    /// </summary>
    public double ValidityPercentage { get; set; }

    /// <summary>
    /// Percentage of unique records (no duplicates)
    /// </summary>
    public double UniquenessPercentage { get; set; }

    /// <summary>
    /// Percentage of consistent records
    /// </summary>
    public double ConsistencyPercentage { get; set; }

    /// <summary>
    /// Overall data quality score
    /// </summary>
    public double OverallScore { get; set; }
}

/// <summary>
/// Result of a bulk import operation
/// </summary>
public class BulkImportResult
{
    /// <summary>
    /// Import operation status
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Number of records processed
    /// </summary>
    public int ProcessedCount { get; set; }
    
    /// <summary>
    /// Number of records successfully imported
    /// </summary>
    public int SuccessCount { get; set; }
    
    /// <summary>
    /// Number of records that failed to import
    /// </summary>
    public int FailureCount { get; set; }
    
    /// <summary>
    /// Import errors encountered
    /// </summary>
    public List<string> Errors { get; set; } = new();
    
    /// <summary>
    /// Import warnings
    /// </summary>
    public List<string> Warnings { get; set; } = new();
    
    /// <summary>
    /// Duration of the import operation
    /// </summary>
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Response model for bulk operation status
/// </summary>
public class BulkOperationStatusResponse
{
    /// <summary>
    /// Operation ID
    /// </summary>
    public string OperationId { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the operation
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public double Progress { get; set; }

    /// <summary>
    /// When the operation started
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// When the operation completed (if finished)
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Duration of the operation
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Total number of items to process
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Number of items processed so far
    /// </summary>
    public int ProcessedItems { get; set; }

    /// <summary>
    /// Number of successful operations
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of failed operations
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// Current operation being performed
    /// </summary>
    public string? CurrentOperation { get; set; }

    /// <summary>
    /// Error messages from the operation
    /// </summary>
    public List<string> ErrorMessages { get; set; } = new();

    /// <summary>
    /// Warnings from the operation
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Estimated time remaining (if operation is in progress)
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; set; }

    /// <summary>
    /// Performance metrics
    /// </summary>
    public OperationPerformanceMetrics Performance { get; set; } = new();
}

/// <summary>
/// Performance metrics for bulk operations
/// </summary>
public class OperationPerformanceMetrics
{
    /// <summary>
    /// Items processed per second
    /// </summary>
    public double ItemsPerSecond { get; set; }

    /// <summary>
    /// Average processing time per item
    /// </summary>
    public TimeSpan AverageItemProcessingTime { get; set; }

    /// <summary>
    /// Memory usage during operation
    /// </summary>
    public long MemoryUsageBytes { get; set; }

    /// <summary>
    /// CPU usage percentage
    /// </summary>
    public double CpuUsagePercentage { get; set; }

    /// <summary>
    /// Database connection usage
    /// </summary>
    public int DatabaseConnections { get; set; }

    /// <summary>
    /// Number of database queries executed
    /// </summary>
    public int DatabaseQueries { get; set; }
}

/// <summary>
/// Summary response for bulk operations
/// </summary>
public class BulkOperationSummaryResponse
{
    /// <summary>
    /// Operation ID
    /// </summary>
    public string OperationId { get; set; } = string.Empty;

    /// <summary>
    /// Type of operation performed
    /// </summary>
    public string OperationType { get; set; } = string.Empty;

    /// <summary>
    /// Operation status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// When the operation started
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// When the operation completed
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Duration of the operation
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Total number of items processed
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Number of successful operations
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of failed operations
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// Who initiated the operation
    /// </summary>
    public string? InitiatedBy { get; set; }

    /// <summary>
    /// Success rate percentage
    /// </summary>
    public double SuccessRate => TotalItems > 0 ? (double)SuccessCount / TotalItems * 100 : 0;
}

/// <summary>
/// Response model for bulk validation operations
/// </summary>
public class BulkValidationResponse
{
    /// <summary>
    /// Entity type that was validated
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Total number of items validated
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Number of valid items
    /// </summary>
    public int ValidItems { get; set; }

    /// <summary>
    /// Number of invalid items
    /// </summary>
    public int InvalidItems { get; set; }

    /// <summary>
    /// Validation results for each item
    /// </summary>
    public List<ItemValidationResult> ValidationResults { get; set; } = new();

    /// <summary>
    /// Overall validation status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Time taken to complete validation
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Validation summary
    /// </summary>
    public ValidationSummary Summary { get; set; } = new();

    /// <summary>
    /// Common validation issues found
    /// </summary>
    public Dictionary<string, int> CommonIssues { get; set; } = new();

    /// <summary>
    /// Recommendations for fixing validation issues
    /// </summary>
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Validation result for a single item
/// </summary>
public class ItemValidationResult
{
    /// <summary>
    /// Index of the item in the original request
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Whether the item is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Validation errors
    /// </summary>
    public List<ValidationError> Errors { get; set; } = new();

    /// <summary>
    /// Validation warnings
    /// </summary>
    public List<ValidationWarning> Warnings { get; set; } = new();

    /// <summary>
    /// Suggestions for improvement
    /// </summary>
    public List<string> Suggestions { get; set; } = new();
}

/// <summary>
/// Validation error details
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Field name where the error occurred
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Error code for programmatic handling
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>
    /// Current value that caused the error
    /// </summary>
    public object? CurrentValue { get; set; }

    /// <summary>
    /// Expected format or value
    /// </summary>
    public string? ExpectedFormat { get; set; }

    /// <summary>
    /// Severity level
    /// </summary>
    public string Severity { get; set; } = "Error";
}

/// <summary>
/// Validation warning details
/// </summary>
public class ValidationWarning
{
    /// <summary>
    /// Field name where the warning occurred
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Warning message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Warning code for programmatic handling
    /// </summary>
    public string WarningCode { get; set; } = string.Empty;

    /// <summary>
    /// Current value that triggered the warning
    /// </summary>
    public object? CurrentValue { get; set; }

    /// <summary>
    /// Recommended action
    /// </summary>
    public string? RecommendedAction { get; set; }
}

/// <summary>
/// Validation summary information
/// </summary>
public class ValidationSummary
{
    /// <summary>
    /// Overall validation score (0-100)
    /// </summary>
    public double ValidationScore { get; set; }

    /// <summary>
    /// Data quality assessment
    /// </summary>
    public string DataQuality { get; set; } = string.Empty;

    /// <summary>
    /// Most common validation errors
    /// </summary>
    public Dictionary<string, int> TopErrors { get; set; } = new();

    /// <summary>
    /// Most problematic fields
    /// </summary>
    public Dictionary<string, int> ProblematicFields { get; set; } = new();

    /// <summary>
    /// Estimated effort to fix all issues
    /// </summary>
    public string EstimatedFixEffort { get; set; } = string.Empty;

    /// <summary>
    /// Priority recommendations
    /// </summary>
    public List<string> PriorityRecommendations { get; set; } = new();
}

/// <summary>
/// Standard response models for different entity types
/// </summary>

public class UserResponse
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int RoleCount { get; set; }
    public int GroupCount { get; set; }
    public string? Department { get; set; }
    public string? JobTitle { get; set; }
}

public class RoleResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int PermissionCount { get; set; }
    public int UserCount { get; set; }
    public string? Category { get; set; }
    public int Priority { get; set; }
}

public class PermissionResponse
{
    public int Id { get; set; }
    public int EntityId { get; set; }
    public int ResourceId { get; set; }
    public string HttpVerb { get; set; } = string.Empty;
    public bool Grant { get; set; }
    public bool Deny { get; set; }
    public string Scheme { get; set; } = string.Empty;
    public DateTime? ExpirationDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsEffective { get; set; }
    public string? Conditions { get; set; }
}

/// <summary>
/// Response model for template generation
/// </summary>
public class TemplateResponse
{
    /// <summary>
    /// Entity type the template is for
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Template format
    /// </summary>
    public string TemplateFormat { get; set; } = string.Empty;

    /// <summary>
    /// Template file name
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Template content (if small enough to include)
    /// </summary>
    public string? TemplateContent { get; set; }

    /// <summary>
    /// Download URL for the template
    /// </summary>
    public string? DownloadUrl { get; set; }

    /// <summary>
    /// Instructions for using the template
    /// </summary>
    public List<string> Instructions { get; set; } = new();

    /// <summary>
    /// Field definitions and validation rules
    /// </summary>
    public List<TemplateFieldDefinition> FieldDefinitions { get; set; } = new();

    /// <summary>
    /// Examples and best practices
    /// </summary>
    public List<string> BestPractices { get; set; } = new();
}

/// <summary>
/// Template field definition
/// </summary>
public class TemplateFieldDefinition
{
    /// <summary>
    /// Field name
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Field description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Data type expected
    /// </summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>
    /// Whether the field is required
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Maximum length (for string fields)
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Validation pattern (regex)
    /// </summary>
    public string? ValidationPattern { get; set; }

    /// <summary>
    /// Allowed values (for enumeration fields)
    /// </summary>
    public List<string> AllowedValues { get; set; } = new();

    /// <summary>
    /// Example values
    /// </summary>
    public List<string> ExampleValues { get; set; } = new();

    /// <summary>
    /// Default value
    /// </summary>
    public string? DefaultValue { get; set; }
}