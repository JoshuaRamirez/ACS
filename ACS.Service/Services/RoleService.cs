namespace ACS.Service.Services;

using ACS.Service.Domain;

public class RoleService : IRoleService
{
    private readonly List<Role> _roles = new();
    private int _nextId = 1;

    public IEnumerable<Role> GetAll() => _roles;

    public Role? GetById(int id) => _roles.FirstOrDefault(r => r.Id == id);

    public Role Add(Role role)
    {
        role.Id = _nextId++;
        _roles.Add(role);
        return role;
    }
}
