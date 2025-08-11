namespace ACS.Service.Services;

using ACS.Service.Domain;

public interface IRoleService
{
    IEnumerable<Role> GetAll();
    Role? GetById(int id);
    Role Add(Role role);
}
