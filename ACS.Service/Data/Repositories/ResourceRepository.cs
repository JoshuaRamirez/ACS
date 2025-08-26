using ACS.Service.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace ACS.Service.Data.Repositories;

/// <summary>
/// Repository implementation for Resource-specific operations
/// </summary>
public class ResourceRepository : Repository<Resource>, IResourceRepository
{
    public ResourceRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Resource?> FindByUriAsync(string uri, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(r => r.Uri == uri, cancellationToken);
    }

    public async Task<IEnumerable<Resource>> FindByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(r => EF.Functions.Like(r.Uri, pattern))
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Resource>> FindResourcesWithAccessAsync(CancellationToken cancellationToken = default)
    {
        return await (from r in _dbSet
                     join ua in _context.UriAccesses on r.Id equals ua.ResourceId
                     select r)
                    .Distinct()
                    .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Resource>> FindUnusedResourcesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(r => !_context.UriAccesses.Any(ua => ua.ResourceId == r.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<Resource?> GetResourceWithPermissionsAsync(int resourceId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(r => r.UriAccesses)
                .ThenInclude(ua => ua.VerbType)
            .Include(r => r.UriAccesses)
                .ThenInclude(ua => ua.PermissionScheme)
                    .ThenInclude(ps => ps.Entity)
            .FirstOrDefaultAsync(r => r.Id == resourceId, cancellationToken);
    }

    public async Task<IEnumerable<Resource>> FindByVerbTypeAsync(string verbName, CancellationToken cancellationToken = default)
    {
        return await (from r in _dbSet
                     join ua in _context.UriAccesses on r.Id equals ua.ResourceId
                     join vt in _context.VerbTypes on ua.VerbTypeId equals vt.Id
                     where vt.VerbName == verbName
                     select r)
                    .Distinct()
                    .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<ResourceWithPermissionCount>> GetResourcesWithPermissionCountsAsync(Expression<Func<Resource, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        var resourcesQuery = _dbSet.AsQueryable();
        
        if (predicate != null)
            resourcesQuery = resourcesQuery.Where(predicate);

        var resources = await resourcesQuery.ToListAsync(cancellationToken);

        var permissionCounts = (from r in resources
                                    join ua in _context.UriAccesses on r.Id equals ua.ResourceId into accesses
                                    from access in accesses.DefaultIfEmpty()
                                    join vt in _context.VerbTypes on access.VerbTypeId equals vt.Id into verbs
                                    from verb in verbs.DefaultIfEmpty()
                                    join ps in _context.EntityPermissions on access.PermissionSchemeId equals ps.Id into schemes
                                    from scheme in schemes.DefaultIfEmpty()
                                    group new { r, access, verb, scheme } by r.Id into g
                                    select new
                                    {
                                        ResourceId = g.Key,
                                        TotalPermissions = g.Count(x => x.access != null),
                                        GrantPermissions = g.Count(x => x.access != null && x.access.Grant),
                                        DenyPermissions = g.Count(x => x.access != null && x.access.Deny),
                                        UniqueEntities = g.Where(x => x.scheme != null).Select(x => x.scheme.EntityId).Distinct().Count(),
                                        VerbTypes = g.Where(x => x.verb != null).Select(x => x.verb.VerbName).Distinct()
                                    }).ToList();

        return resources.Select(resource =>
        {
            var counts = permissionCounts.FirstOrDefault(c => c.ResourceId == resource.Id);
            return new ResourceWithPermissionCount
            {
                Resource = resource,
                TotalPermissions = counts?.TotalPermissions ?? 0,
                GrantPermissions = counts?.GrantPermissions ?? 0,
                DenyPermissions = counts?.DenyPermissions ?? 0,
                UniqueEntities = counts?.UniqueEntities ?? 0,
                VerbTypes = counts?.VerbTypes ?? new List<string>()
            };
        });
    }

    public async Task<bool> UriExistsAsync(string uri, int? excludeResourceId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(r => r.Uri == uri);
        
        if (excludeResourceId.HasValue)
            query = query.Where(r => r.Id != excludeResourceId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<ResourceStatistics> GetResourceStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var totalResources = await _dbSet.CountAsync(cancellationToken);
        var resourcesWithPermissions = await (from r in _dbSet
                                            join ua in _context.UriAccesses on r.Id equals ua.ResourceId
                                            select r.Id)
                                           .Distinct()
                                           .CountAsync(cancellationToken);
        var unusedResources = totalResources - resourcesWithPermissions;
        var resourcesWithPatterns = await _dbSet.CountAsync(r => r.Uri.Contains("*") || r.Uri.Contains("?") || r.Uri.Contains("{"), cancellationToken);
        
        var totalUriAccesses = await _context.UriAccesses.CountAsync(cancellationToken);
        var grantAccessCount = await _context.UriAccesses.CountAsync(ua => ua.Grant, cancellationToken);
        var denyAccessCount = await _context.UriAccesses.CountAsync(ua => ua.Deny, cancellationToken);

        // Resource type distribution
        var resourcesByType = await _dbSet
            .Select(r => GetResourceType(r.Uri))
            .GroupBy(type => type)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);

        // Verb distribution
        var verbDistribution = await (from ua in _context.UriAccesses
                                    join vt in _context.VerbTypes on ua.VerbTypeId equals vt.Id
                                    group ua by vt.VerbName into g
                                    select new { VerbName = g.Key, Count = g.Count() })
                                   .ToDictionaryAsync(x => x.VerbName, x => x.Count, cancellationToken);

        return new ResourceStatistics
        {
            TotalResources = totalResources,
            ResourcesWithPermissions = resourcesWithPermissions,
            UnusedResources = unusedResources,
            ResourcesWithPatterns = resourcesWithPatterns,
            ResourcesByType = resourcesByType,
            VerbDistribution = verbDistribution,
            TotalUriAccesses = totalUriAccesses,
            GrantAccessCount = grantAccessCount,
            DenyAccessCount = denyAccessCount
        };
    }

    public async Task<IEnumerable<Resource>> FindResourcesByEntityAsync(int entityId, CancellationToken cancellationToken = default)
    {
        return await (from r in _dbSet
                     join ua in _context.UriAccesses on r.Id equals ua.ResourceId
                     join ps in _context.EntityPermissions on ua.PermissionSchemeId equals ps.Id
                     where ps.EntityId == entityId
                     select r)
                    .Distinct()
                    .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Resource>> FindResourcesAccessibleByUserAsync(int userId, string? verbFilter = null, CancellationToken cancellationToken = default)
    {
        var query = from r in _dbSet
                   join ua in _context.UriAccesses on r.Id equals ua.ResourceId
                   join ps in _context.EntityPermissions on ua.PermissionSchemeId equals ps.Id
                   join e in _context.Entities on ps.EntityId equals e.Id
                   where ua.Grant
                   select new { r, ua, ps, e };

        // Direct user permissions
        var directQuery = from result in query
                         join u in _context.Users on result.e.Id equals u.EntityId
                         where u.Id == userId
                         select result.r;

        // Role-based permissions
        var roleQuery = from result in query
                       join role in _context.Roles on result.e.Id equals role.EntityId
                       join ur in _context.UserRoles on role.Id equals ur.RoleId
                       where ur.UserId == userId
                       select result.r;

        // Group-based permissions (direct and inherited)
        var groupQuery = from result in query
                        join grp in _context.Groups on result.e.Id equals grp.EntityId
                        join ug in _context.UserGroups on grp.Id equals ug.GroupId
                        where ug.UserId == userId
                        select result.r;

        var allAccessibleResources = directQuery.Union(roleQuery).Union(groupQuery);

        if (!string.IsNullOrEmpty(verbFilter))
        {
            allAccessibleResources = from r in allAccessibleResources
                                   join ua in _context.UriAccesses on r.Id equals ua.ResourceId
                                   join vt in _context.VerbTypes on ua.VerbTypeId equals vt.Id
                                   where vt.VerbName == verbFilter && ua.Grant
                                   select r;
        }

        return await allAccessibleResources.Distinct().ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Resource>> FindMatchingResourcePatternsAsync(string uri, CancellationToken cancellationToken = default)
    {
        var resources = await _dbSet.ToListAsync(cancellationToken);
        var matchingResources = new List<Resource>();

        foreach (var resource in resources)
        {
            if (IsUriMatchingPattern(uri, resource.Uri))
            {
                matchingResources.Add(resource);
            }
        }

        return matchingResources;
    }

    public async Task<ResourceAccessMatrix> GetResourceAccessMatrixAsync(IEnumerable<int>? resourceIds = null, CancellationToken cancellationToken = default)
    {
        var resourceQuery = _dbSet.AsQueryable();
        if (resourceIds != null && resourceIds.Any())
            resourceQuery = resourceQuery.Where(r => resourceIds.Contains(r.Id));

        var resources = await resourceQuery.ToListAsync(cancellationToken);
        var entities = await _context.Entities.ToListAsync(cancellationToken);
        var verbTypes = await _context.VerbTypes.ToListAsync(cancellationToken);

        var accessMap = await (from ua in _context.UriAccesses
                             join ps in _context.EntityPermissions on ua.PermissionSchemeId equals ps.Id
                             join st in _context.SchemeTypes on ps.SchemeTypeId equals st.Id
                             where resourceIds == null || resourceIds.Contains(ua.ResourceId)
                             select new AccessPermission
                             {
                                 ResourceId = ua.ResourceId,
                                 EntityId = ps.EntityId ?? 0,
                                 VerbTypeId = ua.VerbTypeId,
                                 IsGrant = ua.Grant,
                                 IsDeny = ua.Deny,
                                 SchemeType = st.SchemeName
                             })
                            .ToDictionaryAsync(ap => $"{ap.ResourceId}:{ap.EntityId}:{ap.VerbTypeId}", cancellationToken);

        return new ResourceAccessMatrix
        {
            Resources = resources,
            Entities = entities,
            VerbTypes = verbTypes,
            AccessMap = accessMap
        };
    }

    public async Task<IEnumerable<ResourceConflict>> FindResourceConflictsAsync(CancellationToken cancellationToken = default)
    {
        var conflicts = await (from ua1 in _context.UriAccesses
                             join ua2 in _context.UriAccesses on new { ua1.ResourceId, ua1.VerbTypeId } equals new { ua2.ResourceId, ua2.VerbTypeId }
                             join r in _context.Resources on ua1.ResourceId equals r.Id
                             join vt in _context.VerbTypes on ua1.VerbTypeId equals vt.Id
                             join ps1 in _context.EntityPermissions on ua1.PermissionSchemeId equals ps1.Id
                             join ps2 in _context.EntityPermissions on ua2.PermissionSchemeId equals ps2.Id
                             join e1 in _context.Entities on ps1.EntityId equals e1.Id
                             join e2 in _context.Entities on ps2.EntityId equals e2.Id
                             where ua1.Id != ua2.Id && 
                                   ((ua1.Grant && ua2.Deny) || (ua1.Deny && ua2.Grant))
                             group new { r, vt, e1, e2 } by new { ua1.ResourceId, ua1.VerbTypeId } into g
                             select new
                             {
                                 Resource = g.First().r,
                                 VerbType = g.First().vt,
                                 ConflictingEntities = g.Select(x => new[] { x.e1, x.e2 }).SelectMany(x => x).Distinct()
                             })
                            .ToListAsync(cancellationToken);

        return conflicts.Select(c => new ResourceConflict
        {
            Resource = c.Resource,
            VerbType = c.VerbType,
            ConflictingEntities = c.ConflictingEntities,
            ConflictType = "Grant-Deny",
            Severity = DetermineConflictSeverity(c.ConflictingEntities.Count())
        });
    }

    private static bool IsUriMatchingPattern(string uri, string pattern)
    {
        if (pattern == uri) return true;
        if (!pattern.Contains('*') && !pattern.Contains('?') && !pattern.Contains('{')) return false;

        // Convert URI pattern to regex
        var regexPattern = pattern
            .Replace("*", ".*")
            .Replace("?", ".")
            .Replace("{", "(?<param")
            .Replace("}", ">[^/]+)");

        regexPattern = "^" + regexPattern + "$";

        try
        {
            return Regex.IsMatch(uri, regexPattern, RegexOptions.IgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string GetResourceType(string uri)
    {
        if (string.IsNullOrEmpty(uri)) return "Unknown";
        
        var segments = uri.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return "Root";
        
        return segments[0];
    }

    private static string DetermineConflictSeverity(int entityCount)
    {
        return entityCount switch
        {
            <= 2 => "Low",
            <= 5 => "Medium",
            _ => "High"
        };
    }
}