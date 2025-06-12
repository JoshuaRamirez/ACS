namespace ACS.Service.Services;

using ACS.Service.Domain;

public class UserService : IUserService
{
    private readonly List<User> _users = new();
    private int _nextId = 1;

    public IEnumerable<User> GetAll() => _users;

    public User? GetById(int id) => _users.FirstOrDefault(u => u.Id == id);

    public User Add(User user)
    {
        user.Id = _nextId++;
        _users.Add(user);
        return user;
    }
}
