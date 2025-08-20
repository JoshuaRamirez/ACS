using ACS.Service.Data.Models;
using System.Linq.Expressions;

namespace ACS.Service.Data.Repositories;

/// <summary>
/// Repository interface for User-specific operations
/// </summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>
    /// Find user by email address
    /// </summary>
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find users by role
    /// </summary>
    Task<IEnumerable<User>> FindByRoleAsync(string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find users by group
    /// </summary>
    Task<IEnumerable<User>> FindByGroupAsync(string groupName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find active users
    /// </summary>
    Task<IEnumerable<User>> FindActiveUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Find users with failed login attempts exceeding threshold
    /// </summary>
    Task<IEnumerable<User>> FindUsersWithExcessiveFailedLoginsAsync(int threshold, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find users created within date range
    /// </summary>
    Task<IEnumerable<User>> FindUsersCreatedBetweenAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get users with their groups and roles
    /// </summary>
    Task<User?> GetUserWithGroupsAndRolesAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get users with full security context
    /// </summary>
    Task<IEnumerable<User>> GetUsersWithSecurityContextAsync(Expression<Func<User, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if email exists (excluding specific user)
    /// </summary>
    Task<bool> EmailExistsAsync(string email, int? excludeUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update failed login attempts
    /// </summary>
    Task UpdateFailedLoginAttemptsAsync(int userId, int attempts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get user statistics
    /// </summary>
    Task<UserStatistics> GetUserStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Find users by entity permissions
    /// </summary>
    Task<IEnumerable<User>> FindUsersByPermissionAsync(string resourceUri, string verb, CancellationToken cancellationToken = default);
}

/// <summary>
/// User statistics model
/// </summary>
public class UserStatistics
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int InactiveUsers { get; set; }
    public int UsersWithExcessiveFailedLogins { get; set; }
    public int UsersCreatedLastMonth { get; set; }
    public int UsersLoggedInLastWeek { get; set; }
    public Dictionary<string, int> UsersByRole { get; set; } = new();
    public Dictionary<string, int> UsersByGroup { get; set; } = new();
}