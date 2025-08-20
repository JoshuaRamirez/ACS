namespace ACS.Service.Domain.Events;

/// <summary>
/// Base class for entity-related events
/// </summary>
public abstract class EntityEvent : DomainEvent, IAuditableEvent
{
    public int EntityId { get; protected set; }
    public string EntityName { get; protected set; } = string.Empty;
    public string EntityTypeName { get; protected set; } = string.Empty;
    
    // IAuditableEvent implementation
    public string? AffectedEntityType => EntityTypeName;
    public string? AffectedEntityId => EntityId.ToString();
    public abstract string OperationType { get; }
    public object? PreviousState { get; protected set; }
    public object? NewState { get; protected set; }
    public string? Justification { get; protected set; }

    protected EntityEvent(Entity entity, string? justification = null)
    {
        EntityId = entity.Id;
        EntityName = entity.Name;
        EntityTypeName = entity.GetType().Name;
        Justification = justification;
    }
}

/// <summary>
/// Event fired when an entity is created
/// </summary>
public class EntityCreatedEvent : EntityEvent
{
    public override string EventType => "Entity.Created";
    public override string OperationType => "Create";

    public EntityCreatedEvent(Entity entity, string? justification = null) 
        : base(entity, justification)
    {
        NewState = SerializeEntity(entity);
        AddMetadata("CreationTimestamp", DateTime.UtcNow);
        AddMetadata("InitialPermissions", entity.Permissions.Count);
    }

    private object SerializeEntity(Entity entity)
    {
        return new
        {
            entity.Id,
            entity.Name,
            EntityType = entity.GetType().Name,
            PermissionCount = entity.Permissions.Count,
            ChildrenCount = entity.Children.Count,
            ParentsCount = entity.Parents.Count
        };
    }
}

/// <summary>
/// Event fired when an entity is updated
/// </summary>
public class EntityUpdatedEvent : EntityEvent
{
    public override string EventType => "Entity.Updated";
    public override string OperationType => "Update";
    public List<string> ChangedProperties { get; private set; } = new();

    public EntityUpdatedEvent(Entity entity, Entity? previousEntity = null, string? justification = null) 
        : base(entity, justification)
    {
        NewState = SerializeEntity(entity);
        if (previousEntity != null)
        {
            PreviousState = SerializeEntity(previousEntity);
            ChangedProperties = DetectChanges(previousEntity, entity);
        }
        
        AddMetadata("ChangeCount", ChangedProperties.Count);
        AddMetadata("UpdateTimestamp", DateTime.UtcNow);
    }

    private List<string> DetectChanges(Entity previous, Entity current)
    {
        var changes = new List<string>();
        
        if (previous.Name != current.Name)
            changes.Add("Name");
        if (previous.Permissions.Count != current.Permissions.Count)
            changes.Add("Permissions");
        if (previous.Children.Count != current.Children.Count)
            changes.Add("Children");
        if (previous.Parents.Count != current.Parents.Count)
            changes.Add("Parents");
            
        return changes;
    }

    private object SerializeEntity(Entity entity)
    {
        return new
        {
            entity.Id,
            entity.Name,
            EntityType = entity.GetType().Name,
            PermissionCount = entity.Permissions.Count,
            ChildrenCount = entity.Children.Count,
            ParentsCount = entity.Parents.Count
        };
    }
}

/// <summary>
/// Event fired when an entity is deleted
/// </summary>
public class EntityDeletedEvent : EntityEvent
{
    public override string EventType => "Entity.Deleted";
    public override string OperationType => "Delete";
    public bool IsSoftDelete { get; private set; }
    public List<int> OrphanedChildren { get; private set; } = new();

    public EntityDeletedEvent(Entity entity, bool isSoftDelete = false, string? justification = null) 
        : base(entity, justification)
    {
        IsSoftDelete = isSoftDelete;
        PreviousState = SerializeEntity(entity);
        OrphanedChildren = entity.Children.Select(c => c.Id).ToList();
        
        AddMetadata("SoftDelete", isSoftDelete);
        AddMetadata("OrphanedChildrenCount", OrphanedChildren.Count);
        AddMetadata("DeletionTimestamp", DateTime.UtcNow);
    }

    private object SerializeEntity(Entity entity)
    {
        return new
        {
            entity.Id,
            entity.Name,
            EntityType = entity.GetType().Name,
            PermissionCount = entity.Permissions.Count,
            ChildrenIds = entity.Children.Select(c => c.Id).ToList(),
            ParentIds = entity.Parents.Select(p => p.Id).ToList()
        };
    }
}

/// <summary>
/// Event fired when permissions are added to an entity
/// </summary>
public class PermissionGrantedEvent : EntityEvent, ISecurityEvent
{
    public override string EventType => "Permission.Granted";
    public override string OperationType => "GrantPermission";
    
    public Permission Permission { get; private set; }
    public string SecurityEventType => "PermissionGranted";
    public SecurityRiskLevel RiskLevel { get; private set; }
    public string? SourceIpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsSuspicious { get; private set; }

    public PermissionGrantedEvent(Entity entity, Permission permission, string? justification = null)
        : base(entity, justification)
    {
        Permission = permission;
        NewState = SerializePermission(permission);
        RiskLevel = DetermineRiskLevel(permission);
        IsSuspicious = DetectSuspiciousActivity(permission);
        
        AddMetadata("PermissionUri", permission.Uri);
        AddMetadata("HttpVerb", permission.HttpVerb.ToString());
        AddMetadata("Grant", permission.Grant);
        AddMetadata("Deny", permission.Deny);
    }

    private SecurityRiskLevel DetermineRiskLevel(Permission permission)
    {
        // High-risk URIs
        var highRiskPatterns = new[] { "/admin", "/system", "/config", "/security" };
        var criticalRiskPatterns = new[] { "/admin/system", "/config/secrets", "/security/keys" };
        
        if (criticalRiskPatterns.Any(pattern => permission.Uri.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            return SecurityRiskLevel.Critical;
        if (highRiskPatterns.Any(pattern => permission.Uri.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            return SecurityRiskLevel.High;
        if (permission.HttpVerb == HttpVerb.DELETE || permission.HttpVerb == HttpVerb.PUT)
            return SecurityRiskLevel.Medium;
            
        return SecurityRiskLevel.Low;
    }

    private bool DetectSuspiciousActivity(Permission permission)
    {
        // Suspicious patterns
        return permission.Uri.Contains("*") && 
               (permission.HttpVerb == HttpVerb.DELETE || permission.Uri.Contains("/admin"));
    }

    private object SerializePermission(Permission permission)
    {
        return new
        {
            permission.Id,
            permission.Uri,
            permission.HttpVerb,
            permission.Grant,
            permission.Deny,
            permission.Scheme
        };
    }
}

/// <summary>
/// Event fired when permissions are removed from an entity
/// </summary>
public class PermissionRevokedEvent : EntityEvent, ISecurityEvent
{
    public override string EventType => "Permission.Revoked";
    public override string OperationType => "RevokePermission";
    
    public Permission Permission { get; private set; }
    public string SecurityEventType => "PermissionRevoked";
    public SecurityRiskLevel RiskLevel => SecurityRiskLevel.Medium;
    public string? SourceIpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsSuspicious => false;

    public PermissionRevokedEvent(Entity entity, Permission permission, string? justification = null)
        : base(entity, justification)
    {
        Permission = permission;
        PreviousState = SerializePermission(permission);
        
        AddMetadata("RevokedPermissionUri", permission.Uri);
        AddMetadata("RevokedHttpVerb", permission.HttpVerb.ToString());
    }

    private object SerializePermission(Permission permission)
    {
        return new
        {
            permission.Id,
            permission.Uri,
            permission.HttpVerb,
            permission.Grant,
            permission.Deny,
            permission.Scheme
        };
    }
}

/// <summary>
/// Event fired when entity relationships change
/// </summary>
public class EntityRelationshipChangedEvent : EntityEvent
{
    public override string EventType => "Entity.RelationshipChanged";
    public override string OperationType => "ChangeRelationship";
    
    public string RelationshipType { get; private set; }
    public int RelatedEntityId { get; private set; }
    public string RelatedEntityName { get; private set; }
    public string ChangeType { get; private set; } // Added, Removed

    public EntityRelationshipChangedEvent(Entity entity, Entity relatedEntity, string relationshipType, string changeType, string? justification = null)
        : base(entity, justification)
    {
        RelationshipType = relationshipType;
        RelatedEntityId = relatedEntity.Id;
        RelatedEntityName = relatedEntity.Name;
        ChangeType = changeType;
        
        AddMetadata("RelationshipType", relationshipType);
        AddMetadata("ChangeType", changeType);
        AddMetadata("RelatedEntityType", relatedEntity.GetType().Name);
    }
}

/// <summary>
/// Event fired when hierarchy violations are detected
/// </summary>
public class HierarchyViolationDetectedEvent : EntityEvent, IHighPriorityEvent, INotificationEvent
{
    public override string EventType => "Entity.HierarchyViolation";
    public override string OperationType => "HierarchyViolation";
    public EventPriority Priority => EventPriority.High;
    
    public string ViolationType { get; private set; }
    public List<int> AffectedEntityIds { get; private set; }
    
    // INotificationEvent implementation
    public List<string> NotificationRecipients { get; private set; } = new();
    public string NotificationTemplate => "HierarchyViolationAlert";
    public List<string> NotificationChannels { get; private set; } = new() { "Email", "System" };

    public HierarchyViolationDetectedEvent(Entity entity, string violationType, List<int> affectedEntityIds, string? justification = null)
        : base(entity, justification)
    {
        ViolationType = violationType;
        AffectedEntityIds = affectedEntityIds;
        NotificationRecipients.AddRange(new[] { "admin@system.com", "security@system.com" });
        
        AddMetadata("ViolationType", violationType);
        AddMetadata("AffectedEntityCount", affectedEntityIds.Count);
        AddMetadata("DetectionTimestamp", DateTime.UtcNow);
    }
}

/// <summary>
/// Event fired when business rule violations are detected
/// </summary>
public class BusinessRuleViolationEvent : DomainEvent, IHighPriorityEvent, INotificationEvent
{
    public override string EventType => "BusinessRule.Violation";
    public EventPriority Priority => EventPriority.High;
    
    public string RuleId { get; private set; }
    public string RuleName { get; private set; }
    public string ViolationDescription { get; private set; }
    public string AffectedEntity { get; private set; }
    public string Severity { get; private set; }
    
    // INotificationEvent implementation
    public List<string> NotificationRecipients { get; private set; } = new();
    public string NotificationTemplate => "BusinessRuleViolationAlert";
    public List<string> NotificationChannels { get; private set; } = new() { "Email", "System" };

    public BusinessRuleViolationEvent(string ruleId, string ruleName, string violationDescription, string affectedEntity, string severity)
    {
        RuleId = ruleId;
        RuleName = ruleName;
        ViolationDescription = violationDescription;
        AffectedEntity = affectedEntity;
        Severity = severity;
        NotificationRecipients.AddRange(new[] { "compliance@system.com", "admin@system.com" });
        
        AddMetadata("RuleId", ruleId);
        AddMetadata("Severity", severity);
        AddMetadata("AffectedEntity", affectedEntity);
    }
}