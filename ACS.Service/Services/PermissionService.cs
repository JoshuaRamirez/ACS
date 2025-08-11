namespace ACS.Service.Services;

using ACS.Service.Domain;

public class PermissionService : IPermissionService
{
    private readonly List<Permission> _permissions = new();
    private int _nextId = 1;

    public IEnumerable<Permission> GetAll() => _permissions;

    public Permission? GetById(int id) => _permissions.FirstOrDefault(p => p.Id == id);

    public Permission Add(Permission permission)
    {
        permission.Id = _nextId++;
        _permissions.Add(permission);
        return permission;
    }
}
