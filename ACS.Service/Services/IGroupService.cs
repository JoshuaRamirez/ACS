namespace ACS.Service.Services;

using ACS.Service.Domain;

public interface IGroupService
{
    IEnumerable<Group> GetAll();
    Group? GetById(int id);
    Group Add(Group group);
}
