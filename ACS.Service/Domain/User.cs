using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using ACS.Service.Delegates.Normalizers;
using ACS.Service.Domain.Validation;

namespace ACS.Service.Domain;

[MaxUserRolesBusinessRule(5)]
[SegregationOfDutiesBusinessRule("Administrator")]
[DataRetentionBusinessRule(RequiresConsentForStorage = true, PersonalDataFields = new[] { "Name", "Email" })]
public class User : Entity
{
    // Business rule constants
    private const int MAX_ROLES = 5;
    private const int MAX_GROUPS = 20;
    
    public ReadOnlyCollection<Group> GroupMemberships => Parents.OfType<Group>().ToList().AsReadOnly();
    public ReadOnlyCollection<Role> RoleMemberships => Parents.OfType<Role>().ToList().AsReadOnly();

    /// <summary>
    /// Assigns this user to a role with full business rule validation
    /// DOMAIN LOGIC: Enforces business rules and validates preconditions
    /// DELEGATION: Uses normalizer for pure state transformation
    /// </summary>
    public void AssignToRole(Role role, string assignedBy)
    {
        // BUSINESS RULE VALIDATION (Domain Object Responsibility)
        if (role == null)
            throw new ArgumentNullException(nameof(role));

        if (string.IsNullOrWhiteSpace(assignedBy))
            throw new ArgumentException("AssignedBy is required", nameof(assignedBy));

        if (RoleMemberships.Count >= MAX_ROLES)
            throw new DomainException($"User '{Name}' has reached maximum role limit of {MAX_ROLES}");

        if (RoleMemberships.Contains(role))
            return; // Idempotent operation

        // Business rule: Check segregation of duties
        if (HasAdminRole() && role.Name.Equals("Auditor", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Segregation of duties violation: Administrator cannot also be Auditor");

        if (IsAuditor() && role.Name.Equals("Administrator", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Segregation of duties violation: Auditor cannot also be Administrator");

        // DELEGATION TO NORMALIZER (Pure Behavioral Transformation)
        AssignUserToRoleNormalizer.Execute(this, role);
        
        // EF Core change tracking will handle persistence automatically
    }

    /// <summary>
    /// Unassigns this user from a role with business rule validation
    /// </summary>
    public void UnAssignFromRole(Role role, string unassignedBy)
    {
        // BUSINESS RULE VALIDATION
        if (role == null)
            throw new ArgumentNullException(nameof(role));

        if (string.IsNullOrWhiteSpace(unassignedBy))
            throw new ArgumentException("UnassignedBy is required", nameof(unassignedBy));

        if (!RoleMemberships.Contains(role))
            return; // Idempotent operation

        // Business rule: Prevent removing last admin
        if (IsLastSystemAdmin(role))
            throw new DomainException($"Cannot remove last system administrator role from user '{Name}'");

        // DELEGATION TO NORMALIZER
        UnAssignUserFromRoleNormalizer.Execute(this, role);
        
        // EF Core change tracking will handle persistence automatically
    }

    /// <summary>
    /// Adds this user to a group with business rule validation
    /// </summary>
    public void JoinGroup(Group group, string addedBy)
    {
        // BUSINESS RULE VALIDATION
        if (group == null)
            throw new ArgumentNullException(nameof(group));

        if (string.IsNullOrWhiteSpace(addedBy))
            throw new ArgumentException("AddedBy is required", nameof(addedBy));

        if (GroupMemberships.Count >= MAX_GROUPS)
            throw new DomainException($"User '{Name}' has reached maximum group membership limit of {MAX_GROUPS}");

        if (GroupMemberships.Contains(group))
            return; // Idempotent operation

        // Business rule: Check if user has permission to join this group
        if (group.Name.Equals("Executives", StringComparison.OrdinalIgnoreCase) && !HasManagerRole())
            throw new DomainException($"User '{Name}' must have Manager role to join Executives group");

        // Delegate to the group to handle the addition
        // This ensures the group's business rules are also enforced
        group.AddUser(this, addedBy);
    }

    /// <summary>
    /// Removes this user from a group with business rule validation
    /// </summary>
    public void LeaveGroup(Group group, string removedBy)
    {
        // BUSINESS RULE VALIDATION
        if (group == null)
            throw new ArgumentNullException(nameof(group));

        if (string.IsNullOrWhiteSpace(removedBy))
            throw new ArgumentException("RemovedBy is required", nameof(removedBy));

        if (!GroupMemberships.Contains(group))
            return; // Idempotent operation

        // Delegate to the group to handle the removal
        // This ensures the group's business rules are also enforced
        group.RemoveUser(this, removedBy);
    }

    /// <summary>
    /// Changes the user's name with validation
    /// </summary>
    public void ChangeName(string newName, string changedBy)
    {
        // BUSINESS RULE VALIDATION
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Name cannot be empty", nameof(newName));

        if (string.IsNullOrWhiteSpace(changedBy))
            throw new ArgumentException("ChangedBy is required", nameof(changedBy));

        if (newName.Length > 255)
            throw new ArgumentException("Name cannot exceed 255 characters", nameof(newName));

        if (newName == Name)
            return; // No change needed

        // Business rule: Admin users cannot change their own name
        if (HasAdminRole() && changedBy == this.Id.ToString())
            throw new DomainException("Administrator users cannot change their own name for audit purposes");

        var oldName = Name;
        Name = newName;
        
        // EF Core change tracking will handle persistence and audit trail automatically
    }

    // Legacy methods for backward compatibility - delegate to new methods
    [Obsolete("Use AssignToRole(Role, string) instead to properly track who made the change")]
    public void AssignToRole(Role role) => throw new NotSupportedException("Use AssignToRole(Role, string) overload");

    [Obsolete("Use UnAssignFromRole(Role, string) instead to properly track who made the change")]
    public void UnAssignFromRole(Role role) => throw new NotSupportedException("Use UnAssignFromRole(Role, string) overload");

    [Obsolete("Use JoinGroup(Group, string) instead to properly track who made the change")]
    public void AddToGroup(Group group) => throw new NotSupportedException("Use JoinGroup(Group, string) instead");

    [Obsolete("Use LeaveGroup(Group, string) instead to properly track who made the change")]
    public void RemoveFromGroup(Group group) => throw new NotSupportedException("Use LeaveGroup(Group, string) instead");

    // PRIVATE BUSINESS LOGIC METHODS

    public bool HasAdminRole()
    {
        return RoleMemberships.Any(r => r.Name.Equals("Administrator", StringComparison.OrdinalIgnoreCase) ||
                                       r.Name.Equals("Admin", StringComparison.OrdinalIgnoreCase));
    }

    public bool IsAuditor()
    {
        return RoleMemberships.Any(r => r.Name.Equals("Auditor", StringComparison.OrdinalIgnoreCase));
    }

    private bool HasManagerRole()
    {
        return RoleMemberships.Any(r => r.Name.Equals("Manager", StringComparison.OrdinalIgnoreCase));
    }

    private bool IsLastSystemAdmin(Role roleToRemove)
    {
        // Check if this is an admin role and if removing it would leave no system admins
        if (!roleToRemove.Name.Equals("Administrator", StringComparison.OrdinalIgnoreCase) &&
            !roleToRemove.Name.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            return false;

        // Count total system admins (would need access to all users - simplified for now)
        // In a real implementation, this might require a domain service
        return false; // Simplified - would need system-wide check
    }

    // GDPR-related properties for compliance tracking
    public string? PseudonymId { get; set; }
    public bool IsPseudonymized { get; set; }
    public bool IsAnonymized { get; set; }
    public DateTime? AnonymizedAt { get; set; }
}

