using ACS.Service.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace ACS.Service.Data.Repositories;

/// <summary>
/// Repository implementation for Role-specific operations
/// </summary>
public class RoleRepository : Repository<Role>, IRoleRepository
{
    public RoleRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Role?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(r => r.Entity)
            .FirstOrDefaultAsync(r => r.Name == name, cancellationToken);
    }

    public async Task<IEnumerable<Role>> FindRolesForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(r => r.Entity)
            .Include(r => r.UserRoles)
            .Where(r => r.UserRoles.Any(ur => ur.UserId == userId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Role>> FindRolesForGroupAsync(int groupId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(r => r.Entity)
            .Include(r => r.GroupRoles)
            .Where(r => r.GroupRoles.Any(gr => gr.GroupId == groupId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Role>> FindRolesByPermissionAsync(string resourceUri, string verb, CancellationToken cancellationToken = default)
    {
        return await (from r in _dbSet
                     join e in _context.Entities on r.EntityId equals e.Id
                     join ps in _context.EntityPermissions on e.Id equals ps.EntityId
                     join ua in _context.UriAccesses on ps.Id equals ua.PermissionSchemeId
                     join res in _context.Resources on ua.ResourceId equals res.Id
                     join vt in _context.VerbTypes on ua.VerbTypeId equals vt.Id
                     where res.Uri == resourceUri && vt.VerbName == verb && ua.Grant
                     select r)
                    .Include(r => r.Entity)
                    .Distinct()
                    .ToListAsync(cancellationToken);
    }

    public async Task<Role?> GetRoleWithPermissionsAsync(int roleId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(r => r.Entity)
            .Include(r => r.UserRoles)
                .ThenInclude(ur => ur.User)
            .Include(r => r.GroupRoles)
                .ThenInclude(gr => gr.Group)
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);
    }

    public async Task<IEnumerable<RoleWithPermissionCount>> GetRolesWithPermissionCountsAsync(Expression<Func<Role, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        var rolesQuery = _dbSet.Include(r => r.Entity);
        
        if (predicate != null)
            rolesQuery = rolesQuery.Where(predicate);

        var roles = await rolesQuery.ToListAsync(cancellationToken);

        var roleCounts = await (from r in roles
                              join e in _context.Entities on r.EntityId equals e.Id
                              join ps in _context.EntityPermissions on e.Id equals ps.EntityId into permissions
                              from p in permissions.DefaultIfEmpty()
                              join ua in _context.UriAccesses on p.Id equals ua.PermissionSchemeId into accesses
                              from a in accesses.DefaultIfEmpty()
                              join res in _context.Resources on a.ResourceId equals res.Id into resources
                              from resource in resources.DefaultIfEmpty()
                              group new { r, a, resource } by r.Id into g
                              select new
                              {
                                  RoleId = g.Key,
                                  PermissionCount = g.Count(x => x.a != null),
                                  ResourceCount = g.Where(x => x.resource != null).Select(x => x.resource.Id).Distinct().Count(),
                                  ResourceUris = g.Where(x => x.resource != null).Select(x => x.resource.Uri).Distinct()
                              }).ToListAsync(cancellationToken);

        return roles.Select(role =>
        {
            var counts = roleCounts.FirstOrDefault(c => c.RoleId == role.Id);
            return new RoleWithPermissionCount
            {
                Role = role,
                PermissionCount = counts?.PermissionCount ?? 0,
                ResourceCount = counts?.ResourceCount ?? 0,
                ResourceTypes = counts?.ResourceUris.Select(uri => GetResourceType(uri)).Distinct() ?? new List<string>()
            };
        });
    }

    public async Task<IEnumerable<RoleWithAssignmentCount>> GetRolesWithAssignmentCountsAsync(Expression<Func<Role, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        var rolesQuery = _dbSet.Include(r => r.Entity);
        
        if (predicate != null)
            rolesQuery = rolesQuery.Where(predicate);

        var roles = await rolesQuery.ToListAsync(cancellationToken);

        var assignmentCounts = await (from r in roles
                                    join ur in _context.UserRoles on r.Id equals ur.RoleId into userRoles
                                    join gr in _context.GroupRoles on r.Id equals gr.RoleId into groupRoles
                                    select new
                                    {
                                        RoleId = r.Id,
                                        UserCount = userRoles.Count(),
                                        GroupCount = groupRoles.Count()
                                    }).ToListAsync(cancellationToken);

        return roles.Select(role =>
        {
            var counts = assignmentCounts.FirstOrDefault(c => c.RoleId == role.Id);
            return new RoleWithAssignmentCount
            {
                Role = role,
                UserAssignmentCount = counts?.UserCount ?? 0,
                GroupAssignmentCount = counts?.GroupCount ?? 0
            };
        });
    }

    public async Task<IEnumerable<Role>> FindUnusedRolesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(r => r.Entity)
            .Where(r => !r.UserRoles.Any() && !r.GroupRoles.Any())
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Role>> FindRolesByResourcePatternAsync(string resourcePattern, CancellationToken cancellationToken = default)
    {
        return await (from r in _dbSet
                     join e in _context.Entities on r.EntityId equals e.Id
                     join ps in _context.EntityPermissions on e.Id equals ps.EntityId
                     join ua in _context.UriAccesses on ps.Id equals ua.PermissionSchemeId
                     join res in _context.Resources on ua.ResourceId equals res.Id
                     where EF.Functions.Like(res.Uri, resourcePattern)
                     select r)
                    .Include(r => r.Entity)
                    .Distinct()
                    .ToListAsync(cancellationToken);
    }

    public async Task<RoleStatistics> GetRoleStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var totalRoles = await _dbSet.CountAsync(cancellationToken);
        var unusedRoles = await _dbSet.CountAsync(r => !r.UserRoles.Any() && !r.GroupRoles.Any(), cancellationToken);
        var rolesWithUsers = await _dbSet.CountAsync(r => r.UserRoles.Any(), cancellationToken);
        var rolesWithGroups = await _dbSet.CountAsync(r => r.GroupRoles.Any(), cancellationToken);
        var totalUserRoleAssignments = await _context.UserRoles.CountAsync(cancellationToken);
        var totalGroupRoleAssignments = await _context.GroupRoles.CountAsync(cancellationToken);

        // Resource type distribution
        var rolesByResourceType = await (from r in _dbSet
                                       join e in _context.Entities on r.EntityId equals e.Id
                                       join ps in _context.EntityPermissions on e.Id equals ps.EntityId
                                       join ua in _context.UriAccesses on ps.Id equals ua.PermissionSchemeId
                                       join res in _context.Resources on ua.ResourceId equals res.Id
                                       group r by GetResourceType(res.Uri) into g
                                       select new { ResourceType = g.Key, Count = g.Distinct().Count() })
                                      .ToDictionaryAsync(x => x.ResourceType, x => x.Count, cancellationToken);

        // Permission count distribution
        var rolesByPermissionCount = await (from r in _dbSet
                                          join e in _context.Entities on r.EntityId equals e.Id
                                          join ps in _context.EntityPermissions on e.Id equals ps.EntityId into permissions
                                          from p in permissions.DefaultIfEmpty()
                                          join ua in _context.UriAccesses on p.Id equals ua.PermissionSchemeId into accesses
                                          from a in accesses.DefaultIfEmpty()
                                          group a by r.Id into g
                                          let permissionCount = g.Count(x => x != null)
                                          group g.Key by GetPermissionRange(permissionCount) into range
                                          select new { Range = range.Key, Count = range.Count() })
                                         .ToDictionaryAsync(x => x.Range, x => x.Count, cancellationToken);

        return new RoleStatistics
        {
            TotalRoles = totalRoles,
            UnusedRoles = unusedRoles,
            RolesWithUsers = rolesWithUsers,
            RolesWithGroups = rolesWithGroups,
            TotalUserRoleAssignments = totalUserRoleAssignments,
            TotalGroupRoleAssignments = totalGroupRoleAssignments,
            RolesByResourceType = rolesByResourceType,
            RolesByPermissionCount = rolesByPermissionCount
        };
    }

    public async Task<bool> RoleNameExistsAsync(string name, int? excludeRoleId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(r => r.Name == name);
        
        if (excludeRoleId.HasValue)
            query = query.Where(r => r.Id != excludeRoleId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task AssignRoleToUserAsync(int userId, int roleId, string createdBy, CancellationToken cancellationToken = default)
    {
        var existing = await _context.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId, cancellationToken);

        if (existing == null)
        {
            var userRole = new UserRole
            {
                UserId = userId,
                RoleId = roleId,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            await _context.UserRoles.AddAsync(userRole, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RemoveRoleFromUserAsync(int userId, int roleId, CancellationToken cancellationToken = default)
    {
        var userRole = await _context.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId, cancellationToken);

        if (userRole != null)
        {
            _context.UserRoles.Remove(userRole);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task AssignRoleToGroupAsync(int groupId, int roleId, string createdBy, CancellationToken cancellationToken = default)
    {
        var existing = await _context.GroupRoles
            .FirstOrDefaultAsync(gr => gr.GroupId == groupId && gr.RoleId == roleId, cancellationToken);

        if (existing == null)
        {
            var groupRole = new GroupRole
            {
                GroupId = groupId,
                RoleId = roleId,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            await _context.GroupRoles.AddAsync(groupRole, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RemoveRoleFromGroupAsync(int groupId, int roleId, CancellationToken cancellationToken = default)
    {
        var groupRole = await _context.GroupRoles
            .FirstOrDefaultAsync(gr => gr.GroupId == groupId && gr.RoleId == roleId, cancellationToken);

        if (groupRole != null)
        {
            _context.GroupRoles.Remove(groupRole);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IEnumerable<Role>> GetEffectiveRolesForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        // Direct role assignments
        var directRoles = await _dbSet
            .Include(r => r.Entity)
            .Include(r => r.UserRoles)
            .Where(r => r.UserRoles.Any(ur => ur.UserId == userId))
            .ToListAsync(cancellationToken);

        // Roles inherited from groups
        var inheritedRoles = await (from r in _dbSet
                                  join gr in _context.GroupRoles on r.Id equals gr.RoleId
                                  join ug in _context.UserGroups on gr.GroupId equals ug.GroupId
                                  where ug.UserId == userId
                                  select r)
                                 .Include(r => r.Entity)
                                 .Distinct()
                                 .ToListAsync(cancellationToken);

        return directRoles.Union(inheritedRoles).Distinct();
    }

    public async Task<IEnumerable<RoleConflict>> FindRoleConflictsAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _dbSet.Include(r => r.Entity).ToListAsync(cancellationToken);
        var conflicts = new List<RoleConflict>();

        for (int i = 0; i < roles.Count; i++)
        {
            for (int j = i + 1; j < roles.Count; j++)
            {
                var role1 = roles[i];
                var role2 = roles[j];

                var conflictingResources = await FindConflictingResources(role1.Id, role2.Id, cancellationToken);
                
                if (conflictingResources.Any())
                {
                    conflicts.Add(new RoleConflict
                    {
                        Role1 = role1,
                        Role2 = role2,
                        ConflictingResources = conflictingResources,
                        ConflictType = DetermineConflictType(conflictingResources.Count())
                    });
                }
            }
        }

        return conflicts;
    }

    public async Task<IEnumerable<RoleHierarchyNode>> GetRoleHierarchyAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _dbSet.Include(r => r.Entity).ToListAsync(cancellationToken);
        var hierarchyNodes = new List<RoleHierarchyNode>();

        foreach (var role in roles)
        {
            var includedRoles = new List<RoleHierarchyNode>();
            double maxOverlap = 0;

            foreach (var otherRole in roles.Where(r => r.Id != role.Id))
            {
                var overlap = await CalculatePermissionOverlap(role.Id, otherRole.Id, cancellationToken);
                if (overlap > 0.7) // 70% overlap threshold
                {
                    includedRoles.Add(new RoleHierarchyNode 
                    { 
                        Role = otherRole, 
                        PermissionOverlap = overlap 
                    });
                    maxOverlap = Math.Max(maxOverlap, overlap);
                }
            }

            hierarchyNodes.Add(new RoleHierarchyNode
            {
                Role = role,
                IncludedRoles = includedRoles.OrderByDescending(n => n.PermissionOverlap),
                PermissionOverlap = maxOverlap
            });
        }

        return hierarchyNodes.OrderByDescending(n => n.PermissionOverlap);
    }

    private async Task<IEnumerable<string>> FindConflictingResources(int role1Id, int role2Id, CancellationToken cancellationToken)
    {
        return await (from ua1 in _context.UriAccesses
                     join ps1 in _context.EntityPermissions on ua1.PermissionSchemeId equals ps1.Id
                     join e1 in _context.Entities on ps1.EntityId equals e1.Id
                     join r1 in _context.Roles on e1.Id equals r1.EntityId
                     join ua2 in _context.UriAccesses on ua1.ResourceId equals ua2.ResourceId
                     join ps2 in _context.EntityPermissions on ua2.PermissionSchemeId equals ps2.Id
                     join e2 in _context.Entities on ps2.EntityId equals e2.Id
                     join r2 in _context.Roles on e2.Id equals r2.EntityId
                     join res in _context.Resources on ua1.ResourceId equals res.Id
                     where r1.Id == role1Id && r2.Id == role2Id 
                           && ua1.VerbTypeId == ua2.VerbTypeId
                           && ((ua1.Grant && ua2.Deny) || (ua1.Deny && ua2.Grant))
                     select res.Uri)
                    .Distinct()
                    .ToListAsync(cancellationToken);
    }

    private async Task<double> CalculatePermissionOverlap(int role1Id, int role2Id, CancellationToken cancellationToken)
    {
        var role1Permissions = await GetRolePermissionSignature(role1Id, cancellationToken);
        var role2Permissions = await GetRolePermissionSignature(role2Id, cancellationToken);

        if (!role1Permissions.Any() || !role2Permissions.Any())
            return 0;

        var intersection = role1Permissions.Intersect(role2Permissions).Count();
        var union = role1Permissions.Union(role2Permissions).Count();

        return union == 0 ? 0 : (double)intersection / union;
    }

    private async Task<HashSet<string>> GetRolePermissionSignature(int roleId, CancellationToken cancellationToken)
    {
        return (await (from ua in _context.UriAccesses
                      join ps in _context.EntityPermissions on ua.PermissionSchemeId equals ps.Id
                      join e in _context.Entities on ps.EntityId equals e.Id
                      join r in _context.Roles on e.Id equals r.EntityId
                      join res in _context.Resources on ua.ResourceId equals res.Id
                      join vt in _context.VerbTypes on ua.VerbTypeId equals vt.Id
                      where r.Id == roleId && ua.Grant
                      select $"{res.Uri}:{vt.VerbName}")
                     .ToListAsync(cancellationToken))
                .ToHashSet();
    }

    private static string GetResourceType(string uri)
    {
        if (string.IsNullOrEmpty(uri)) return "Unknown";
        
        var segments = uri.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.FirstOrDefault() ?? "Unknown";
    }

    private static string GetPermissionRange(int count)
    {
        return count switch
        {
            0 => "No Permissions",
            <= 5 => "1-5 Permissions",
            <= 10 => "6-10 Permissions",
            <= 25 => "11-25 Permissions",
            <= 50 => "26-50 Permissions",
            _ => "50+ Permissions"
        };
    }

    private static string DetermineConflictType(int conflictCount)
    {
        return conflictCount switch
        {
            <= 3 => "Minor",
            <= 10 => "Moderate",
            _ => "Major"
        };
    }
}