using ACS.Service.Domain;

namespace ACS.Service.Services;

public interface IUserService
{
    // Async methods (preferred)
    Task<IEnumerable<User>> GetAllAsync();
    Task<User?> GetByIdAsync(int id);
    Task<User> AddAsync(User user, string createdBy);
    Task<User> UpdateAsync(User user);
    Task DeleteAsync(int id);
    
    // Legacy sync methods for backward compatibility (deprecated)
    IEnumerable<User> GetAll();
    User? GetById(int id);
    User Add(User user);
}
