using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace ACS.Service.Domain;

public class Authorization
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AuthorizationType Type { get; set; }
    public AuthorizationPolicy Policy { get; set; } = new();
    public List<AuthorizationRule> Rules { get; set; } = new();
    public Dictionary<string, object> Context { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Evaluation methods
    public bool Evaluate(Entity subject, Resource resource, string action)
    {
        if (!IsActive)
            return false;

        // Check policy first
        if (!Policy.Evaluate(subject, resource, action, Context))
            return false;

        // Evaluate all rules
        var results = Rules.Select(rule => rule.Evaluate(subject, resource, action, Context)).ToList();
        
        // Apply combination logic
        return Type switch
        {
            AuthorizationType.AllowOverride => results.Any(r => r == true),
            AuthorizationType.DenyOverride => !results.Any(r => r == false),
            AuthorizationType.Unanimous => results.All(r => r == true),
            AuthorizationType.Consensus => results.Count(r => r == true) > results.Count / 2,
            AuthorizationType.FirstApplicable => results.FirstOrDefault(r => r.HasValue) ?? false,
            _ => false
        };
    }

    public AuthorizationResult EvaluateWithDetails(Entity subject, Resource resource, string action)
    {
        var result = new AuthorizationResult
        {
            IsAuthorized = false,
            AuthorizationId = Id,
            Subject = subject,
            Resource = resource,
            Action = action,
            EvaluatedAt = DateTime.UtcNow
        };

        if (!IsActive)
        {
            result.Reason = "Authorization is not active";
            return result;
        }

        // Check policy
        var policyResult = Policy.EvaluateWithDetails(subject, resource, action, Context);
        result.PolicyResult = policyResult;
        
        if (!policyResult.IsAllowed)
        {
            result.Reason = $"Policy denied: {policyResult.Reason}";
            return result;
        }

        // Evaluate rules
        foreach (var rule in Rules.OrderBy(r => r.Priority))
        {
            var ruleResult = rule.EvaluateWithDetails(subject, resource, action, Context);
            result.RuleResults.Add(ruleResult);
            
            if (Type == AuthorizationType.FirstApplicable && ruleResult.Decision.HasValue)
            {
                result.IsAuthorized = ruleResult.Decision.Value;
                result.Reason = ruleResult.Reason;
                result.AppliedRule = rule;
                return result;
            }
        }

        // Apply combination logic
        var decisions = result.RuleResults.Where(r => r.Decision.HasValue).Select(r => r.Decision!.Value).ToList();
        
        result.IsAuthorized = Type switch
        {
            AuthorizationType.AllowOverride => decisions.Any(d => d),
            AuthorizationType.DenyOverride => !decisions.Any(d => !d),
            AuthorizationType.Unanimous => decisions.All(d => d),
            AuthorizationType.Consensus => decisions.Count(d => d) > decisions.Count / 2,
            _ => false
        };

        result.Reason = result.IsAuthorized ? "Authorization granted" : "Authorization denied";
        return result;
    }

    public void AddRule(AuthorizationRule rule)
    {
        rule.AuthorizationId = Id;
        Rules.Add(rule);
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveRule(int ruleId)
    {
        Rules.RemoveAll(r => r.Id == ruleId);
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdatePolicy(AuthorizationPolicy policy)
    {
        Policy = policy;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetContext(string key, object value)
    {
        Context[key] = value;
    }

    public void ClearContext()
    {
        Context.Clear();
    }
}

public enum AuthorizationType
{
    AllowOverride,    // If any rule allows, authorization is granted
    DenyOverride,     // If any rule denies, authorization is denied
    Unanimous,        // All rules must allow
    Consensus,        // Majority of rules must allow
    FirstApplicable   // First matching rule decides
}

public class AuthorizationPolicy
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PolicyType Type { get; set; }
    public string Expression { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public List<PolicyCondition> Conditions { get; set; } = new();
    public bool RequiresAuthentication { get; set; } = true;
    public List<string> RequiredClaims { get; set; } = new();
    public TimeSpan? MaxAge { get; set; }
    
    public bool Evaluate(Entity? subject, Resource resource, string action, Dictionary<string, object> context)
    {
        // Check authentication requirement
        if (RequiresAuthentication && subject == null)
            return false;

        // Check required claims
        if (RequiredClaims.Any() && subject != null)
        {
            var subjectClaims = GetSubjectClaims(subject);
            if (!RequiredClaims.All(claim => subjectClaims.ContainsKey(claim)))
                return false;
        }

        // Evaluate conditions (skip if subject is null and authentication not required)
        if (subject != null && !Conditions.All(condition => condition.Evaluate(subject, resource, action, context)))
            return false;

        // Evaluate expression based on type
        return Type switch
        {
            PolicyType.Simple => subject != null ? EvaluateSimpleExpression(subject, resource, action, context) : false,
            PolicyType.Script => subject != null ? EvaluateScriptExpression(subject, resource, action, context) : false,
            PolicyType.Regex => EvaluateRegexExpression(resource, action),
            PolicyType.Custom => subject != null ? EvaluateCustomExpression(subject, resource, action, context) : false,
            _ => false
        };
    }

    public PolicyEvaluationResult EvaluateWithDetails(Entity? subject, Resource resource, string action, Dictionary<string, object> context)
    {
        var result = new PolicyEvaluationResult
        {
            PolicyId = Id,
            PolicyName = Name,
            IsAllowed = false,
            EvaluatedAt = DateTime.UtcNow
        };

        if (RequiresAuthentication && subject == null)
        {
            result.Reason = "Authentication required";
            return result;
        }

        if (RequiredClaims.Any() && subject != null)
        {
            var subjectClaims = GetSubjectClaims(subject);
            var missingClaims = RequiredClaims.Where(claim => !subjectClaims.ContainsKey(claim)).ToList();
            if (missingClaims.Any())
            {
                result.Reason = $"Missing required claims: {string.Join(", ", missingClaims)}";
                return result;
            }
        }

        foreach (var condition in Conditions)
        {
            var conditionResult = subject != null ? condition.EvaluateWithDetails(subject, resource, action, context) : 
                new ConditionEvaluationResult { IsMet = false, Reason = "Subject is null" };
            result.ConditionResults.Add(conditionResult);
            if (!conditionResult.IsMet)
            {
                result.Reason = $"Condition not met: {conditionResult.Reason}";
                return result;
            }
        }

        result.IsAllowed = Evaluate(subject, resource, action, context);
        result.Reason = result.IsAllowed ? "Policy allows access" : "Policy denies access";
        return result;
    }

    private bool EvaluateSimpleExpression(Entity subject, Resource resource, string action, Dictionary<string, object> context)
    {
        // Simple expression: "subject.role == 'admin' && resource.type == 'document'"
        try
        {
            var evaluator = new SimpleExpressionEvaluator(Expression);
            return evaluator.Evaluate(subject, resource, action, context);
        }
        catch
        {
            return false;
        }
    }

    private bool EvaluateScriptExpression(Entity subject, Resource resource, string action, Dictionary<string, object> context)
    {
        // Script-based evaluation (would use scripting engine in production)
        // For now, simplified logic
        return Expression.Contains("allow") && !Expression.Contains("deny");
    }

    private bool EvaluateRegexExpression(Resource resource, string action)
    {
        try
        {
            var regex = new Regex(Expression, RegexOptions.IgnoreCase);
            return regex.IsMatch($"{resource.Uri}:{action}");
        }
        catch
        {
            return false;
        }
    }

    private bool EvaluateCustomExpression(Entity subject, Resource resource, string action, Dictionary<string, object> context)
    {
        // Custom evaluation logic based on parameters
        if (Parameters.ContainsKey("customLogic"))
        {
            var logic = Parameters["customLogic"].ToString();
            return logic == "allow";
        }
        return false;
    }

    private Dictionary<string, object> GetSubjectClaims(Entity subject)
    {
        var claims = new Dictionary<string, object>();
        
        if (subject is User user)
        {
            claims["id"] = user.Id;
            claims["name"] = user.Name;
            claims["type"] = "user";
            // Add more user-specific claims
        }
        else if (subject is Group group)
        {
            claims["id"] = group.Id;
            claims["name"] = group.Name;
            claims["type"] = "group";
        }
        else if (subject is Role role)
        {
            claims["id"] = role.Id;
            claims["name"] = role.Name;
            claims["type"] = "role";
        }
        
        return claims;
    }
}

public enum PolicyType
{
    Simple,   // Simple boolean expression
    Script,   // Script-based evaluation
    Regex,    // Regular expression matching
    Custom    // Custom evaluation logic
}

public class PolicyCondition
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ConditionType Type { get; set; }
    public string Property { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public object? Value { get; set; }
    public bool Negate { get; set; }
    
    public bool Evaluate(Entity subject, Resource resource, string action, Dictionary<string, object> context)
    {
        var result = Type switch
        {
            ConditionType.Subject => EvaluateSubjectCondition(subject),
            ConditionType.Resource => EvaluateResourceCondition(resource),
            ConditionType.Action => EvaluateActionCondition(action),
            ConditionType.Context => EvaluateContextCondition(context),
            ConditionType.Time => EvaluateTimeCondition(),
            _ => false
        };
        
        return Negate ? !result : result;
    }

    public ConditionEvaluationResult EvaluateWithDetails(Entity subject, Resource resource, string action, Dictionary<string, object> context)
    {
        var result = new ConditionEvaluationResult
        {
            ConditionId = Id,
            ConditionName = Name,
            IsMet = Evaluate(subject, resource, action, context)
        };

        result.Reason = result.IsMet 
            ? $"Condition '{Name}' is satisfied" 
            : $"Condition '{Name}' is not satisfied";

        return result;
    }

    private bool EvaluateSubjectCondition(Entity? subject)
    {
        if (subject == null)
            return false;

        var value = GetPropertyValue(subject, Property);
        return CompareValues(value, Operator, Value);
    }

    private bool EvaluateResourceCondition(Resource resource)
    {
        var value = GetPropertyValue(resource, Property);
        return CompareValues(value, Operator, Value);
    }

    private bool EvaluateActionCondition(string action)
    {
        return CompareValues(action, Operator, Value);
    }

    private bool EvaluateContextCondition(Dictionary<string, object> context)
    {
        if (!context.ContainsKey(Property))
            return false;

        return CompareValues(context[Property], Operator, Value);
    }

    private bool EvaluateTimeCondition()
    {
        var now = DateTime.UtcNow;
        return Property switch
        {
            "hour" => CompareValues(now.Hour, Operator, Value),
            "dayOfWeek" => CompareValues(now.DayOfWeek.ToString(), Operator, Value),
            "date" => CompareValues(now.Date, Operator, Value),
            _ => false
        };
    }

    private object? GetPropertyValue(object obj, string propertyPath)
    {
        var parts = propertyPath.Split('.');
        object? current = obj;

        foreach (var part in parts)
        {
            if (current == null)
                return null;

            var property = current.GetType().GetProperty(part);
            if (property == null)
                return null;

            current = property.GetValue(current);
        }

        return current;
    }

    private bool CompareValues(object? actual, string op, object? expected)
    {
        if (actual == null || expected == null)
            return false;

        try
        {
            return op switch
            {
                "==" => actual.Equals(expected),
                "!=" => !actual.Equals(expected),
                ">" => Comparer<object>.Default.Compare(actual, expected) > 0,
                "<" => Comparer<object>.Default.Compare(actual, expected) < 0,
                ">=" => Comparer<object>.Default.Compare(actual, expected) >= 0,
                "<=" => Comparer<object>.Default.Compare(actual, expected) <= 0,
                "contains" => actual.ToString()?.Contains(expected.ToString() ?? "") ?? false,
                "startsWith" => actual.ToString()?.StartsWith(expected.ToString() ?? "") ?? false,
                "endsWith" => actual.ToString()?.EndsWith(expected.ToString() ?? "") ?? false,
                "matches" => Regex.IsMatch(actual.ToString() ?? "", expected.ToString() ?? ""),
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }
}

public enum ConditionType
{
    Subject,   // Condition on subject properties
    Resource,  // Condition on resource properties
    Action,    // Condition on action
    Context,   // Condition on context values
    Time       // Time-based condition
}

public class AuthorizationRule
{
    public int Id { get; set; }
    public int AuthorizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RuleType Type { get; set; }
    public string Expression { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    public RuleEffect Effect { get; set; }
    public List<RuleTarget> Targets { get; set; } = new();
    public List<RuleCondition> Conditions { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    public bool? Evaluate(Entity subject, Resource resource, string action, Dictionary<string, object> context)
    {
        if (!IsActive)
            return null;

        // Check if rule applies to target
        if (Targets.Any() && !Targets.Any(t => t.Matches(subject, resource, action)))
            return null;

        // Evaluate conditions
        if (!Conditions.All(c => c.Evaluate(subject, resource, action, context)))
            return null;

        // Evaluate rule expression
        var result = Type switch
        {
            RuleType.Permission => EvaluatePermissionRule(subject, resource, action),
            RuleType.Attribute => EvaluateAttributeRule(subject, resource, context),
            RuleType.Relationship => EvaluateRelationshipRule(subject, resource),
            RuleType.Custom => EvaluateCustomRule(subject, resource, action, context),
            _ => false
        };

        return Effect switch
        {
            RuleEffect.Permit => result,
            RuleEffect.Deny => !result,
            RuleEffect.Indeterminate => null,
            _ => null
        };
    }

    public RuleEvaluationResult EvaluateWithDetails(Entity subject, Resource resource, string action, Dictionary<string, object> context)
    {
        var result = new RuleEvaluationResult
        {
            RuleId = Id,
            RuleName = Name,
            Decision = null,
            EvaluatedAt = DateTime.UtcNow
        };

        if (!IsActive)
        {
            result.Reason = "Rule is not active";
            return result;
        }

        if (Targets.Any())
        {
            var matchingTarget = Targets.FirstOrDefault(t => t.Matches(subject, resource, action));
            if (matchingTarget == null)
            {
                result.Reason = "No matching target";
                return result;
            }
            result.MatchedTarget = matchingTarget;
        }

        foreach (var condition in Conditions)
        {
            if (!condition.Evaluate(subject, resource, action, context))
            {
                result.Reason = $"Condition '{condition.Name}' not met";
                return result;
            }
        }

        var evaluation = Evaluate(subject, resource, action, context);
        result.Decision = evaluation;
        result.Reason = evaluation.HasValue 
            ? (evaluation.Value ? "Rule permits access" : "Rule denies access")
            : "Rule is indeterminate";

        return result;
    }

    private bool EvaluatePermissionRule(Entity subject, Resource resource, string action)
    {
        // Check if subject has required permission by examining permissions directly
        if (!Enum.TryParse<HttpVerb>(action, out var httpVerb))
            return false;

        var hasPermission = subject.Permissions.Any(p => 
            p.Uri == resource.Uri && 
            p.HttpVerb == httpVerb && 
            p.Grant && 
            !p.Deny);

        return hasPermission;
    }

    private bool EvaluateAttributeRule(Entity subject, Resource resource, Dictionary<string, object> context)
    {
        // Evaluate based on subject/resource attributes
        try
        {
            var evaluator = new SimpleExpressionEvaluator(Expression);
            return evaluator.Evaluate(subject, resource, "", context);
        }
        catch
        {
            return false;
        }
    }

    private bool EvaluateRelationshipRule(Entity subject, Resource resource)
    {
        // Check relationships between subject and resource
        if (Expression.Contains("owner"))
        {
            // Check if subject owns the resource
            return subject.Id == resource.Id; // Simplified
        }
        
        if (Expression.Contains("member"))
        {
            // Check if subject is member of resource (resources don't have group membership in this model)
            // This would require additional domain logic to determine resource-group relationships
            return false; // Simplified - resources are not groups
        }

        return false;
    }

    private bool EvaluateCustomRule(Entity subject, Resource resource, string action, Dictionary<string, object> context)
    {
        // Custom rule evaluation
        if (Metadata.ContainsKey("customEvaluator"))
        {
            var evaluatorName = Metadata["customEvaluator"].ToString();
            // In production, invoke custom evaluator
            return evaluatorName == "allow";
        }
        return false;
    }
}

public enum RuleType
{
    Permission,    // Based on permissions
    Attribute,     // Based on attributes
    Relationship,  // Based on relationships
    Custom         // Custom logic
}

public enum RuleEffect
{
    Permit,        // Rule permits access
    Deny,          // Rule denies access
    Indeterminate  // Rule cannot determine
}

public class RuleTarget
{
    public int Id { get; set; }
    public TargetType Type { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public Dictionary<string, object> Attributes { get; set; } = new();
    
    public bool Matches(Entity subject, Resource resource, string action)
    {
        return Type switch
        {
            TargetType.Subject => MatchesSubject(subject),
            TargetType.Resource => MatchesResource(resource),
            TargetType.Action => MatchesAction(action),
            TargetType.Any => true,
            _ => false
        };
    }

    private bool MatchesSubject(Entity subject)
    {
        if (string.IsNullOrEmpty(Pattern) || Pattern == "*")
            return true;

        // Match by type
        if (Pattern.StartsWith("type:"))
        {
            var expectedType = Pattern.Substring(5);
            var actualType = subject.GetType().Name;
            return actualType.Equals(expectedType, StringComparison.OrdinalIgnoreCase);
        }

        // Match by pattern
        return Regex.IsMatch(subject.Name, Pattern, RegexOptions.IgnoreCase);
    }

    private bool MatchesResource(Resource resource)
    {
        if (string.IsNullOrEmpty(Pattern) || Pattern == "*")
            return true;

        return resource.MatchesUri(Pattern);
    }

    private bool MatchesAction(string action)
    {
        if (string.IsNullOrEmpty(Pattern) || Pattern == "*")
            return true;

        return Regex.IsMatch(action, Pattern, RegexOptions.IgnoreCase);
    }
}

public enum TargetType
{
    Subject,
    Resource,
    Action,
    Any
}

public class RuleCondition
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    
    public bool Evaluate(Entity subject, Resource resource, string action, Dictionary<string, object> context)
    {
        try
        {
            var evaluator = new SimpleExpressionEvaluator(Expression);
            return evaluator.Evaluate(subject, resource, action, context);
        }
        catch
        {
            return false;
        }
    }
}

// Supporting classes for evaluation results
public class AuthorizationResult
{
    public bool IsAuthorized { get; set; }
    public int AuthorizationId { get; set; }
    public Entity Subject { get; set; } = null!;
    public Resource Resource { get; set; } = null!;
    public string Action { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public PolicyEvaluationResult? PolicyResult { get; set; }
    public List<RuleEvaluationResult> RuleResults { get; set; } = new();
    public AuthorizationRule? AppliedRule { get; set; }
    public DateTime EvaluatedAt { get; set; }
}

public class PolicyEvaluationResult
{
    public int PolicyId { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public bool IsAllowed { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<ConditionEvaluationResult> ConditionResults { get; set; } = new();
    public DateTime EvaluatedAt { get; set; }
}

public class ConditionEvaluationResult
{
    public int ConditionId { get; set; }
    public string ConditionName { get; set; } = string.Empty;
    public bool IsMet { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class RuleEvaluationResult
{
    public int RuleId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public bool? Decision { get; set; }
    public string Reason { get; set; } = string.Empty;
    public RuleTarget? MatchedTarget { get; set; }
    public DateTime EvaluatedAt { get; set; }
}

// Simple expression evaluator
public class SimpleExpressionEvaluator
{
    private readonly string _expression;
    
    public SimpleExpressionEvaluator(string expression)
    {
        _expression = expression;
    }
    
    public bool Evaluate(Entity? subject, Resource? resource, string action, Dictionary<string, object> context)
    {
        // Simplified expression evaluation
        // In production, use expression trees or scripting engine
        
        var expression = _expression.ToLower();
        
        // Replace variables
        if (subject != null)
        {
            expression = expression.Replace("subject.id", subject.Id.ToString());
            expression = expression.Replace("subject.name", subject.Name.ToLower());
            expression = expression.Replace("subject.type", subject.GetType().Name.ToLower());
        }
        
        if (resource != null)
        {
            expression = expression.Replace("resource.uri", resource.Uri.ToLower());
            expression = expression.Replace("resource.type", resource.ResourceType?.ToLower() ?? "");
        }
        
        expression = expression.Replace("action", action.ToLower());
        
        foreach (var kvp in context)
        {
            expression = expression.Replace($"context.{kvp.Key.ToLower()}", kvp.Value?.ToString()?.ToLower() ?? "");
        }
        
        // Simple evaluation
        if (expression.Contains("=="))
        {
            var parts = expression.Split("==");
            if (parts.Length == 2)
            {
                return parts[0].Trim() == parts[1].Trim();
            }
        }
        
        if (expression.Contains("!="))
        {
            var parts = expression.Split("!=");
            if (parts.Length == 2)
            {
                return parts[0].Trim() != parts[1].Trim();
            }
        }
        
        // Default to true for "allow" or false for "deny"
        return expression.Contains("allow") || expression.Contains("true");
    }
}
