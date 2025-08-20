using System.ComponentModel.DataAnnotations;

namespace ACS.WebApi.Models.Requests;

/// <summary>
/// Base class for bulk operation requests
/// </summary>
public abstract class BulkOperationRequestBase
{
    /// <summary>
    /// Whether to continue processing when individual operations fail
    /// </summary>
    public bool ContinueOnError { get; set; } = true;

    /// <summary>
    /// Maximum number of concurrent operations
    /// </summary>
    [Range(1, 100, ErrorMessage = "Concurrency level must be between 1 and 100")]
    public int ConcurrencyLevel { get; set; } = 10;

    /// <summary>
    /// Reason for the bulk operation (for audit trail)
    /// </summary>
    [StringLength(1000, ErrorMessage = "Reason cannot exceed 1000 characters")]
    public string? Reason { get; set; }

    /// <summary>
    /// Whether to validate all items before processing
    /// </summary>
    public bool ValidateFirst { get; set; } = true;

    /// <summary>
    /// Batch size for processing (0 = process all at once)
    /// </summary>
    [Range(0, 10000, ErrorMessage = "Batch size must be between 0 and 10,000")]
    public int BatchSize { get; set; } = 0;
}

#region User Bulk Operations

/// <summary>
/// Request model for bulk user creation
/// </summary>
public class BulkCreateUsersRequest : BulkOperationRequestBase
{
    /// <summary>
    /// Users to create
    /// </summary>
    [Required(ErrorMessage = "Users list is required")]
    [MinLength(1, ErrorMessage = "At least one user is required")]
    [MaxLength(10000, ErrorMessage = "Cannot create more than 10,000 users at once")]
    public List<CreateUserRequest> Users { get; set; } = new();

    /// <summary>
    /// Default roles to assign to all new users
    /// </summary>
    public List<int> DefaultRoleIds { get; set; } = new();

    /// <summary>
    /// Default groups to add all new users to
    /// </summary>
    public List<int> DefaultGroupIds { get; set; } = new();

    /// <summary>
    /// Whether to send welcome emails to new users
    /// </summary>
    public bool SendWelcomeEmails { get; set; } = false;

    /// <summary>
    /// Template to use for welcome emails
    /// </summary>
    public string? WelcomeEmailTemplate { get; set; }
}

/// <summary>
/// Request model for creating a single user in bulk operations
/// </summary>
public class CreateUserRequest
{
    /// <summary>
    /// Username for the new user
    /// </summary>
    [Required(ErrorMessage = "Username is required")]
    [StringLength(100, ErrorMessage = "Username cannot exceed 100 characters")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Email address for the user
    /// </summary>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// First name of the user
    /// </summary>
    [StringLength(100, ErrorMessage = "First name cannot exceed 100 characters")]
    public string? FirstName { get; set; }

    /// <summary>
    /// Last name of the user
    /// </summary>
    [StringLength(100, ErrorMessage = "Last name cannot exceed 100 characters")]
    public string? LastName { get; set; }

    /// <summary>
    /// Whether the user is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Initial password for the user (optional)
    /// </summary>
    [StringLength(256, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 256 characters")]
    public string? InitialPassword { get; set; }

    /// <summary>
    /// Whether the user must change password on first login
    /// </summary>
    public bool MustChangePassword { get; set; } = true;

    /// <summary>
    /// Role IDs to assign to this user
    /// </summary>
    public List<int> RoleIds { get; set; } = new();

    /// <summary>
    /// Group IDs to add this user to
    /// </summary>
    public List<int> GroupIds { get; set; } = new();

    /// <summary>
    /// Additional user metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Department or organizational unit
    /// </summary>
    [StringLength(200, ErrorMessage = "Department cannot exceed 200 characters")]
    public string? Department { get; set; }

    /// <summary>
    /// Job title
    /// </summary>
    [StringLength(200, ErrorMessage = "Job title cannot exceed 200 characters")]
    public string? JobTitle { get; set; }

    /// <summary>
    /// Phone number
    /// </summary>
    [Phone(ErrorMessage = "Invalid phone number format")]
    public string? PhoneNumber { get; set; }
}

/// <summary>
/// Request model for bulk user updates
/// </summary>
public class BulkUpdateUsersRequest : BulkOperationRequestBase
{
    /// <summary>
    /// User updates to apply
    /// </summary>
    [Required(ErrorMessage = "Updates list is required")]
    [MinLength(1, ErrorMessage = "At least one update is required")]
    [MaxLength(10000, ErrorMessage = "Cannot update more than 10,000 users at once")]
    public List<UserUpdateRequest> Updates { get; set; } = new();
}

/// <summary>
/// Request model for updating a single user in bulk operations
/// </summary>
public class UserUpdateRequest
{
    /// <summary>
    /// ID of the user to update
    /// </summary>
    [Required(ErrorMessage = "User ID is required")]
    public int UserId { get; set; }

    /// <summary>
    /// Email address (if changing)
    /// </summary>
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
    public string? Email { get; set; }

    /// <summary>
    /// First name (if changing)
    /// </summary>
    [StringLength(100, ErrorMessage = "First name cannot exceed 100 characters")]
    public string? FirstName { get; set; }

    /// <summary>
    /// Last name (if changing)
    /// </summary>
    [StringLength(100, ErrorMessage = "Last name cannot exceed 100 characters")]
    public string? LastName { get; set; }

    /// <summary>
    /// Active status (if changing)
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>
    /// Department (if changing)
    /// </summary>
    [StringLength(200, ErrorMessage = "Department cannot exceed 200 characters")]
    public string? Department { get; set; }

    /// <summary>
    /// Job title (if changing)
    /// </summary>
    [StringLength(200, ErrorMessage = "Job title cannot exceed 200 characters")]
    public string? JobTitle { get; set; }

    /// <summary>
    /// Phone number (if changing)
    /// </summary>
    [Phone(ErrorMessage = "Invalid phone number format")]
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Metadata updates
    /// </summary>
    public Dictionary<string, object> MetadataUpdates { get; set; } = new();
}

/// <summary>
/// Request model for bulk role assignment to users
/// </summary>
public class BulkAssignRolesRequest : BulkOperationRequestBase
{
    /// <summary>
    /// User IDs to assign roles to
    /// </summary>
    [Required(ErrorMessage = "User IDs are required")]
    [MinLength(1, ErrorMessage = "At least one user ID is required")]
    [MaxLength(10000, ErrorMessage = "Cannot assign roles to more than 10,000 users at once")]
    public List<int> UserIds { get; set; } = new();

    /// <summary>
    /// Role IDs to assign
    /// </summary>
    [Required(ErrorMessage = "Role IDs are required")]
    [MinLength(1, ErrorMessage = "At least one role ID is required")]
    [MaxLength(100, ErrorMessage = "Cannot assign more than 100 roles at once")]
    public List<int> RoleIds { get; set; } = new();

    /// <summary>
    /// Expiration date for the role assignments
    /// </summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Whether to replace existing roles or add to them
    /// </summary>
    public bool ReplaceExistingRoles { get; set; } = false;

    /// <summary>
    /// Justification for the role assignments
    /// </summary>
    [StringLength(1000, ErrorMessage = "Justification cannot exceed 1000 characters")]
    public string? Justification { get; set; }
}

/// <summary>
/// Request model for bulk role removal from users
/// </summary>
public class BulkRemoveRolesRequest : BulkOperationRequestBase
{
    /// <summary>
    /// User IDs to remove roles from
    /// </summary>
    [Required(ErrorMessage = "User IDs are required")]
    [MinLength(1, ErrorMessage = "At least one user ID is required")]
    [MaxLength(10000, ErrorMessage = "Cannot remove roles from more than 10,000 users at once")]
    public List<int> UserIds { get; set; } = new();

    /// <summary>
    /// Role IDs to remove
    /// </summary>
    [Required(ErrorMessage = "Role IDs are required")]
    [MinLength(1, ErrorMessage = "At least one role ID is required")]
    public List<int> RoleIds { get; set; } = new();

    /// <summary>
    /// Justification for the role removals
    /// </summary>
    [StringLength(1000, ErrorMessage = "Justification cannot exceed 1000 characters")]
    public string? Justification { get; set; }
}

/// <summary>
/// Request model for bulk user status changes
/// </summary>
public class BulkChangeUserStatusRequest : BulkOperationRequestBase
{
    /// <summary>
    /// User IDs to change status for
    /// </summary>
    [Required(ErrorMessage = "User IDs are required")]
    [MinLength(1, ErrorMessage = "At least one user ID is required")]
    [MaxLength(10000, ErrorMessage = "Cannot change status for more than 10,000 users at once")]
    public List<int> UserIds { get; set; } = new();

    /// <summary>
    /// New active status
    /// </summary>
    [Required(ErrorMessage = "Active status is required")]
    public bool IsActive { get; set; }

    /// <summary>
    /// Reason for the status change
    /// </summary>
    [Required(ErrorMessage = "Reason is required")]
    [StringLength(1000, ErrorMessage = "Reason cannot exceed 1000 characters")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Effective date for the status change
    /// </summary>
    public DateTime? EffectiveDate { get; set; }

    /// <summary>
    /// Whether to notify users of the status change
    /// </summary>
    public bool NotifyUsers { get; set; } = false;
}

#endregion

#region Role Bulk Operations

/// <summary>
/// Request model for bulk role creation
/// </summary>
public class BulkCreateRolesRequest : BulkOperationRequestBase
{
    /// <summary>
    /// Roles to create
    /// </summary>
    [Required(ErrorMessage = "Roles list is required")]
    [MinLength(1, ErrorMessage = "At least one role is required")]
    [MaxLength(1000, ErrorMessage = "Cannot create more than 1,000 roles at once")]
    public List<CreateRoleRequest> Roles { get; set; } = new();
}

/// <summary>
/// Request model for creating a single role in bulk operations
/// </summary>
public class CreateRoleRequest
{
    /// <summary>
    /// Name of the role
    /// </summary>
    [Required(ErrorMessage = "Role name is required")]
    [StringLength(255, ErrorMessage = "Role name cannot exceed 255 characters")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the role
    /// </summary>
    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string? Description { get; set; }

    /// <summary>
    /// Whether the role is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Permission IDs to assign to this role
    /// </summary>
    public List<int> PermissionIds { get; set; } = new();

    /// <summary>
    /// Parent role IDs (for role hierarchy)
    /// </summary>
    public List<int> ParentRoleIds { get; set; } = new();

    /// <summary>
    /// Role metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Role category or type
    /// </summary>
    [StringLength(100, ErrorMessage = "Category cannot exceed 100 characters")]
    public string? Category { get; set; }

    /// <summary>
    /// Priority level for the role (higher number = higher priority)
    /// </summary>
    [Range(0, 100, ErrorMessage = "Priority must be between 0 and 100")]
    public int Priority { get; set; } = 0;
}

/// <summary>
/// Request model for bulk permission assignment to roles
/// </summary>
public class BulkAssignPermissionsRequest : BulkOperationRequestBase
{
    /// <summary>
    /// Role IDs to assign permissions to
    /// </summary>
    [Required(ErrorMessage = "Role IDs are required")]
    [MinLength(1, ErrorMessage = "At least one role ID is required")]
    [MaxLength(1000, ErrorMessage = "Cannot assign permissions to more than 1,000 roles at once")]
    public List<int> RoleIds { get; set; } = new();

    /// <summary>
    /// Permissions to assign
    /// </summary>
    [Required(ErrorMessage = "Permissions are required")]
    [MinLength(1, ErrorMessage = "At least one permission is required")]
    public List<CreatePermissionRequest> Permissions { get; set; } = new();

    /// <summary>
    /// Whether to replace existing permissions or add to them
    /// </summary>
    public bool ReplaceExistingPermissions { get; set; } = false;

    /// <summary>
    /// Justification for the permission assignments
    /// </summary>
    [StringLength(1000, ErrorMessage = "Justification cannot exceed 1000 characters")]
    public string? Justification { get; set; }
}

#endregion

#region Permission Bulk Operations

/// <summary>
/// Request model for bulk permission creation
/// </summary>
public class BulkCreatePermissionsRequest : BulkOperationRequestBase
{
    /// <summary>
    /// Permissions to create
    /// </summary>
    [Required(ErrorMessage = "Permissions list is required")]
    [MinLength(1, ErrorMessage = "At least one permission is required")]
    [MaxLength(10000, ErrorMessage = "Cannot create more than 10,000 permissions at once")]
    public List<CreatePermissionRequest> Permissions { get; set; } = new();
}

/// <summary>
/// Request model for creating a single permission in bulk operations
/// </summary>
public class CreatePermissionRequest
{
    /// <summary>
    /// Entity ID that the permission applies to
    /// </summary>
    [Required(ErrorMessage = "Entity ID is required")]
    public int EntityId { get; set; }

    /// <summary>
    /// Resource ID that the permission controls access to
    /// </summary>
    [Required(ErrorMessage = "Resource ID is required")]
    public int ResourceId { get; set; }

    /// <summary>
    /// HTTP verb for the permission
    /// </summary>
    [Required(ErrorMessage = "HTTP verb is required")]
    [RegularExpression("^(GET|POST|PUT|DELETE|PATCH|HEAD|OPTIONS|\\*)$", ErrorMessage = "Invalid HTTP verb")]
    public string HttpVerb { get; set; } = string.Empty;

    /// <summary>
    /// Whether to grant access
    /// </summary>
    public bool Grant { get; set; } = true;

    /// <summary>
    /// Whether to deny access (for explicit denials)
    /// </summary>
    public bool Deny { get; set; } = false;

    /// <summary>
    /// Permission scheme type
    /// </summary>
    [Required(ErrorMessage = "Scheme is required")]
    [StringLength(50, ErrorMessage = "Scheme cannot exceed 50 characters")]
    public string Scheme { get; set; } = "Allow";

    /// <summary>
    /// Expiration date for the permission
    /// </summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Conditions for the permission
    /// </summary>
    public string? Conditions { get; set; }

    /// <summary>
    /// Permission metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Request model for bulk permission expiration updates
/// </summary>
public class BulkUpdatePermissionExpirationRequest : BulkOperationRequestBase
{
    /// <summary>
    /// Permission IDs to update
    /// </summary>
    [Required(ErrorMessage = "Permission IDs are required")]
    [MinLength(1, ErrorMessage = "At least one permission ID is required")]
    [MaxLength(10000, ErrorMessage = "Cannot update more than 10,000 permissions at once")]
    public List<int> PermissionIds { get; set; } = new();

    /// <summary>
    /// New expiration date (null to remove expiration)
    /// </summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Justification for the expiration updates
    /// </summary>
    [StringLength(1000, ErrorMessage = "Justification cannot exceed 1000 characters")]
    public string? Justification { get; set; }
}

#endregion

#region Group Bulk Operations

/// <summary>
/// Request model for bulk group membership operations
/// </summary>
public class BulkGroupMembershipRequest : BulkOperationRequestBase
{
    /// <summary>
    /// User IDs to add to groups
    /// </summary>
    [Required(ErrorMessage = "User IDs are required")]
    [MinLength(1, ErrorMessage = "At least one user ID is required")]
    [MaxLength(10000, ErrorMessage = "Cannot add more than 10,000 users at once")]
    public List<int> UserIds { get; set; } = new();

    /// <summary>
    /// Group IDs to add users to
    /// </summary>
    [Required(ErrorMessage = "Group IDs are required")]
    [MinLength(1, ErrorMessage = "At least one group ID is required")]
    [MaxLength(100, ErrorMessage = "Cannot add to more than 100 groups at once")]
    public List<int> GroupIds { get; set; } = new();

    /// <summary>
    /// Justification for the group memberships
    /// </summary>
    [StringLength(1000, ErrorMessage = "Justification cannot exceed 1000 characters")]
    public string? Justification { get; set; }
}

#endregion

#region Import/Export Operations

/// <summary>
/// Request model for bulk entity export
/// </summary>
public class BulkExportRequest
{
    /// <summary>
    /// Type of entities to export
    /// </summary>
    [Required(ErrorMessage = "Entity type is required")]
    [RegularExpression("^(User|Role|Permission|Group|Resource)$", ErrorMessage = "Invalid entity type")]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Export format
    /// </summary>
    [RegularExpression("^(CSV|JSON|XML)$", ErrorMessage = "Export format must be CSV, JSON, or XML")]
    public string ExportFormat { get; set; } = "CSV";

    /// <summary>
    /// Whether to include inactive entities
    /// </summary>
    public bool IncludeInactive { get; set; } = false;

    /// <summary>
    /// Additional filters to apply
    /// </summary>
    public Dictionary<string, object> Filters { get; set; } = new();

    /// <summary>
    /// Fields to include in export (empty = all fields)
    /// </summary>
    public List<string> IncludeFields { get; set; } = new();

    /// <summary>
    /// Fields to exclude from export
    /// </summary>
    public List<string> ExcludeFields { get; set; } = new();

    /// <summary>
    /// Maximum number of records to export
    /// </summary>
    [Range(1, 1000000, ErrorMessage = "Max records must be between 1 and 1,000,000")]
    public int MaxRecords { get; set; } = 100000;

    /// <summary>
    /// Reason for the export
    /// </summary>
    [Required(ErrorMessage = "Export reason is required")]
    [StringLength(1000, ErrorMessage = "Export reason cannot exceed 1000 characters")]
    public string ExportReason { get; set; } = string.Empty;
}

/// <summary>
/// Request model for template generation
/// </summary>
public class GenerateTemplateRequest
{
    /// <summary>
    /// Entity type to generate template for
    /// </summary>
    [Required(ErrorMessage = "Entity type is required")]
    [RegularExpression("^(User|Role|Permission|Group|Resource)$", ErrorMessage = "Invalid entity type")]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Template format
    /// </summary>
    [RegularExpression("^(CSV|Excel|JSON)$", ErrorMessage = "Template format must be CSV, Excel, or JSON")]
    public string TemplateFormat { get; set; } = "CSV";

    /// <summary>
    /// Whether to include sample data
    /// </summary>
    public bool IncludeSampleData { get; set; } = true;

    /// <summary>
    /// Number of sample rows to include
    /// </summary>
    [Range(0, 10, ErrorMessage = "Sample rows must be between 0 and 10")]
    public int SampleRows { get; set; } = 3;

    /// <summary>
    /// Whether to include validation rules in the template
    /// </summary>
    public bool IncludeValidationRules { get; set; } = true;

    /// <summary>
    /// Language for template headers and descriptions
    /// </summary>
    [StringLength(10, ErrorMessage = "Language code cannot exceed 10 characters")]
    public string Language { get; set; } = "en";
}

/// <summary>
/// Request model for bulk operation validation
/// </summary>
public class BulkValidationRequest
{
    /// <summary>
    /// Entity type being validated
    /// </summary>
    [Required(ErrorMessage = "Entity type is required")]
    [RegularExpression("^(User|Role|Permission|Group|Resource)$", ErrorMessage = "Invalid entity type")]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Data to validate (as JSON array)
    /// </summary>
    [Required(ErrorMessage = "Data is required")]
    public string Data { get; set; } = string.Empty;

    /// <summary>
    /// Validation rules to apply
    /// </summary>
    public List<string> ValidationRules { get; set; } = new();

    /// <summary>
    /// Whether to perform deep validation (including database checks)
    /// </summary>
    public bool DeepValidation { get; set; } = true;

    /// <summary>
    /// Maximum number of validation errors to return
    /// </summary>
    [Range(1, 10000, ErrorMessage = "Max errors must be between 1 and 10,000")]
    public int MaxErrors { get; set; } = 1000;
}

#endregion