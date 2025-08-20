namespace ACS.Service.Domain.Events;

/// <summary>
/// Event fired when a user is created
/// </summary>
public class UserCreatedEvent : EntityCreatedEvent, INotificationEvent
{
    public override string EventType => "User.Created";
    
    // INotificationEvent implementation
    public List<string> NotificationRecipients { get; private set; } = new();
    public string NotificationTemplate => "UserWelcome";
    public List<string> NotificationChannels { get; private set; } = new() { "Email" };

    public UserCreatedEvent(User user, string? justification = null) 
        : base(user, justification)
    {
        NotificationRecipients.Add($"user-{user.Id}@system.com");
        AddMetadata("GroupMemberships", user.GroupMemberships.Count);
        AddMetadata("RoleMemberships", user.RoleMemberships.Count);
    }
}

/// <summary>
/// Event fired when a user is assigned to a role
/// </summary>
public class UserAssignedToRoleEvent : EntityEvent, ISecurityEvent
{
    public override string EventType => "User.AssignedToRole";
    public override string OperationType => "AssignRole";
    
    public int RoleId { get; private set; }
    public string RoleName { get; private set; }
    
    // ISecurityEvent implementation
    public string SecurityEventType => "RoleAssignment";
    public SecurityRiskLevel RiskLevel { get; private set; }
    public string? SourceIpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsSuspicious { get; private set; }

    public UserAssignedToRoleEvent(User user, Role role, string? justification = null)
        : base(user, justification)
    {
        RoleId = role.Id;
        RoleName = role.Name;
        RiskLevel = DetermineRiskLevel(role);
        IsSuspicious = DetectSuspiciousAssignment(role);
        
        AddMetadata("RoleId", role.Id);
        AddMetadata("RoleName", role.Name);
        AddMetadata("TotalUserRoles", user.RoleMemberships.Count + 1);
    }

    private SecurityRiskLevel DetermineRiskLevel(Role role)
    {
        var highRiskRoles = new[] { "Administrator", "SecurityAdmin", "SystemAdmin" };
        var criticalRoles = new[] { "SuperAdmin", "RootAdmin" };
        
        if (criticalRoles.Contains(role.Name, StringComparer.OrdinalIgnoreCase))
            return SecurityRiskLevel.Critical;
        if (highRiskRoles.Contains(role.Name, StringComparer.OrdinalIgnoreCase))
            return SecurityRiskLevel.High;
        if (role.Permissions.Any(p => p.Uri.Contains("/admin") || p.Uri.Contains("/system")))
            return SecurityRiskLevel.Medium;
            
        return SecurityRiskLevel.Low;
    }

    private bool DetectSuspiciousAssignment(Role role)
    {
        // Check for suspicious patterns like admin role assignments outside business hours
        var isOutsideBusinessHours = DateTime.UtcNow.Hour < 8 || DateTime.UtcNow.Hour > 18;
        var isHighRiskRole = RiskLevel >= SecurityRiskLevel.High;
        
        return isOutsideBusinessHours && isHighRiskRole;
    }
}

/// <summary>
/// Event fired when a user is removed from a role
/// </summary>
public class UserRemovedFromRoleEvent : EntityEvent, ISecurityEvent
{
    public override string EventType => "User.RemovedFromRole";
    public override string OperationType => "UnassignRole";
    
    public int RoleId { get; private set; }
    public string RoleName { get; private set; }
    
    // ISecurityEvent implementation
    public string SecurityEventType => "RoleUnassignment";
    public SecurityRiskLevel RiskLevel => SecurityRiskLevel.Medium;
    public string? SourceIpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsSuspicious => false;

    public UserRemovedFromRoleEvent(User user, Role role, string? justification = null)
        : base(user, justification)
    {
        RoleId = role.Id;
        RoleName = role.Name;
        
        AddMetadata("RemovedRoleId", role.Id);
        AddMetadata("RemovedRoleName", role.Name);
        AddMetadata("RemainingUserRoles", Math.Max(0, user.RoleMemberships.Count - 1));
    }
}

/// <summary>
/// Event fired when a user is added to a group
/// </summary>
public class UserAddedToGroupEvent : EntityEvent
{
    public override string EventType => "User.AddedToGroup";
    public override string OperationType => "AddToGroup";
    
    public int GroupId { get; private set; }
    public string GroupName { get; private set; }

    public UserAddedToGroupEvent(User user, Group group, string? justification = null)
        : base(user, justification)
    {
        GroupId = group.Id;
        GroupName = group.Name;
        
        AddMetadata("GroupId", group.Id);
        AddMetadata("GroupName", group.Name);
        AddMetadata("TotalUserGroups", user.GroupMemberships.Count + 1);
    }
}

/// <summary>
/// Event fired when a user is removed from a group
/// </summary>
public class UserRemovedFromGroupEvent : EntityEvent
{
    public override string EventType => "User.RemovedFromGroup";
    public override string OperationType => "RemoveFromGroup";
    
    public int GroupId { get; private set; }
    public string GroupName { get; private set; }

    public UserRemovedFromGroupEvent(User user, Group group, string? justification = null)
        : base(user, justification)
    {
        GroupId = group.Id;
        GroupName = group.Name;
        
        AddMetadata("RemovedGroupId", group.Id);
        AddMetadata("RemovedGroupName", group.Name);
        AddMetadata("RemainingUserGroups", Math.Max(0, user.GroupMemberships.Count - 1));
    }
}

/// <summary>
/// Event fired when user authentication occurs
/// </summary>
public class UserAuthenticationEvent : DomainEvent, ISecurityEvent, IAsyncEvent
{
    public override string EventType => "User.Authentication";
    
    public int UserId { get; private set; }
    public string UserName { get; private set; }
    public bool IsSuccessful { get; private set; }
    public string? FailureReason { get; private set; }
    public string AuthenticationMethod { get; private set; }
    
    // ISecurityEvent implementation
    public string SecurityEventType => "Authentication";
    public SecurityRiskLevel RiskLevel { get; private set; }
    public string? SourceIpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsSuspicious { get; private set; }
    
    // IAsyncEvent implementation
    public int MaxRetries => 3;
    public TimeSpan RetryDelay => TimeSpan.FromSeconds(5);

    public UserAuthenticationEvent(int userId, string userName, bool isSuccessful, 
        string authenticationMethod, string? failureReason = null)
    {
        UserId = userId;
        UserName = userName;
        IsSuccessful = isSuccessful;
        FailureReason = failureReason;
        AuthenticationMethod = authenticationMethod;
        
        RiskLevel = DetermineRiskLevel();
        IsSuspicious = DetectSuspiciousLogin();
        
        AddMetadata("IsSuccessful", isSuccessful);
        AddMetadata("AuthenticationMethod", authenticationMethod);
        AddMetadata("FailureReason", failureReason ?? "N/A");
    }

    private SecurityRiskLevel DetermineRiskLevel()
    {
        if (!IsSuccessful)
            return SecurityRiskLevel.Medium;
        if (IsSuspicious)
            return SecurityRiskLevel.High;
        return SecurityRiskLevel.Low;
    }

    private bool DetectSuspiciousLogin()
    {
        // Check for suspicious patterns
        var isOutsideBusinessHours = DateTime.UtcNow.Hour < 6 || DateTime.UtcNow.Hour > 22;
        var isWeekend = DateTime.UtcNow.DayOfWeek == DayOfWeek.Saturday || DateTime.UtcNow.DayOfWeek == DayOfWeek.Sunday;
        
        // Suspicious if login failure or unusual timing
        return !IsSuccessful || (isOutsideBusinessHours && isWeekend);
    }
}

/// <summary>
/// Event fired when a user's account is locked
/// </summary>
public class UserAccountLockedEvent : EntityEvent, IHighPriorityEvent, INotificationEvent
{
    public override string EventType => "User.AccountLocked";
    public override string OperationType => "LockAccount";
    public EventPriority Priority => EventPriority.High;
    
    public string LockReason { get; private set; }
    public DateTime LockedUntil { get; private set; }
    public int FailedAttempts { get; private set; }
    
    // INotificationEvent implementation
    public List<string> NotificationRecipients { get; private set; } = new();
    public string NotificationTemplate => "AccountLocked";
    public List<string> NotificationChannels { get; private set; } = new() { "Email", "SMS" };

    public UserAccountLockedEvent(User user, string lockReason, DateTime lockedUntil, int failedAttempts, string? justification = null)
        : base(user, justification)
    {
        LockReason = lockReason;
        LockedUntil = lockedUntil;
        FailedAttempts = failedAttempts;
        
        NotificationRecipients.Add($"user-{user.Id}@system.com");
        NotificationRecipients.Add("security@system.com");
        
        AddMetadata("LockReason", lockReason);
        AddMetadata("LockedUntil", lockedUntil);
        AddMetadata("FailedAttempts", failedAttempts);
    }
}

/// <summary>
/// Event fired when a user's account is unlocked
/// </summary>
public class UserAccountUnlockedEvent : EntityEvent, INotificationEvent
{
    public override string EventType => "User.AccountUnlocked";
    public override string OperationType => "UnlockAccount";
    
    public string UnlockReason { get; private set; }
    public string UnlockedBy { get; private set; }
    
    // INotificationEvent implementation
    public List<string> NotificationRecipients { get; private set; } = new();
    public string NotificationTemplate => "AccountUnlocked";
    public List<string> NotificationChannels { get; private set; } = new() { "Email" };

    public UserAccountUnlockedEvent(User user, string unlockReason, string unlockedBy, string? justification = null)
        : base(user, justification)
    {
        UnlockReason = unlockReason;
        UnlockedBy = unlockedBy;
        
        NotificationRecipients.Add($"user-{user.Id}@system.com");
        
        AddMetadata("UnlockReason", unlockReason);
        AddMetadata("UnlockedBy", unlockedBy);
    }
}

/// <summary>
/// Event fired when suspicious user activity is detected
/// </summary>
public class SuspiciousUserActivityEvent : DomainEvent, IHighPriorityEvent, ISecurityEvent, INotificationEvent
{
    public override string EventType => "User.SuspiciousActivity";
    public EventPriority Priority => EventPriority.Critical;
    
    public int UserId { get; private set; }
    public string UserName { get; private set; }
    public string ActivityType { get; private set; }
    public string ActivityDescription { get; private set; }
    public Dictionary<string, object> ActivityDetails { get; private set; }
    
    // ISecurityEvent implementation
    public string SecurityEventType => "SuspiciousActivity";
    public SecurityRiskLevel RiskLevel => SecurityRiskLevel.Critical;
    public string? SourceIpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsSuspicious => true;
    
    // INotificationEvent implementation
    public List<string> NotificationRecipients { get; private set; } = new();
    public string NotificationTemplate => "SuspiciousActivityAlert";
    public List<string> NotificationChannels { get; private set; } = new() { "Email", "SMS", "Push" };

    public SuspiciousUserActivityEvent(int userId, string userName, string activityType, 
        string activityDescription, Dictionary<string, object> activityDetails)
    {
        UserId = userId;
        UserName = userName;
        ActivityType = activityType;
        ActivityDescription = activityDescription;
        ActivityDetails = activityDetails ?? new Dictionary<string, object>();
        
        NotificationRecipients.AddRange(new[] { "security@system.com", "admin@system.com" });
        
        AddMetadata("ActivityType", activityType);
        AddMetadata("ThreatLevel", "HIGH");
        foreach (var detail in ActivityDetails)
        {
            AddMetadata($"ActivityDetail.{detail.Key}", detail.Value);
        }
    }
}