using System.Collections.ObjectModel;
using ACS.Service.Delegates.Normalizers;

namespace ACS.Service.Domain;

public abstract class Entity
{
    public int Id { get; set; }
    public string Name { get; set; }
    protected List<Entity> Children { get; set; } = new List<Entity>();
    protected List<Entity> Parents { get; set; } = new List<Entity>();
    protected List<Permission> Permissions { get; set; } = new List<Permission>();

    public void AddPermission(Permission permission)
    {
        Permissions.Add(permission);
        AddPermissionToEntity.Execute(permission, Id);
    }

    public void RemovePermission(Permission permission)
    {
        if (Permissions.Remove(permission))
        {
            RemovePermissionFromEntity.Execute(permission, Id);
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

    private List<Permission> AggregatePermissions()
    {
        var aggregatedPermissions = new List<Permission>();
        var queue = new Queue<Entity>();
        queue.Enqueue(this);

        while (queue.Count > 0)
        {
            var currentEntity = queue.Dequeue();
            aggregatedPermissions.AddRange(currentEntity.Permissions);

            foreach (var child in currentEntity.Children)
            {
                queue.Enqueue(child);
            }
        }

        return ResolvePermissionConflicts(aggregatedPermissions);
    }

    private List<Permission> ResolvePermissionConflicts(List<Permission> permissions)
    {
        var resolvedPermissions = new Dictionary<string, Permission>();

        foreach (var permission in permissions)
        {
            var key = $"{permission.Uri}:{permission.HttpVerb}";

            if (!resolvedPermissions.ContainsKey(key))
            {
                resolvedPermissions[key] = permission;
            }
            else
            {
                var existingPermission = resolvedPermissions[key];

                if (permission.Deny)
                {
                    existingPermission.Deny = true;
                    existingPermission.Grant = false;
                }
                else if (permission.Grant && !existingPermission.Deny)
                {
                    existingPermission.Grant = true;
                }
            }
        }

        return resolvedPermissions.Values.ToList();
    }

    public bool HasPermission(string uri, HttpVerb httpVerb)
    {
        var permissions = AggregatePermissions();
        var permission = permissions.FirstOrDefault(p => p.Uri == uri && p.HttpVerb == httpVerb);

        return permission != null && permission.Grant && !permission.Deny;
    }
}