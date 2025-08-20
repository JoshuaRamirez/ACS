namespace ACS.Service.Domain.Events;

/// <summary>
/// Event fired when a group is created
/// </summary>
public class GroupCreatedEvent : EntityCreatedEvent
{
    public override string EventType => "Group.Created";
    
    public int InitialMemberCount { get; private set; }
    public string GroupType { get; private set; }

    public GroupCreatedEvent(Group group, string groupType = "Standard", string? justification = null) 
        : base(group, justification)
    {
        InitialMemberCount = group.Users.Count + group.Groups.Count;
        GroupType = groupType;
        
        AddMetadata("GroupType", groupType);
        AddMetadata("InitialMemberCount", InitialMemberCount);
        AddMetadata("InitialUserCount", group.Users.Count);
        AddMetadata("InitialSubgroupCount", group.Groups.Count);
    }
}

/// <summary>
/// Event fired when a group hierarchy is modified
/// </summary>
public class GroupHierarchyChangedEvent : EntityEvent
{
    public override string EventType => "Group.HierarchyChanged";
    public override string OperationType => "ChangeHierarchy";
    
    public string ChangeType { get; private set; } // Added, Removed, Moved
    public int? ParentGroupId { get; private set; }
    public string? ParentGroupName { get; private set; }
    public int? ChildGroupId { get; private set; }
    public string? ChildGroupName { get; private set; }
    public int NewDepth { get; private set; }

    public GroupHierarchyChangedEvent(Group group, string changeType, int newDepth, 
        Group? parentGroup = null, Group? childGroup = null, string? justification = null)
        : base(group, justification)
    {
        ChangeType = changeType;
        NewDepth = newDepth;
        ParentGroupId = parentGroup?.Id;
        ParentGroupName = parentGroup?.Name;
        ChildGroupId = childGroup?.Id;
        ChildGroupName = childGroup?.Name;
        
        AddMetadata("ChangeType", changeType);
        AddMetadata("NewDepth", newDepth);
        AddMetadata("ParentGroupId", parentGroup?.Id?.ToString() ?? "None");
        AddMetadata("ChildGroupId", childGroup?.Id?.ToString() ?? "None");
    }
}

/// <summary>
/// Event fired when a user is added to a group
/// </summary>
public class GroupMemberAddedEvent : EntityEvent
{
    public override string EventType => "Group.MemberAdded";
    public override string OperationType => "AddMember";
    
    public int MemberId { get; private set; }
    public string MemberName { get; private set; }
    public string MemberType { get; private set; } // User, Role
    public int NewMemberCount { get; private set; }

    public GroupMemberAddedEvent(Group group, Entity member, string? justification = null)
        : base(group, justification)
    {
        MemberId = member.Id;
        MemberName = member.Name;
        MemberType = member.GetType().Name;
        NewMemberCount = CalculateNewMemberCount(group, member);
        
        AddMetadata("MemberId", member.Id);
        AddMetadata("MemberType", MemberType);
        AddMetadata("NewMemberCount", NewMemberCount);
    }

    private int CalculateNewMemberCount(Group group, Entity member)
    {
        var baseCount = group.Users.Count + group.Roles.Count + group.Groups.Count;
        return member switch
        {
            User => baseCount + 1,
            Role => baseCount + 1,
            Group => baseCount + 1,
            _ => baseCount
        };
    }
}

/// <summary>
/// Event fired when a member is removed from a group
/// </summary>
public class GroupMemberRemovedEvent : EntityEvent
{
    public override string EventType => "Group.MemberRemoved";
    public override string OperationType => "RemoveMember";
    
    public int MemberId { get; private set; }
    public string MemberName { get; private set; }
    public string MemberType { get; private set; }
    public int NewMemberCount { get; private set; }
    public bool IsGroupEmpty { get; private set; }

    public GroupMemberRemovedEvent(Group group, Entity member, string? justification = null)
        : base(group, justification)
    {
        MemberId = member.Id;
        MemberName = member.Name;
        MemberType = member.GetType().Name;
        NewMemberCount = CalculateNewMemberCount(group, member);
        IsGroupEmpty = NewMemberCount == 0;
        
        AddMetadata("RemovedMemberId", member.Id);
        AddMetadata("RemovedMemberType", MemberType);
        AddMetadata("NewMemberCount", NewMemberCount);
        AddMetadata("IsGroupEmpty", IsGroupEmpty);
    }

    private int CalculateNewMemberCount(Group group, Entity member)
    {
        var baseCount = group.Users.Count + group.Roles.Count + group.Groups.Count;
        return member switch
        {
            User => Math.Max(0, baseCount - 1),
            Role => Math.Max(0, baseCount - 1),
            Group => Math.Max(0, baseCount - 1),
            _ => baseCount
        };
    }
}

/// <summary>
/// Event fired when group membership limits are exceeded
/// </summary>
public class GroupMembershipLimitExceededEvent : EntityEvent, IHighPriorityEvent, INotificationEvent
{
    public override string EventType => "Group.MembershipLimitExceeded";
    public override string OperationType => "LimitExceeded";
    public EventPriority Priority => EventPriority.High;
    
    public string LimitType { get; private set; } // Users, Groups, Total
    public int CurrentCount { get; private set; }
    public int MaximumAllowed { get; private set; }
    public int ExcessCount { get; private set; }
    
    // INotificationEvent implementation
    public List<string> NotificationRecipients { get; private set; } = new();
    public string NotificationTemplate => "GroupMembershipLimitExceeded";
    public List<string> NotificationChannels { get; private set; } = new() { "Email", "System" };

    public GroupMembershipLimitExceededEvent(Group group, string limitType, int currentCount, int maximumAllowed, string? justification = null)
        : base(group, justification)
    {
        LimitType = limitType;
        CurrentCount = currentCount;
        MaximumAllowed = maximumAllowed;
        ExcessCount = currentCount - maximumAllowed;
        
        NotificationRecipients.AddRange(new[] { "admin@system.com", "compliance@system.com" });
        
        AddMetadata("LimitType", limitType);
        AddMetadata("CurrentCount", currentCount);
        AddMetadata("MaximumAllowed", maximumAllowed);
        AddMetadata("ExcessCount", ExcessCount);
    }
}

/// <summary>
/// Event fired when a circular hierarchy is detected in groups
/// </summary>
public class GroupCircularHierarchyDetectedEvent : EntityEvent, IHighPriorityEvent, INotificationEvent
{
    public override string EventType => "Group.CircularHierarchyDetected";
    public override string OperationType => "CircularHierarchyDetection";
    public EventPriority Priority => EventPriority.Critical;
    
    public List<int> CircularPath { get; private set; }
    public List<string> CircularPathNames { get; private set; }
    public int CycleLength { get; private set; }
    
    // INotificationEvent implementation
    public List<string> NotificationRecipients { get; private set; } = new();
    public string NotificationTemplate => "CircularHierarchyAlert";
    public List<string> NotificationChannels { get; private set; } = new() { "Email", "System", "SMS" };

    public GroupCircularHierarchyDetectedEvent(Group group, List<Group> circularPath, string? justification = null)
        : base(group, justification)
    {
        CircularPath = circularPath.Select(g => g.Id).ToList();
        CircularPathNames = circularPath.Select(g => g.Name).ToList();
        CycleLength = circularPath.Count;
        
        NotificationRecipients.AddRange(new[] { "admin@system.com", "security@system.com", "system-alerts@system.com" });
        
        AddMetadata("CycleLength", CycleLength);
        AddMetadata("CircularPath", string.Join(" -> ", CircularPathNames));
        AddMetadata("DetectionTimestamp", DateTime.UtcNow);
    }
}

/// <summary>
/// Event fired when group permissions are inherited or propagated
/// </summary>
public class GroupPermissionInheritanceEvent : EntityEvent
{
    public override string EventType => "Group.PermissionInheritance";
    public override string OperationType => "InheritPermissions";
    
    public int SourceGroupId { get; private set; }
    public string SourceGroupName { get; private set; }
    public List<Permission> InheritedPermissions { get; private set; }
    public int AffectedMembersCount { get; private set; }
    public string InheritanceType { get; private set; } // Direct, Transitive

    public GroupPermissionInheritanceEvent(Group group, Group sourceGroup, List<Permission> inheritedPermissions, 
        int affectedMembersCount, string inheritanceType = "Direct", string? justification = null)
        : base(group, justification)
    {
        SourceGroupId = sourceGroup.Id;
        SourceGroupName = sourceGroup.Name;
        InheritedPermissions = inheritedPermissions;
        AffectedMembersCount = affectedMembersCount;
        InheritanceType = inheritanceType;
        
        AddMetadata("SourceGroupId", sourceGroup.Id);
        AddMetadata("InheritedPermissionCount", inheritedPermissions.Count);
        AddMetadata("AffectedMembersCount", affectedMembersCount);
        AddMetadata("InheritanceType", inheritanceType);
        
        foreach (var permission in inheritedPermissions.Take(10)) // Limit metadata size
        {
            AddMetadata($"InheritedPermission_{permission.Id}", $"{permission.Uri}:{permission.HttpVerb}");
        }
    }
}

/// <summary>
/// Event fired when group-based access policy is applied
/// </summary>
public class GroupAccessPolicyAppliedEvent : EntityEvent, IAsyncEvent
{
    public override string EventType => "Group.AccessPolicyApplied";
    public override string OperationType => "ApplyAccessPolicy";
    
    public string PolicyName { get; private set; }
    public string PolicyType { get; private set; }
    public Dictionary<string, object> PolicyParameters { get; private set; }
    public int AffectedUsersCount { get; private set; }
    public List<string> AppliedResources { get; private set; }
    
    // IAsyncEvent implementation
    public int MaxRetries => 3;
    public TimeSpan RetryDelay => TimeSpan.FromSeconds(10);

    public GroupAccessPolicyAppliedEvent(Group group, string policyName, string policyType, 
        Dictionary<string, object> policyParameters, int affectedUsersCount, List<string> appliedResources, string? justification = null)
        : base(group, justification)
    {
        PolicyName = policyName;
        PolicyType = policyType;
        PolicyParameters = policyParameters ?? new Dictionary<string, object>();
        AffectedUsersCount = affectedUsersCount;
        AppliedResources = appliedResources ?? new List<string>();
        
        AddMetadata("PolicyName", policyName);
        AddMetadata("PolicyType", policyType);
        AddMetadata("AffectedUsersCount", affectedUsersCount);
        AddMetadata("AppliedResourcesCount", AppliedResources.Count);
        
        foreach (var param in PolicyParameters.Take(5)) // Limit metadata size
        {
            AddMetadata($"PolicyParam_{param.Key}", param.Value);
        }
    }
}

/// <summary>
/// Event fired when group cleanup operations are performed
/// </summary>
public class GroupCleanupPerformedEvent : EntityEvent
{
    public override string EventType => "Group.CleanupPerformed";
    public override string OperationType => "Cleanup";
    
    public string CleanupType { get; private set; } // EmptyGroups, OrphanedPermissions, InvalidHierarchy
    public int RemovedItemsCount { get; private set; }
    public List<string> CleanupActions { get; private set; }
    public Dictionary<string, int> CleanupStats { get; private set; }

    public GroupCleanupPerformedEvent(Group group, string cleanupType, int removedItemsCount, 
        List<string> cleanupActions, Dictionary<string, int> cleanupStats, string? justification = null)
        : base(group, justification)
    {
        CleanupType = cleanupType;
        RemovedItemsCount = removedItemsCount;
        CleanupActions = cleanupActions ?? new List<string>();
        CleanupStats = cleanupStats ?? new Dictionary<string, int>();
        
        AddMetadata("CleanupType", cleanupType);
        AddMetadata("RemovedItemsCount", removedItemsCount);
        AddMetadata("ActionsCount", CleanupActions.Count);
        
        foreach (var action in CleanupActions.Take(10)) // Limit metadata size
        {
            AddMetadata($"CleanupAction_{CleanupActions.IndexOf(action)}", action);
        }
        
        foreach (var stat in CleanupStats)
        {
            AddMetadata($"CleanupStat_{stat.Key}", stat.Value);
        }
    }
}