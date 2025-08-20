using ACS.Service.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace ACS.Service.Data.Repositories;

/// <summary>
/// Repository implementation for User-specific operations
/// </summary>
public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(ApplicationDbContext context) : base(context) { }

    public async Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(u => u.Entity)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<IEnumerable<User>> FindByRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(u => u.Entity)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Where(u => u.UserRoles.Any(ur => ur.Role.Name == roleName))
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<User>> FindByGroupAsync(string groupName, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(u => u.Entity)
            .Include(u => u.UserGroups)
                .ThenInclude(ug => ug.Group)
            .Where(u => u.UserGroups.Any(ug => ug.Group.Name == groupName))
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<User>> FindActiveUsersAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(u => u.Entity)
            .Where(u => u.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<User>> FindUsersWithExcessiveFailedLoginsAsync(int threshold, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(u => u.Entity)
            .Where(u => u.FailedLoginAttempts >= threshold)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<User>> FindUsersCreatedBetweenAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(u => u.Entity)
            .Where(u => u.CreatedAt >= startDate && u.CreatedAt <= endDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<User?> GetUserWithGroupsAndRolesAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(u => u.Entity)
            .Include(u => u.UserGroups)
                .ThenInclude(ug => ug.Group)
                    .ThenInclude(g => g.Entity)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.Entity)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    public async Task<IEnumerable<User>> GetUsersWithSecurityContextAsync(Expression<Func<User, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        IQueryable<User> query = _dbSet
            .Include(u => u.Entity)
            .Include(u => u.UserGroups)
                .ThenInclude(ug => ug.Group)
                    .ThenInclude(g => g.Entity)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.Entity)
            .Include(u => u.UserGroups)
                .ThenInclude(ug => ug.Group)
                    .ThenInclude(g => g.GroupRoles)
                        .ThenInclude(gr => gr.Role)
                            .ThenInclude(r => r.Entity);

        if (predicate != null)
            query = query.Where(predicate);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(string email, int? excludeUserId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(u => u.Email == email);
        
        if (excludeUserId.HasValue)
            query = query.Where(u => u.Id != excludeUserId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task UpdateFailedLoginAttemptsAsync(int userId, int attempts, CancellationToken cancellationToken = default)
    {
        var user = await _dbSet.FindAsync(new object[] { userId }, cancellationToken);
        if (user != null)
        {
            user.FailedLoginAttempts = attempts;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<UserStatistics> GetUserStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var totalUsers = await _dbSet.CountAsync(cancellationToken);
        var activeUsers = await _dbSet.CountAsync(u => u.IsActive, cancellationToken);
        var usersWithExcessiveFailedLogins = await _dbSet.CountAsync(u => u.FailedLoginAttempts >= 5, cancellationToken);
        var lastMonth = DateTime.UtcNow.AddMonths(-1);
        var usersCreatedLastMonth = await _dbSet.CountAsync(u => u.CreatedAt >= lastMonth, cancellationToken);
        var lastWeek = DateTime.UtcNow.AddDays(-7);
        var usersLoggedInLastWeek = await _dbSet.CountAsync(u => u.LastLoginAt >= lastWeek, cancellationToken);

        // Get users by role statistics
        var usersByRole = await (from u in _dbSet
                               join ur in _context.UserRoles on u.Id equals ur.UserId
                               join r in _context.Roles on ur.RoleId equals r.Id
                               group u by r.Name into g
                               select new { RoleName = g.Key, Count = g.Count() })
                              .ToDictionaryAsync(x => x.RoleName, x => x.Count, cancellationToken);

        // Get users by group statistics
        var usersByGroup = await (from u in _dbSet
                                join ug in _context.UserGroups on u.Id equals ug.UserId
                                join g in _context.Groups on ug.GroupId equals g.Id
                                group u by g.Name into grp
                                select new { GroupName = grp.Key, Count = grp.Count() })
                               .ToDictionaryAsync(x => x.GroupName, x => x.Count, cancellationToken);

        return new UserStatistics
        {
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,
            InactiveUsers = totalUsers - activeUsers,
            UsersWithExcessiveFailedLogins = usersWithExcessiveFailedLogins,
            UsersCreatedLastMonth = usersCreatedLastMonth,
            UsersLoggedInLastWeek = usersLoggedInLastWeek,
            UsersByRole = usersByRole,
            UsersByGroup = usersByGroup
        };
    }

    public async Task<IEnumerable<User>> FindUsersByPermissionAsync(string resourceUri, string verb, CancellationToken cancellationToken = default)
    {
        // Find users directly assigned permissions
        var directUsers = await (from u in _dbSet
                               join e in _context.Entities on u.EntityId equals e.Id
                               join ps in _context.EntityPermissions on e.Id equals ps.EntityId
                               join ua in _context.UriAccesses on ps.Id equals ua.PermissionSchemeId
                               join r in _context.Resources on ua.ResourceId equals r.Id
                               join vt in _context.VerbTypes on ua.VerbTypeId equals vt.Id
                               where r.Uri == resourceUri && vt.VerbName == verb && ua.Grant
                               select u)
                              .Include(u => u.Entity)
                              .Distinct()
                              .ToListAsync(cancellationToken);

        // Find users through role permissions
        var roleUsers = await (from u in _dbSet
                             join ur in _context.UserRoles on u.Id equals ur.UserId
                             join role in _context.Roles on ur.RoleId equals role.Id
                             join e in _context.Entities on role.EntityId equals e.Id
                             join ps in _context.EntityPermissions on e.Id equals ps.EntityId
                             join ua in _context.UriAccesses on ps.Id equals ua.PermissionSchemeId
                             join res in _context.Resources on ua.ResourceId equals res.Id
                             join vt in _context.VerbTypes on ua.VerbTypeId equals vt.Id
                             where res.Uri == resourceUri && vt.VerbName == verb && ua.Grant
                             select u)
                            .Include(u => u.Entity)
                            .Distinct()
                            .ToListAsync(cancellationToken);

        // Find users through group permissions
        var groupUsers = await (from u in _dbSet
                              join ug in _context.UserGroups on u.Id equals ug.UserId
                              join grp in _context.Groups on ug.GroupId equals grp.Id
                              join e in _context.Entities on grp.EntityId equals e.Id
                              join ps in _context.EntityPermissions on e.Id equals ps.EntityId
                              join ua in _context.UriAccesses on ps.Id equals ua.PermissionSchemeId
                              join res in _context.Resources on ua.ResourceId equals res.Id
                              join vt in _context.VerbTypes on ua.VerbTypeId equals vt.Id
                              where res.Uri == resourceUri && vt.VerbName == verb && ua.Grant
                              select u)
                             .Include(u => u.Entity)
                             .Distinct()
                             .ToListAsync(cancellationToken);

        // Combine and return unique users
        return directUsers.Union(roleUsers).Union(groupUsers).Distinct();
    }
}