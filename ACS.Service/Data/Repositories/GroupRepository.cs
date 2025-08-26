using ACS.Service.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace ACS.Service.Data.Repositories;

/// <summary>
/// Repository implementation for Group-specific operations
/// </summary>
public class GroupRepository : Repository<Group>, IGroupRepository
{
    public GroupRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Group?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(g => g.Entity)
            .FirstOrDefaultAsync(g => g.Name == name, cancellationToken);
    }

    public async Task<IEnumerable<Group>> FindGroupsForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(g => g.Entity)
            .Include(g => g.UserGroups)
            .Where(g => g.UserGroups.Any(ug => ug.UserId == userId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Group>> FindChildGroupsAsync(int parentGroupId, bool recursive = false, CancellationToken cancellationToken = default)
    {
        var directChildren = await _dbSet
            .Include(g => g.Entity)
            .Include(g => g.ParentGroupRelations)
            .Where(g => g.ParentGroupRelations.Any(gh => gh.ParentGroupId == parentGroupId))
            .ToListAsync(cancellationToken);

        if (!recursive)
            return directChildren;

        var allChildren = new List<Group>(directChildren);
        foreach (var child in directChildren)
        {
            var grandChildren = await FindChildGroupsAsync(child.Id, true, cancellationToken);
            allChildren.AddRange(grandChildren);
        }

        return allChildren.Distinct();
    }

    public async Task<IEnumerable<Group>> FindParentGroupsAsync(int childGroupId, bool recursive = false, CancellationToken cancellationToken = default)
    {
        var directParents = await _dbSet
            .Include(g => g.Entity)
            .Include(g => g.ChildGroupRelations)
            .Where(g => g.ChildGroupRelations.Any(gh => gh.ChildGroupId == childGroupId))
            .ToListAsync(cancellationToken);

        if (!recursive)
            return directParents;

        var allParents = new List<Group>(directParents);
        foreach (var parent in directParents)
        {
            var grandParents = await FindParentGroupsAsync(parent.Id, true, cancellationToken);
            allParents.AddRange(grandParents);
        }

        return allParents.Distinct();
    }

    public async Task<bool> IsAncestorOfAsync(int ancestorGroupId, int descendantGroupId, CancellationToken cancellationToken = default)
    {
        if (ancestorGroupId == descendantGroupId)
            return false;

        var parents = await FindParentGroupsAsync(descendantGroupId, true, cancellationToken);
        return parents.Any(p => p.Id == ancestorGroupId);
    }

    public async Task<bool> WouldCreateCycleAsync(int parentGroupId, int childGroupId, CancellationToken cancellationToken = default)
    {
        if (parentGroupId == childGroupId)
            return true;

        return await IsAncestorOfAsync(childGroupId, parentGroupId, cancellationToken);
    }

    public async Task<Group?> GetGroupWithUsersAndRolesAsync(int groupId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(g => g.Entity)
            .Include(g => g.UserGroups)
                .ThenInclude(ug => ug.User)
                    .ThenInclude(u => u.Entity)
            .Include(g => g.GroupRoles)
                .ThenInclude(gr => gr.Role)
                    .ThenInclude(r => r.Entity)
            .Include(g => g.ParentGroupRelations)
                .ThenInclude(gh => gh.ParentGroup)
            .Include(g => g.ChildGroupRelations)
                .ThenInclude(gh => gh.ChildGroup)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);
    }

    public async Task<IEnumerable<GroupHierarchyNode>> GetGroupHierarchyTreeAsync(int? rootGroupId = null, CancellationToken cancellationToken = default)
    {
        var allGroups = await _dbSet
            .Include(g => g.Entity)
            .Include(g => g.ParentGroupRelations)
            .Include(g => g.ChildGroupRelations)
            .ToListAsync(cancellationToken);

        var rootGroups = rootGroupId.HasValue
            ? allGroups.Where(g => g.Id == rootGroupId.Value)
            : allGroups.Where(g => !g.ParentGroupRelations.Any());

        return rootGroups.Select(root => BuildHierarchyNode(root, allGroups, 0, root.Name));
    }

    private GroupHierarchyNode BuildHierarchyNode(Group group, List<Group> allGroups, int level, string path)
    {
        var childGroups = allGroups
            .Where(g => group.ChildGroupRelations.Any(rel => rel.ChildGroupId == g.Id))
            .ToList();

        var children = childGroups.Select(child => 
            BuildHierarchyNode(child, allGroups, level + 1, $"{path}/{child.Name}"));

        return new GroupHierarchyNode
        {
            Group = group,
            Children = children,
            Level = level,
            Path = path
        };
    }

    public async Task<IEnumerable<Group>> FindGroupsByRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(g => g.Entity)
            .Include(g => g.GroupRoles)
                .ThenInclude(gr => gr.Role)
            .Where(g => g.GroupRoles.Any(gr => gr.Role.Name == roleName))
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<GroupWithMemberCount>> GetGroupsWithMemberCountsAsync(Expression<Func<Group, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        IQueryable<Group> query = _dbSet
            .Include(g => g.Entity)
            .Include(g => g.UserGroups)
            .Include(g => g.GroupRoles)
            .Include(g => g.ChildGroupRelations);

        if (predicate != null)
            query = query.Where(predicate);

        var groups = await query.ToListAsync(cancellationToken);

        return groups.Select(g => new GroupWithMemberCount
        {
            Group = g,
            UserCount = g.UserGroups.Count,
            RoleCount = g.GroupRoles.Count,
            ChildGroupCount = g.ChildGroupRelations.Count
        });
    }

    public async Task<IEnumerable<Group>> FindRootGroupsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(g => g.Entity)
            .Include(g => g.ParentGroupRelations)
            .Where(g => !g.ParentGroupRelations.Any())
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Group>> FindLeafGroupsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(g => g.Entity)
            .Include(g => g.ChildGroupRelations)
            .Where(g => !g.ChildGroupRelations.Any())
            .ToListAsync(cancellationToken);
    }

    public async Task<GroupStatistics> GetGroupStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var totalGroups = await _dbSet.CountAsync(cancellationToken);
        var rootGroups = await _dbSet.CountAsync(g => !g.ParentGroupRelations.Any(), cancellationToken);
        var leafGroups = await _dbSet.CountAsync(g => !g.ChildGroupRelations.Any(), cancellationToken);
        var totalUserMemberships = await _context.UserGroups.CountAsync(cancellationToken);
        var totalRoleAssignments = await _context.GroupRoles.CountAsync(cancellationToken);

        // Calculate max hierarchy depth using recursive CTE
        var maxDepth = await _context.Database
            .SqlQueryRaw<int>(@"
                WITH GroupDepth AS (
                    -- Base case: root groups (depth 0)
                    SELECT Id, 0 as Depth
                    FROM Groups g
                    WHERE NOT EXISTS (
                        SELECT 1 FROM GroupHierarchy gh 
                        WHERE gh.ChildGroupId = g.Id
                    )
                    
                    UNION ALL
                    
                    -- Recursive case: child groups
                    SELECT g.Id, gd.Depth + 1
                    FROM Groups g
                    INNER JOIN GroupHierarchy gh ON g.Id = gh.ChildGroupId
                    INNER JOIN GroupDepth gd ON gh.ParentGroupId = gd.Id
                )
                SELECT MAX(Depth) FROM GroupDepth")
            .FirstOrDefaultAsync(cancellationToken);

        return new GroupStatistics
        {
            TotalGroups = totalGroups,
            RootGroups = rootGroups,
            LeafGroups = leafGroups,
            MaxHierarchyDepth = maxDepth,
            TotalUserMemberships = totalUserMemberships,
            TotalRoleAssignments = totalRoleAssignments,
            GroupsByType = new Dictionary<string, int>
            {
                { "Root", rootGroups },
                { "Leaf", leafGroups },
                { "Intermediate", totalGroups - rootGroups - leafGroups }
            }
        };
    }

    public async Task<bool> GroupNameExistsAsync(string name, int? excludeGroupId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(g => g.Name == name);
        
        if (excludeGroupId.HasValue)
            query = query.Where(g => g.Id != excludeGroupId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task AddUserToGroupAsync(int userId, int groupId, string createdBy, CancellationToken cancellationToken = default)
    {
        var existing = await _context.UserGroups
            .FirstOrDefaultAsync(ug => ug.UserId == userId && ug.GroupId == groupId, cancellationToken);

        if (existing == null)
        {
            var userGroup = new UserGroup
            {
                UserId = userId,
                GroupId = groupId,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            await _context.UserGroups.AddAsync(userGroup, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RemoveUserFromGroupAsync(int userId, int groupId, CancellationToken cancellationToken = default)
    {
        var userGroup = await _context.UserGroups
            .FirstOrDefaultAsync(ug => ug.UserId == userId && ug.GroupId == groupId, cancellationToken);

        if (userGroup != null)
        {
            _context.UserGroups.Remove(userGroup);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task AddGroupHierarchyAsync(int parentGroupId, int childGroupId, string createdBy, CancellationToken cancellationToken = default)
    {
        if (await WouldCreateCycleAsync(parentGroupId, childGroupId, cancellationToken))
            throw new InvalidOperationException("Adding this relationship would create a cycle in the group hierarchy.");

        var existing = await _context.GroupHierarchies
            .FirstOrDefaultAsync(gh => gh.ParentGroupId == parentGroupId && gh.ChildGroupId == childGroupId, cancellationToken);

        if (existing == null)
        {
            var groupHierarchy = new GroupHierarchy
            {
                ParentGroupId = parentGroupId,
                ChildGroupId = childGroupId,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            await _context.GroupHierarchies.AddAsync(groupHierarchy, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RemoveGroupHierarchyAsync(int parentGroupId, int childGroupId, CancellationToken cancellationToken = default)
    {
        var groupHierarchy = await _context.GroupHierarchies
            .FirstOrDefaultAsync(gh => gh.ParentGroupId == parentGroupId && gh.ChildGroupId == childGroupId, cancellationToken);

        if (groupHierarchy != null)
        {
            _context.GroupHierarchies.Remove(groupHierarchy);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}