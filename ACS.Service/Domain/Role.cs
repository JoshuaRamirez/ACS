using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using ACS.Service.Delegates.Normalizers;
using ACS.Service.Domain.Validation;

namespace ACS.Service.Domain;

[LeastPrivilegeBusinessRule(
    ProhibitedCombinations = new[] { "/admin/users,/admin/system", "/finance/payments,/finance/approve" },
    RequiresJustification = new[] { "/admin/system", "/security/keys", "/config/secrets" })]
public class Role : Entity
{
    // Business rule constants
    private const int MAX_USERS_PER_ROLE = 500;
    private const int MAX_GROUPS_PER_ROLE = 50;
    
    // Critical role names that require special handling
    private static readonly string[] CRITICAL_ROLES = { "Administrator", "Admin", "SystemAdmin", "Root" };
    private static readonly string[] AUDIT_ROLES = { "Auditor", "Compliance", "SecurityOfficer" };

    public ReadOnlyCollection<Group> GroupMemberships => Parents.OfType<Group>().ToList().AsReadOnly();
    public ReadOnlyCollection<User> Users => Children.OfType<User>().ToList().AsReadOnly();

    /// <summary>
    /// Assigns a user to this role with full business rule validation
    /// DOMAIN LOGIC: Enforces business rules and validates preconditions
    /// DELEGATION: Uses normalizer for pure state transformation
    /// </summary>
    public void AssignUser(User user, string assignedBy)
    {
        // BUSINESS RULE VALIDATION (Domain Object Responsibility)
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        if (string.IsNullOrWhiteSpace(assignedBy))
            throw new ArgumentException("AssignedBy is required", nameof(assignedBy));

        if (Users.Count >= MAX_USERS_PER_ROLE)
            throw new DomainException($"Role '{Name}' has reached maximum user limit of {MAX_USERS_PER_ROLE}");

        if (Users.Contains(user))
            return; // Idempotent operation

        // Business rule: Critical roles require special approval
        if (IsCriticalRole() && !IsAuthorizedForCriticalRoleAssignment(assignedBy))
            throw new DomainException($"Role '{Name}' is critical and requires special authorization to assign");

        // Business rule: Segregation of duties
        if (IsAuditRole() && user.HasAdminRole())
            throw new DomainException("Segregation of duties violation: Cannot assign audit role to administrator");

        if (IsCriticalRole() && user.IsAuditor())
            throw new DomainException("Segregation of duties violation: Cannot assign admin role to auditor");

        // Business rule: Check for conflicting role combinations
        ValidateRoleCombination(user);

        // DELEGATION TO NORMALIZER (Pure Behavioral Transformation)
        AssignUserToRoleNormalizer.Execute(user, this);
        
        // EF Core change tracking will handle persistence automatically
    }

    /// <summary>
    /// Unassigns a user from this role with business rule validation
    /// </summary>
    public void UnAssignUser(User user, string unassignedBy)
    {
        // BUSINESS RULE VALIDATION
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        if (string.IsNullOrWhiteSpace(unassignedBy))
            throw new ArgumentException("UnassignedBy is required", nameof(unassignedBy));

        if (!Users.Contains(user))
            return; // Idempotent operation

        // Business rule: Prevent removing last critical role user
        if (IsCriticalRole() && IsLastUserWithRole())
            throw new DomainException($"Cannot remove last user from critical role '{Name}'");

        // Business rule: Users cannot remove their own critical roles
        if (IsCriticalRole() && unassignedBy == user.Id.ToString())
            throw new DomainException("Users cannot remove their own critical role assignments");

        // DELEGATION TO NORMALIZER
        UnAssignUserFromRoleNormalizer.Execute(user, this);
        
        // EF Core change tracking will handle persistence automatically
    }

    /// <summary>
    /// Assigns this role to a group with business rule validation
    /// </summary>
    public void AssignToGroup(Group group, string assignedBy)
    {
        // BUSINESS RULE VALIDATION
        if (group == null)
            throw new ArgumentNullException(nameof(group));

        if (string.IsNullOrWhiteSpace(assignedBy))
            throw new ArgumentException("AssignedBy is required", nameof(assignedBy));

        if (GroupMemberships.Count >= MAX_GROUPS_PER_ROLE)
            throw new DomainException($"Role '{Name}' has reached maximum group assignment limit of {MAX_GROUPS_PER_ROLE}");

        if (GroupMemberships.Contains(group))
            return; // Idempotent operation

        // Business rule: Critical roles have group restrictions
        if (IsCriticalRole() && !IsAuthorizedGroupForCriticalRole(group))
            throw new DomainException($"Critical role '{Name}' cannot be assigned to group '{group.Name}'");

        // Delegate to the group to handle the assignment
        // This ensures the group's business rules are also enforced
        group.AssignRole(this, assignedBy);
    }

    /// <summary>
    /// Removes this role from a group with business rule validation
    /// </summary>
    public void RemoveFromGroup(Group group, string removedBy)
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
        group.RemoveRole(this, removedBy);
    }

    /// <summary>
    /// Changes the role's name with validation
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

        // Business rule: Critical roles cannot be renamed
        if (IsCriticalRole())
            throw new DomainException($"Critical role '{Name}' cannot be renamed for security reasons");

        var oldName = Name;
        Name = newName;
        
        // EF Core change tracking will handle persistence and audit trail automatically
    }

    // Legacy methods for backward compatibility
    [Obsolete("Use AssignUser(User, string) instead to properly track who made the change")]
    public void AssignUser(User user) => throw new NotSupportedException("Use AssignUser(User, string) overload");

    [Obsolete("Use UnAssignUser(User, string) instead to properly track who made the change")]
    public void UnAssignUser(User user) => throw new NotSupportedException("Use UnAssignUser(User, string) overload");

    [Obsolete("Use AssignToGroup(Group, string) instead to properly track who made the change")]
    public void AddToGroup(Group group) => throw new NotSupportedException("Use AssignToGroup(Group, string) instead");

    [Obsolete("Use RemoveFromGroup(Group, string) instead to properly track who made the change")]
    public void RemoveFromGroup(Group group) => throw new NotSupportedException("Use RemoveFromGroup(Group, string) instead");

    // PRIVATE BUSINESS LOGIC METHODS

    private bool IsCriticalRole()
    {
        return CRITICAL_ROLES.Any(cr => Name.Equals(cr, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsAuditRole()
    {
        return AUDIT_ROLES.Any(ar => Name.Equals(ar, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsAuthorizedForCriticalRoleAssignment(string assignedBy)
    {
        // In a real implementation, this would check against a list of authorized users
        // For now, we'll implement a simplified check
        return !string.IsNullOrWhiteSpace(assignedBy) && assignedBy != "anonymous";
    }

    private bool IsLastUserWithRole()
    {
        return Users.Count <= 1;
    }

    private void ValidateRoleCombination(User user)
    {
        // Business rule: Check for prohibited role combinations
        var prohibitedCombinations = new Dictionary<string, string[]>
        {
            ["Administrator"] = new[] { "Auditor", "Compliance" },
            ["Auditor"] = new[] { "Administrator", "SystemAdmin" },
            ["Finance"] = new[] { "FinanceApprover" }, // Prevent self-approval
        };

        if (prohibitedCombinations.TryGetValue(Name, out var prohibited))
        {
            var conflictingRole = user.RoleMemberships.FirstOrDefault(r => 
                prohibited.Any(p => r.Name.Equals(p, StringComparison.OrdinalIgnoreCase)));

            if (conflictingRole != null)
            {
                throw new DomainException(
                    $"Cannot assign role '{Name}' to user '{user.Name}' " +
                    $"because they already have conflicting role '{conflictingRole.Name}'");
            }
        }
    }

    private bool IsAuthorizedGroupForCriticalRole(Group group)
    {
        // Business rule: Critical roles can only be assigned to specific groups
        var authorizedGroups = new[] { "Administrators", "SystemAdmins", "ITManagement" };
        return authorizedGroups.Any(ag => group.Name.Equals(ag, StringComparison.OrdinalIgnoreCase));
    }
}

