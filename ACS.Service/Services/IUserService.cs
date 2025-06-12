namespace ACS.Service.Services;

using ACS.Service.Domain;

public interface IUserService
{
    IEnumerable<User> GetAll();
    User? GetById(int id);
    User Add(User user);
}
