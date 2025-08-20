using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace ACS.Service.Domain.Validation;

/// <summary>
/// Base class for business rule validation attributes
/// </summary>
public abstract class BusinessRuleValidationAttribute : DomainValidationAttribute
{
    /// <summary>
    /// Rule identifier for tracking and configuration
    /// </summary>
    public string RuleId { get; set; } = string.Empty;
    
    /// <summary>
    /// Rule severity level
    /// </summary>
    public RuleSeverity Severity { get; set; } = RuleSeverity.Error;
    
    /// <summary>
    /// Whether this rule can be bypassed by administrators
    /// </summary>
    public bool AllowAdminBypass { get; set; } = false;
    
    /// <summary>
    /// Custom error code for the business rule
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;
    
    protected ValidationResult CreateRuleViolationResult(string message, string[]? memberNames = null)
    {
        var errorMessage = string.IsNullOrEmpty(ErrorCode) 
            ? message 
            : $"[{ErrorCode}] {message}";
            
        return new ValidationResult(errorMessage, memberNames);
    }
    
    protected bool CanBypassRule(IDomainValidationContext domainContext)
    {
        if (!AllowAdminBypass)
            return false;
            
        return domainContext.UserContext?.Roles.Contains("Administrator") ?? false;
    }
}

public enum RuleSeverity
{
    Warning,
    Error,
    Critical
}

/// <summary>
/// Validates that users cannot be assigned to more roles than the business limit
/// </summary>
public class MaxUserRolesBusinessRuleAttribute : BusinessRuleValidationAttribute
{
    public int MaxRoles { get; }

    public MaxUserRolesBusinessRuleAttribute(int maxRoles)
    {
        MaxRoles = maxRoles;
        RuleId = "BR001_MAX_USER_ROLES";
        ErrorCode = "MAX_ROLES_EXCEEDED";
    }

    public override ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        if (validationContext.ObjectInstance is not User user)
            return ValidationResult.Success;

        if (CanBypassRule(domainContext))
            return ValidationResult.Success;

        // Count current roles (this would need to be adapted based on your User model)
        var roleCount = 0; // user.Roles?.Count ?? 0; // Adjust based on actual User model
        
        if (roleCount > MaxRoles)
        {
            return CreateRuleViolationResult(
                $"User cannot be assigned more than {MaxRoles} roles. Current: {roleCount}",
                new[] { nameof(User) });
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates that groups cannot exceed maximum member limits based on business rules
/// </summary>
public class GroupMemberLimitsBusinessRuleAttribute : BusinessRuleValidationAttribute
{
    public int MaxUsers { get; set; } = 1000;
    public int MaxGroups { get; set; } = 100;
    public int MaxTotalMembers { get; set; } = 1500;

    public GroupMemberLimitsBusinessRuleAttribute()
    {
        RuleId = "BR002_GROUP_MEMBER_LIMITS";
        ErrorCode = "GROUP_CAPACITY_EXCEEDED";
    }

    public override ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        if (validationContext.ObjectInstance is not Group group)
            return ValidationResult.Success;

        if (CanBypassRule(domainContext))
            return ValidationResult.Success;

        // This would need to be implemented based on your actual Group model
        // For now, we'll use placeholder logic
        var userCount = 0; // Count of users in group
        var groupCount = 0; // Count of subgroups
        var totalCount = userCount + groupCount;

        if (userCount > MaxUsers)
            return CreateRuleViolationResult($"Group cannot contain more than {MaxUsers} users. Current: {userCount}");

        if (groupCount > MaxGroups)
            return CreateRuleViolationResult($"Group cannot contain more than {MaxGroups} subgroups. Current: {groupCount}");

        if (totalCount > MaxTotalMembers)
            return CreateRuleViolationResult($"Group cannot contain more than {MaxTotalMembers} total members. Current: {totalCount}");

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates that role permissions don't violate principle of least privilege
/// </summary>
public class LeastPrivilegeBusinessRuleAttribute : BusinessRuleValidationAttribute
{
    public string[]? ProhibitedCombinations { get; set; }
    public string[]? RequiresJustification { get; set; }

    public LeastPrivilegeBusinessRuleAttribute()
    {
        RuleId = "BR003_LEAST_PRIVILEGE";
        ErrorCode = "PRIVILEGE_VIOLATION";
        Severity = RuleSeverity.Warning;
    }

    public override ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        if (validationContext.ObjectInstance is not Role role)
            return ValidationResult.Success;

        if (CanBypassRule(domainContext))
            return ValidationResult.Success;

        // Check for prohibited permission combinations
        if (ProhibitedCombinations != null)
        {
            var rolePermissions = role.Permissions?.Select(p => p.Uri).ToList() ?? new List<string>();
            
            foreach (var prohibition in ProhibitedCombinations)
            {
                var prohibitedPerms = prohibition.Split(',');
                if (prohibitedPerms.All(p => rolePermissions.Contains(p.Trim())))
                {
                    return CreateRuleViolationResult(
                        $"Role contains prohibited permission combination: {prohibition}");
                }
            }
        }

        // Check for permissions that require justification
        if (RequiresJustification != null)
        {
            var sensitivePermissions = role.Permissions?
                .Where(p => RequiresJustification.Contains(p.Uri))
                .Select(p => p.Uri)
                .ToList() ?? new List<string>();

            if (sensitivePermissions.Any())
            {
                // In a real implementation, you might check for justification metadata
                var hasJustification = domainContext.OperationContext.OperationData.ContainsKey("Justification");
                if (!hasJustification && Severity == RuleSeverity.Error)
                {
                    return CreateRuleViolationResult(
                        $"Sensitive permissions require justification: {string.Join(", ", sensitivePermissions)}");
                }
            }
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates that temporal permissions have appropriate time windows
/// </summary>
public class TemporalPermissionBusinessRuleAttribute : BusinessRuleValidationAttribute
{
    public TimeSpan MaxDuration { get; set; } = TimeSpan.FromDays(365);
    public TimeSpan MinDuration { get; set; } = TimeSpan.FromMinutes(5);
    public bool RequiresFutureStartDate { get; set; } = false;

    public TemporalPermissionBusinessRuleAttribute()
    {
        RuleId = "BR004_TEMPORAL_PERMISSIONS";
        ErrorCode = "INVALID_TIME_WINDOW";
    }

    public override ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        if (validationContext.ObjectInstance is not TemporaryPermission tempPerm)
            return ValidationResult.Success;

        if (CanBypassRule(domainContext))
            return ValidationResult.Success;

        var now = DateTime.UtcNow;
        var duration = tempPerm.ExpiresAt - tempPerm.GrantedAt;

        // Check minimum duration
        if (duration < MinDuration)
            return CreateRuleViolationResult($"Temporary permission duration must be at least {MinDuration.TotalMinutes} minutes");

        // Check maximum duration
        if (duration > MaxDuration)
            return CreateRuleViolationResult($"Temporary permission duration cannot exceed {MaxDuration.TotalDays} days");

        // Check future start date requirement
        if (RequiresFutureStartDate && tempPerm.GrantedAt <= now)
            return CreateRuleViolationResult("Temporary permission must have a future start date");

        // Check for reasonable expiration
        if (tempPerm.ExpiresAt <= now)
            return CreateRuleViolationResult("Temporary permission cannot expire in the past");

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates compliance with segregation of duties requirements
/// </summary>
public class SegregationOfDutiesBusinessRuleAttribute : BusinessRuleValidationAttribute
{
    public string ConflictingRole { get; }
    public string ConflictScope { get; set; } = "Global"; // Global, Tenant, Department

    public SegregationOfDutiesBusinessRuleAttribute(string conflictingRole)
    {
        ConflictingRole = conflictingRole;
        RuleId = "BR005_SEGREGATION_DUTIES";
        ErrorCode = "DUTY_CONFLICT";
        Severity = RuleSeverity.Critical;
    }

    public override ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        if (validationContext.ObjectInstance is not User user)
            return ValidationResult.Success;

        if (CanBypassRule(domainContext))
            return ValidationResult.Success;

        // Check if user already has conflicting role
        // This would need to be implemented based on your actual User-Role relationship
        var hasConflictingRole = false; // Check user's roles for ConflictingRole
        
        if (hasConflictingRole)
        {
            return CreateRuleViolationResult(
                $"User cannot have both current role and '{ConflictingRole}' due to segregation of duties policy");
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates that resource access patterns follow business rules
/// </summary>
public class ResourceAccessPatternBusinessRuleAttribute : BusinessRuleValidationAttribute
{
    public string[]? RestrictedPatterns { get; set; }
    public string[]? RequiresApproval { get; set; }
    public TimeSpan? TimeWindow { get; set; }

    public ResourceAccessPatternBusinessRuleAttribute()
    {
        RuleId = "BR006_RESOURCE_ACCESS_PATTERN";
        ErrorCode = "INVALID_ACCESS_PATTERN";
    }

    public override ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        if (validationContext.ObjectInstance is not Permission permission)
            return ValidationResult.Success;

        if (CanBypassRule(domainContext))
            return ValidationResult.Success;

        // Check restricted patterns
        if (RestrictedPatterns != null)
        {
            foreach (var pattern in RestrictedPatterns)
            {
                if (permission.Uri.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return CreateRuleViolationResult($"Access to resource pattern '{pattern}' is restricted");
                }
            }
        }

        // Check for approval requirements
        if (RequiresApproval != null)
        {
            foreach (var pattern in RequiresApproval)
            {
                if (permission.Uri.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    var hasApproval = domainContext.OperationContext.OperationData.ContainsKey("ApprovalId");
                    if (!hasApproval)
                    {
                        return CreateRuleViolationResult($"Access to resource pattern '{pattern}' requires approval");
                    }
                }
            }
        }

        // Check time window restrictions
        if (TimeWindow.HasValue)
        {
            var now = DateTime.UtcNow.TimeOfDay;
            var windowStart = TimeOfDay.Parse("09:00:00");
            var windowEnd = TimeOfDay.Parse("17:00:00");
            
            if (now < windowStart || now > windowEnd)
            {
                return CreateRuleViolationResult($"Resource access only allowed during business hours ({windowStart}-{windowEnd})");
            }
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates that audit trail requirements are met
/// </summary>
public class AuditTrailBusinessRuleAttribute : BusinessRuleValidationAttribute
{
    public bool RequiresJustification { get; set; } = false;
    public string[]? AuditableActions { get; set; }

    public AuditTrailBusinessRuleAttribute()
    {
        RuleId = "BR007_AUDIT_TRAIL";
        ErrorCode = "AUDIT_REQUIREMENT_NOT_MET";
    }

    public override ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        var operationType = domainContext.OperationContext.OperationType;
        
        // Check if operation requires audit
        if (AuditableActions != null && AuditableActions.Contains(operationType))
        {
            if (RequiresJustification)
            {
                var hasJustification = domainContext.OperationContext.OperationData.ContainsKey("AuditJustification");
                if (!hasJustification)
                {
                    return CreateRuleViolationResult($"Operation '{operationType}' requires audit justification");
                }
            }

            // Ensure audit context is available
            if (domainContext.UserContext == null)
            {
                return CreateRuleViolationResult($"Auditable operation '{operationType}' requires user context");
            }
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates data retention and privacy compliance rules
/// </summary>
public class DataRetentionBusinessRuleAttribute : BusinessRuleValidationAttribute
{
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(2555); // 7 years default
    public bool RequiresConsentForStorage { get; set; } = false;
    public string[]? PersonalDataFields { get; set; }

    public DataRetentionBusinessRuleAttribute()
    {
        RuleId = "BR008_DATA_RETENTION";
        ErrorCode = "DATA_RETENTION_VIOLATION";
    }

    public override ValidationResult? ValidateInDomain(object? value, ValidationContext validationContext, IDomainValidationContext domainContext)
    {
        if (RequiresConsentForStorage && PersonalDataFields != null)
        {
            var hasPersonalData = PersonalDataFields.Any(field => 
                validationContext.ObjectType.GetProperty(field)?.GetValue(validationContext.ObjectInstance) != null);

            if (hasPersonalData)
            {
                var hasConsent = domainContext.OperationContext.OperationData.ContainsKey("DataProcessingConsent");
                if (!hasConsent)
                {
                    return CreateRuleViolationResult("Processing personal data requires explicit consent");
                }
            }
        }

        return ValidationResult.Success;
    }
}