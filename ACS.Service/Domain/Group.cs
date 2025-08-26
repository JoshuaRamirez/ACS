using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using ACS.Service.Delegates.Normalizers;
using ACS.Service.Domain.Validation;

namespace ACS.Service.Domain;

[GroupMemberLimitsBusinessRule(MaxUsers = 1000, MaxGroups = 100, MaxTotalMembers = 1500)]
[NoCyclicHierarchy(MaxDepth = 20)]
public class Group : Entity
{
    // Business rule constants
    private const int MAX_USERS = 1000;
    private const int MAX_GROUPS = 100;
    private const int MAX_TOTAL_MEMBERS = 1500;
    private const int MAX_HIERARCHY_DEPTH = 20;

    public ReadOnlyCollection<Group> Groups => Children.OfType<Group>().ToList().AsReadOnly();
    public ReadOnlyCollection<Group> ParentGroups => Parents.OfType<Group>().ToList().AsReadOnly();
    public ReadOnlyCollection<User> Users => Children.OfType<User>().ToList().AsReadOnly();
    public ReadOnlyCollection<Role> Roles => Children.OfType<Role>().ToList().AsReadOnly();

    /// <summary>
    /// Adds a user to this group with full business rule validation
    /// DOMAIN LOGIC: Enforces business rules and validates preconditions
    /// DELEGATION: Uses normalizer for pure state transformation
    /// </summary>
    public void AddUser(User user, string addedBy)
    {
        // BUSINESS RULE VALIDATION (Domain Object Responsibility)
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        if (string.IsNullOrWhiteSpace(addedBy))
            throw new ArgumentException("AddedBy is required", nameof(addedBy));

        if (Users.Count >= MAX_USERS)
            throw new DomainException($"Group '{Name}' has reached maximum user limit of {MAX_USERS}");

        if (Users.Contains(user))
            return; // Idempotent operation

        if (GetTotalMemberCount() >= MAX_TOTAL_MEMBERS)
            throw new DomainException($"Group '{Name}' has reached maximum total member limit of {MAX_TOTAL_MEMBERS}");

        // Users cannot create circular references since they are leaf nodes in the hierarchy

        // DELEGATION TO NORMALIZER (Pure Behavioral Transformation)
        AddUserToGroupNormalizer.Execute(user, this);
        
        // EF Core change tracking will handle persistence automatically
    }

    /// <summary>
    /// Removes a user from this group with business rule validation
    /// </summary>
    public void RemoveUser(User user, string removedBy)
    {
        // BUSINESS RULE VALIDATION
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        if (string.IsNullOrWhiteSpace(removedBy))
            throw new ArgumentException("RemovedBy is required", nameof(removedBy));

        if (!Users.Contains(user))
            return; // Idempotent operation

        // Check business rules
        if (IsLastAdminUser(user))
            throw new DomainException($"Cannot remove last admin user '{user.Name}' from group '{Name}'");

        // DELEGATION TO NORMALIZER
        RemoveUserFromGroupNormalizer.Execute(user, this);
        
        // EF Core change tracking will handle persistence automatically
    }

    /// <summary>
    /// Adds a child group to this group with hierarchy validation
    /// </summary>
    public void AddGroup(Group childGroup, string addedBy)
    {
        // BUSINESS RULE VALIDATION
        if (childGroup == null)
            throw new ArgumentNullException(nameof(childGroup));

        if (string.IsNullOrWhiteSpace(addedBy))
            throw new ArgumentException("AddedBy is required", nameof(addedBy));

        if (childGroup == this)
            throw new DomainException("Group cannot contain itself");

        if (Groups.Count >= MAX_GROUPS)
            throw new DomainException($"Group '{Name}' has reached maximum subgroup limit of {MAX_GROUPS}");

        if (Groups.Contains(childGroup))
            return; // Idempotent operation

        if (GetHierarchyDepth() >= MAX_HIERARCHY_DEPTH)
            throw new DomainException($"Maximum hierarchy depth of {MAX_HIERARCHY_DEPTH} exceeded");

        if (WouldCreateCircularReference(childGroup))
            throw new DomainException("Adding this group would create a circular hierarchy");

        // DELEGATION TO NORMALIZER (Pure Behavioral Transformation)
        AddGroupToGroupNormalizer.Execute(childGroup, this);
        
        // EF Core change tracking will handle persistence automatically
    }

    /// <summary>
    /// Removes a child group from this group
    /// </summary>
    public void RemoveGroup(Group childGroup, string removedBy)
    {
        // BUSINESS RULE VALIDATION
        if (childGroup == null)
            throw new ArgumentNullException(nameof(childGroup));

        if (string.IsNullOrWhiteSpace(removedBy))
            throw new ArgumentException("RemovedBy is required", nameof(removedBy));

        if (!Groups.Contains(childGroup))
            return; // Idempotent operation

        // DELEGATION TO NORMALIZER
        RemoveGroupFromGroupNormalizer.Execute(childGroup, this);
        
        // EF Core change tracking will handle persistence automatically
    }

    /// <summary>
    /// Assigns a role to this group
    /// </summary>
    public void AssignRole(Role role, string assignedBy)
    {
        // BUSINESS RULE VALIDATION
        if (role == null)
            throw new ArgumentNullException(nameof(role));

        if (string.IsNullOrWhiteSpace(assignedBy))
            throw new ArgumentException("AssignedBy is required", nameof(assignedBy));

        if (Roles.Contains(role))
            return; // Idempotent operation

        // DELEGATION TO NORMALIZER
        AddRoleToGroupNormalizer.Execute(role, this);
        
        // EF Core change tracking will handle persistence automatically
    }

    /// <summary>
    /// Removes a role from this group
    /// </summary>
    public void RemoveRole(Role role, string removedBy)
    {
        // BUSINESS RULE VALIDATION
        if (role == null)
            throw new ArgumentNullException(nameof(role));

        if (string.IsNullOrWhiteSpace(removedBy))
            throw new ArgumentException("RemovedBy is required", nameof(removedBy));

        if (!Roles.Contains(role))
            return; // Idempotent operation

        // DELEGATION TO NORMALIZER
        RemoveRoleFromGroupNormalizer.Execute(role, this);
        
        // EF Core change tracking will handle persistence automatically
    }

    // Legacy methods for backward compatibility - these should not be used
    [Obsolete("Use AddUser(User, string) instead to properly track who made the change")]
    public void AddUser(User user) => throw new NotSupportedException("Use AddUser(User, string) overload");

    [Obsolete("Use RemoveUser(User, string) instead to properly track who made the change")]
    public void RemoveUser(User user) => throw new NotSupportedException("Use RemoveUser(User, string) overload");

    // PRIVATE BUSINESS LOGIC METHODS

    private int GetTotalMemberCount()
    {
        return Users.Count + Groups.Count + Roles.Count;
    }

    private int GetHierarchyDepth()
    {
        int depth = 0;
        var current = this;
        var visited = new HashSet<int>();

        while (current.ParentGroups.Any() && !visited.Contains(current.Id))
        {
            visited.Add(current.Id);
            depth++;
            current = current.ParentGroups.First();

            if (depth > MAX_HIERARCHY_DEPTH)
                break; // Prevent infinite loop
        }

        return depth;
    }

    private bool WouldCreateCircularReference(Group groupToAdd)
    {
        // BFS to check for circular reference
        var visited = new HashSet<int>();
        var queue = new Queue<Group>();
        queue.Enqueue(groupToAdd);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Id == this.Id)
                return true;

            if (visited.Contains(current.Id))
                continue;

            visited.Add(current.Id);

            foreach (var child in current.Groups)
            {
                queue.Enqueue(child);
            }
        }

        return false;
    }

    private bool IsLastAdminUser(User user)
    {
        // Business logic to check if user is last admin
        var adminRole = Roles.FirstOrDefault(r => r.Name.Equals("Admin", StringComparison.OrdinalIgnoreCase));
        if (adminRole == null) return false;

        var adminUsers = Users.Where(u => u.RoleMemberships.Contains(adminRole));
        return adminUsers.Count() == 1 && adminUsers.Contains(user);
    }

    // Role management methods
    public void AddRole(Role role, string addedBy)
    {
        if (role == null)
            throw new ArgumentNullException(nameof(role));

        if (string.IsNullOrWhiteSpace(addedBy))
            throw new ArgumentException("AddedBy cannot be null or empty", nameof(addedBy));

        // Add role as child entity using base Entity logic
        AddChild(role);
    }

    // Backwards compatibility methods
    public void AddToGroup(Group parent) => parent.AddGroup(this, "System");
    public void RemoveFromGroup(Group parent) => parent.RemoveGroup(this, "System");
}