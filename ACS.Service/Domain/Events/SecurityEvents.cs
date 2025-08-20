namespace ACS.Service.Domain.Events;

/// <summary>
/// Event fired when unauthorized access is attempted
/// </summary>
public class UnauthorizedAccessAttemptEvent : DomainEvent, IHighPriorityEvent, ISecurityEvent, INotificationEvent
{
    public override string EventType => "Security.UnauthorizedAccessAttempt";
    public EventPriority Priority => EventPriority.High;
    
    public string AttemptedResource { get; private set; }
    public string AttemptedAction { get; private set; }
    public int AttemptingUserId { get; private set; }
    public string AttemptingUserName { get; private set; }
    public string DenialReason { get; private set; }
    public List<string> RequiredPermissions { get; private set; }
    
    // ISecurityEvent implementation
    public string SecurityEventType => "UnauthorizedAccess";
    public SecurityRiskLevel RiskLevel { get; private set; }
    public string? SourceIpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsSuspicious { get; private set; }
    
    // INotificationEvent implementation
    public List<string> NotificationRecipients { get; private set; } = new();
    public string NotificationTemplate => "UnauthorizedAccessAlert";
    public List<string> NotificationChannels { get; private set; } = new() { "Email", "System" };

    public UnauthorizedAccessAttemptEvent(string attemptedResource, string attemptedAction, int attemptingUserId, 
        string attemptingUserName, string denialReason, List<string> requiredPermissions)
    {
        AttemptedResource = attemptedResource;
        AttemptedAction = attemptedAction;
        AttemptingUserId = attemptingUserId;
        AttemptingUserName = attemptingUserName;
        DenialReason = denialReason;
        RequiredPermissions = requiredPermissions ?? new List<string>();
        
        RiskLevel = DetermineRiskLevel();
        IsSuspicious = DetectSuspiciousAccess();
        
        NotificationRecipients.AddRange(new[] { "security@system.com", "admin@system.com" });
        
        AddMetadata("AttemptedResource", attemptedResource);
        AddMetadata("AttemptedAction", attemptedAction);
        AddMetadata("DenialReason", denialReason);
        AddMetadata("RequiredPermissionsCount", RequiredPermissions.Count);
    }

    private SecurityRiskLevel DetermineRiskLevel()
    {
        var highRiskResources = new[] { "/admin", "/system", "/config", "/security" };
        var criticalActions = new[] { "DELETE", "PUT" };
        
        if (highRiskResources.Any(r => AttemptedResource.Contains(r, StringComparison.OrdinalIgnoreCase)))
            return SecurityRiskLevel.High;
        if (criticalActions.Contains(AttemptedAction.ToUpper()))
            return SecurityRiskLevel.Medium;
        return SecurityRiskLevel.Low;
    }

    private bool DetectSuspiciousAccess()
    {
        // Multiple failed attempts in short time could indicate attack
        return RiskLevel >= SecurityRiskLevel.High || 
               AttemptedResource.Contains("*") ||
               AttemptedAction == "DELETE";
    }
}

/// <summary>
/// Event fired when a security policy violation is detected
/// </summary>
public class SecurityPolicyViolationEvent : DomainEvent, IHighPriorityEvent, ISecurityEvent, INotificationEvent
{
    public override string EventType => "Security.PolicyViolation";
    public EventPriority Priority => EventPriority.Critical;
    
    public string PolicyId { get; private set; }
    public string PolicyName { get; private set; }
    public string ViolationType { get; private set; }
    public string ViolationDescription { get; private set; }
    public int? UserId { get; private set; }
    public string? UserName { get; private set; }
    public Dictionary<string, object> ViolationContext { get; private set; }
    
    // ISecurityEvent implementation
    public string SecurityEventType => "PolicyViolation";
    public SecurityRiskLevel RiskLevel => SecurityRiskLevel.High;
    public string? SourceIpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsSuspicious => true;
    
    // INotificationEvent implementation
    public List<string> NotificationRecipients { get; private set; } = new();
    public string NotificationTemplate => "SecurityPolicyViolationAlert";
    public List<string> NotificationChannels { get; private set; } = new() { "Email", "SMS", "System" };

    public SecurityPolicyViolationEvent(string policyId, string policyName, string violationType, 
        string violationDescription, Dictionary<string, object> violationContext, int? userId = null, string? userName = null)
    {
        PolicyId = policyId;
        PolicyName = policyName;
        ViolationType = violationType;
        ViolationDescription = violationDescription;
        ViolationContext = violationContext ?? new Dictionary<string, object>();
        UserId = userId;
        UserName = userName;
        
        NotificationRecipients.AddRange(new[] { "security@system.com", "compliance@system.com", "admin@system.com" });
        
        AddMetadata("PolicyId", policyId);
        AddMetadata("ViolationType", violationType);
        AddMetadata("UserId", userId?.ToString() ?? "System");
        
        foreach (var context in ViolationContext.Take(10)) // Limit metadata size
        {
            AddMetadata($"ViolationContext_{context.Key}", context.Value);
        }
    }
}

/// <summary>
/// Event fired when privilege escalation is detected
/// </summary>
public class PrivilegeEscalationDetectedEvent : DomainEvent, IHighPriorityEvent, ISecurityEvent, INotificationEvent
{
    public override string EventType => "Security.PrivilegeEscalationDetected";
    public EventPriority Priority => EventPriority.Critical;
    
    public int UserId { get; private set; }
    public string UserName { get; private set; }
    public string EscalationType { get; private set; }
    public List<string> PreviousPermissions { get; private set; }
    public List<string> NewPermissions { get; private set; }
    public string EscalationMethod { get; private set; }
    public Dictionary<string, object> EscalationDetails { get; private set; }
    
    // ISecurityEvent implementation
    public string SecurityEventType => "PrivilegeEscalation";
    public SecurityRiskLevel RiskLevel => SecurityRiskLevel.Critical;
    public string? SourceIpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsSuspicious => true;
    
    // INotificationEvent implementation
    public List<string> NotificationRecipients { get; private set; } = new();
    public string NotificationTemplate => "PrivilegeEscalationAlert";
    public List<string> NotificationChannels { get; private set; } = new() { "Email", "SMS", "Push", "System" };

    public PrivilegeEscalationDetectedEvent(int userId, string userName, string escalationType, 
        List<string> previousPermissions, List<string> newPermissions, string escalationMethod, Dictionary<string, object> escalationDetails)
    {
        UserId = userId;
        UserName = userName;
        EscalationType = escalationType;
        PreviousPermissions = previousPermissions ?? new List<string>();
        NewPermissions = newPermissions ?? new List<string>();
        EscalationMethod = escalationMethod;
        EscalationDetails = escalationDetails ?? new Dictionary<string, object>();
        
        NotificationRecipients.AddRange(new[] { "security@system.com", "admin@system.com", "ciso@system.com" });
        
        AddMetadata("EscalationType", escalationType);
        AddMetadata("EscalationMethod", escalationMethod);
        AddMetadata("PreviousPermissionsCount", PreviousPermissions.Count);
        AddMetadata("NewPermissionsCount", NewPermissions.Count);
        AddMetadata("PermissionIncrease", NewPermissions.Count - PreviousPermissions.Count);
        
        foreach (var detail in EscalationDetails.Take(5)) // Limit metadata size
        {
            AddMetadata($"EscalationDetail_{detail.Key}", detail.Value);
        }
    }
}

/// <summary>
/// Event fired when anomalous behavior is detected
/// </summary>
public class AnomalousBehaviorDetectedEvent : DomainEvent, ISecurityEvent, IAsyncEvent
{
    public override string EventType => "Security.AnomalousBehaviorDetected";
    
    public int? UserId { get; private set; }
    public string? UserName { get; private set; }
    public string AnomalyType { get; private set; }
    public string AnomalyDescription { get; private set; }
    public double AnomalyScore { get; private set; } // 0.0 to 1.0
    public Dictionary<string, object> BehaviorMetrics { get; private set; }
    public List<string> DetectionRules { get; private set; }
    
    // ISecurityEvent implementation
    public string SecurityEventType => "AnomalousActivity";
    public SecurityRiskLevel RiskLevel { get; private set; }
    public string? SourceIpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsSuspicious => AnomalyScore > 0.7;
    
    // IAsyncEvent implementation
    public int MaxRetries => 2;
    public TimeSpan RetryDelay => TimeSpan.FromSeconds(30);

    public AnomalousBehaviorDetectedEvent(string anomalyType, string anomalyDescription, double anomalyScore, 
        Dictionary<string, object> behaviorMetrics, List<string> detectionRules, int? userId = null, string? userName = null)
    {
        UserId = userId;
        UserName = userName;
        AnomalyType = anomalyType;
        AnomalyDescription = anomalyDescription;
        AnomalyScore = anomalyScore;
        BehaviorMetrics = behaviorMetrics ?? new Dictionary<string, object>();
        DetectionRules = detectionRules ?? new List<string>();
        
        RiskLevel = DetermineRiskLevel(anomalyScore);
        
        AddMetadata("AnomalyType", anomalyType);
        AddMetadata("AnomalyScore", anomalyScore);
        AddMetadata("DetectionRulesCount", DetectionRules.Count);
        
        foreach (var metric in BehaviorMetrics.Take(10)) // Limit metadata size
        {
            AddMetadata($"BehaviorMetric_{metric.Key}", metric.Value);
        }
    }

    private SecurityRiskLevel DetermineRiskLevel(double anomalyScore)
    {
        return anomalyScore switch
        {
            >= 0.9 => SecurityRiskLevel.Critical,
            >= 0.7 => SecurityRiskLevel.High,
            >= 0.5 => SecurityRiskLevel.Medium,
            _ => SecurityRiskLevel.Low
        };
    }
}

/// <summary>
/// Event fired when data breach is detected or suspected
/// </summary>
public class DataBreachDetectedEvent : DomainEvent, IHighPriorityEvent, ISecurityEvent, INotificationEvent
{
    public override string EventType => "Security.DataBreachDetected";
    public EventPriority Priority => EventPriority.Critical;
    
    public string BreachType { get; private set; }
    public string BreachDescription { get; private set; }
    public List<string> AffectedDataTypes { get; private set; }
    public int EstimatedRecordsAffected { get; private set; }
    public DateTime DetectedAt { get; private set; }
    public string DetectionMethod { get; private set; }
    public Dictionary<string, object> BreachIndicators { get; private set; }
    
    // ISecurityEvent implementation
    public string SecurityEventType => "DataBreach";
    public SecurityRiskLevel RiskLevel => SecurityRiskLevel.Critical;
    public string? SourceIpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsSuspicious => true;
    
    // INotificationEvent implementation
    public List<string> NotificationRecipients { get; private set; } = new();
    public string NotificationTemplate => "DataBreachAlert";
    public List<string> NotificationChannels { get; private set; } = new() { "Email", "SMS", "Push", "System", "Phone" };

    public DataBreachDetectedEvent(string breachType, string breachDescription, List<string> affectedDataTypes, 
        int estimatedRecordsAffected, string detectionMethod, Dictionary<string, object> breachIndicators)
    {
        BreachType = breachType;
        BreachDescription = breachDescription;
        AffectedDataTypes = affectedDataTypes ?? new List<string>();
        EstimatedRecordsAffected = estimatedRecordsAffected;
        DetectedAt = DateTime.UtcNow;
        DetectionMethod = detectionMethod;
        BreachIndicators = breachIndicators ?? new Dictionary<string, object>();
        
        NotificationRecipients.AddRange(new[] { 
            "security@system.com", 
            "admin@system.com", 
            "ciso@system.com", 
            "legal@system.com", 
            "compliance@system.com" 
        });
        
        AddMetadata("BreachType", breachType);
        AddMetadata("DetectionMethod", detectionMethod);
        AddMetadata("EstimatedRecordsAffected", estimatedRecordsAffected);
        AddMetadata("AffectedDataTypesCount", AffectedDataTypes.Count);
        AddMetadata("BreachSeverity", "CRITICAL");
        
        foreach (var indicator in BreachIndicators.Take(10)) // Limit metadata size
        {
            AddMetadata($"BreachIndicator_{indicator.Key}", indicator.Value);
        }
    }
}

/// <summary>
/// Event fired when security configuration changes are made
/// </summary>
public class SecurityConfigurationChangedEvent : DomainEvent, ISecurityEvent, IAuditableEvent
{
    public override string EventType => "Security.ConfigurationChanged";
    
    public string ConfigurationType { get; private set; }
    public string ConfigurationKey { get; private set; }
    public object? PreviousValue { get; private set; }
    public object? NewValue { get; private set; }
    public int ChangedByUserId { get; private set; }
    public string ChangedByUserName { get; private set; }
    
    // ISecurityEvent implementation
    public string SecurityEventType => "ConfigurationChange";
    public SecurityRiskLevel RiskLevel { get; private set; }
    public string? SourceIpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsSuspicious { get; private set; }
    
    // IAuditableEvent implementation
    public string? AffectedEntityType => "SecurityConfiguration";
    public string? AffectedEntityId => ConfigurationKey;
    public string OperationType => "ConfigurationChange";
    object? IAuditableEvent.PreviousState => PreviousValue;
    object? IAuditableEvent.NewState => NewValue;
    public string? Justification { get; set; }

    public SecurityConfigurationChangedEvent(string configurationType, string configurationKey, 
        object? previousValue, object? newValue, int changedByUserId, string changedByUserName, string? justification = null)
    {
        ConfigurationType = configurationType;
        ConfigurationKey = configurationKey;
        PreviousValue = previousValue;
        NewValue = newValue;
        ChangedByUserId = changedByUserId;
        ChangedByUserName = changedByUserName;
        Justification = justification;
        
        RiskLevel = DetermineRiskLevel(configurationType, configurationKey);
        IsSuspicious = DetectSuspiciousChange();
        
        AddMetadata("ConfigurationType", configurationType);
        AddMetadata("ConfigurationKey", configurationKey);
        AddMetadata("ChangedBy", changedByUserName);
        AddMetadata("PreviousValue", previousValue?.ToString() ?? "null");
        AddMetadata("NewValue", newValue?.ToString() ?? "null");
    }

    private SecurityRiskLevel DetermineRiskLevel(string type, string key)
    {
        var criticalConfig = new[] { "encryption", "authentication", "authorization", "audit" };
        var highRiskConfig = new[] { "password", "session", "access", "permission" };
        
        if (criticalConfig.Any(c => type.Contains(c, StringComparison.OrdinalIgnoreCase) || 
                                   key.Contains(c, StringComparison.OrdinalIgnoreCase)))
            return SecurityRiskLevel.Critical;
        if (highRiskConfig.Any(c => type.Contains(c, StringComparison.OrdinalIgnoreCase) || 
                                  key.Contains(c, StringComparison.OrdinalIgnoreCase)))
            return SecurityRiskLevel.High;
        return SecurityRiskLevel.Medium;
    }

    private bool DetectSuspiciousChange()
    {
        // Changes outside business hours could be suspicious
        var isOutsideBusinessHours = DateTime.UtcNow.Hour < 8 || DateTime.UtcNow.Hour > 18;
        var isHighRisk = RiskLevel >= SecurityRiskLevel.High;
        
        return isOutsideBusinessHours && isHighRisk;
    }
}