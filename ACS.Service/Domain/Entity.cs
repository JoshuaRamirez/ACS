using System.ComponentModel.DataAnnotations;
using ACS.Service.Domain.Validation;

namespace ACS.Service.Domain;

[UniqueEntityName(EntityType = typeof(Entity), CaseInsensitive = true)]
[MaxChildren(100)]
[AuditTrailBusinessRule(RequiresJustification = true, AuditableActions = new[] { "Create", "Delete", "ModifyPermissions" })]
public abstract class Entity
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(255, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    
    [ValidEntityRelationship("child")]
    public List<Entity> Children { get; set; } = new List<Entity>();
    
    [ValidEntityRelationship("parent")]
    public List<Entity> Parents { get; set; } = new List<Entity>();
    
    [ValidPermissionCombination]
    public List<Permission> Permissions { get; set; } = new List<Permission>();
    
    public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

    public void AddPermission(Permission permission)
    {
        Permissions.Add(permission);
        // EF Core change tracking will handle persistence automatically
    }

    public void AddPermission(Permission permission, string addedBy)
    {
        Permissions.Add(permission);
        // Audit tracking could be implemented here
        // EF Core change tracking will handle persistence automatically
    }

    public void RemovePermission(Permission permission)
    {
        Permissions.Remove(permission);
        // EF Core change tracking will handle persistence automatically
    }

    public void RemovePermission(string resource, string action, string removedBy)
    {
        var permission = Permissions.FirstOrDefault(p => p.Resource == resource && p.Action == action);
        if (permission != null)
        {
            Permissions.Remove(permission);
            // Audit tracking could be implemented here
        }
        // EF Core change tracking will handle persistence automatically
    }

    [MaintainsInvariants("INV004", "INV006")]
    protected void AddChild(Entity child)
    {
        if (child == null)
            throw new ArgumentNullException(nameof(child));
        if (child.Id == this.Id && this.Id > 0)
            throw new DomainInvariantViolationException("INV004", "Entity cannot be its own child");
            
        Children.Add(child);
        child.Parents.Add(this);
        // EF Core change tracking will handle persistence automatically
    }

    [MaintainsInvariants("INV006")]
    protected void RemoveChild(Entity child)
    {
        if (child == null)
            return;
            
        Children.Remove(child);
        child.Parents.Remove(this);
        // EF Core change tracking will handle persistence automatically
    }

    // Note: Permission evaluation is now handled by IPermissionEvaluationService
    // This method is deprecated and should not be used for production logic
    [Obsolete("Use IPermissionEvaluationService.HasPermissionAsync instead")]
    public bool HasPermission(string uri, HttpVerb httpVerb)
    {
        // Simple in-memory check for compatibility
        var permission = Permissions.FirstOrDefault(p => p.Uri == uri && p.HttpVerb == httpVerb);
        return permission != null && permission.Grant && !permission.Deny;
    }
}