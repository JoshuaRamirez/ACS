namespace ACS.Service.Services;

using ACS.Service.Domain;

public interface IPermissionService
{
    IEnumerable<Permission> GetAll();
    Permission? GetById(int id);
    Permission Add(Permission permission);
}
