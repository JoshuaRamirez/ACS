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
    [ValidEntityRelationship("child", MaxRelationships = 100)]
    public List<Entity> Children { get; set; } = new List<Entity>();
    
    [ValidEntityRelationship("parent", MaxRelationships = 10)]
    public List<Entity> Parents { get; set; } = new List<Entity>();
    
    [ValidPermissionCombination]
    public List<Permission> Permissions { get; set; } = new List<Permission>();

    public void AddPermission(Permission permission)
    {
        Permissions.Add(permission);
        // Note: Persistence should now be handled through proper service layer
        // AddPermissionToEntity.Execute(permission, Id);
    }

    public void RemovePermission(Permission permission)
    {
        if (Permissions.Remove(permission))
        {
            // Note: Persistence should now be handled through proper service layer
            // RemovePermissionFromEntity.Execute(permission, Id);
        }
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
    }

    [MaintainsInvariants("INV006")]
    protected void RemoveChild(Entity child)
    {
        if (child == null)
            return;
            
        Children.Remove(child);
        child.Parents.Remove(this);
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