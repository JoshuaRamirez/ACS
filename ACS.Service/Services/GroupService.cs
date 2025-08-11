namespace ACS.Service.Services;

using ACS.Service.Domain;

public class GroupService : IGroupService
{
    private readonly List<Group> _groups = new();
    private int _nextId = 1;

    public IEnumerable<Group> GetAll() => _groups;

    public Group? GetById(int id) => _groups.FirstOrDefault(g => g.Id == id);

    public Group Add(Group group)
    {
        group.Id = _nextId++;
        _groups.Add(group);
        return group;
    }
}
