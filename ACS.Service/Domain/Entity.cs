namespace ACS.Service.Domain;

public abstract class Entity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<Entity> Children { get; set; } = new List<Entity>();
    public List<Entity> Parents { get; set; } = new List<Entity>();
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

    protected void AddChild(Entity child)
    {
        Children.Add(child);
        child.Parents.Add(this);
    }

    protected void RemoveChild(Entity child)
    {
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