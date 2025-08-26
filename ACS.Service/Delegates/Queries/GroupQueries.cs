using ACS.Service.Domain;
using ACS.Service.Infrastructure;

namespace ACS.Service.Delegates.Queries;

/// <summary>
/// Query to retrieve a single group by ID
/// </summary>
public class GetGroupByIdQuery : Query<Group?>
{
    public int GroupId { get; set; }
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (GroupId <= 0)
            throw new ArgumentException("Group ID must be greater than zero", nameof(GroupId));
        
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override Group? ExecuteQuery()
    {
        EntityGraph.Groups.TryGetValue(GroupId, out var group);
        return group;
    }
}

/// <summary>
/// Query to retrieve multiple groups with filtering, sorting, and pagination
/// </summary>
public class GetGroupsQuery : Query<ICollection<Group>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
    public int? ParentGroupId { get; set; }
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (Page <= 0)
            throw new ArgumentException("Page must be greater than zero", nameof(Page));
        
        if (PageSize <= 0 || PageSize > 1000)
            throw new ArgumentException("Page size must be between 1 and 1000", nameof(PageSize));
        
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override ICollection<Group> ExecuteQuery()
    {
        var allGroups = EntityGraph.Groups.Values.AsQueryable();

        // Apply parent group filter
        if (ParentGroupId.HasValue)
        {
            allGroups = allGroups.Where(g => g.Parents.Any(p => p.Id == ParentGroupId.Value));
        }

        // Apply search filter
        if (!string.IsNullOrEmpty(Search))
        {
            allGroups = allGroups.Where(g => g.Name.Contains(Search, StringComparison.OrdinalIgnoreCase));
        }

        // Apply sorting
        if (!string.IsNullOrEmpty(SortBy))
        {
            allGroups = SortBy.ToLower() switch
            {
                "name" => SortDescending ? allGroups.OrderByDescending(g => g.Name) : allGroups.OrderBy(g => g.Name),
                "id" => SortDescending ? allGroups.OrderByDescending(g => g.Id) : allGroups.OrderBy(g => g.Id),
                _ => allGroups.OrderBy(g => g.Id)
            };
        }

        // Apply pagination
        return allGroups
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
            .ToList();
    }
}

/// <summary>
/// Query to get total count of groups with optional filtering
/// </summary>
public class GetGroupsCountQuery : Query<int>
{
    public string? Search { get; set; }
    public int? ParentGroupId { get; set; }
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override int ExecuteQuery()
    {
        var allGroups = EntityGraph.Groups.Values.AsQueryable();

        // Apply parent group filter
        if (ParentGroupId.HasValue)
        {
            allGroups = allGroups.Where(g => g.Parents.Any(p => p.Id == ParentGroupId.Value));
        }

        // Apply search filter
        if (!string.IsNullOrEmpty(Search))
        {
            allGroups = allGroups.Where(g => g.Name.Contains(Search, StringComparison.OrdinalIgnoreCase));
        }

        return allGroups.Count();
    }
}

/// <summary>
/// Query to get child groups of a parent group
/// </summary>
public class GetChildGroupsQuery : Query<ICollection<Group>>
{
    public int ParentGroupId { get; set; }
    public bool IncludeNestedChildren { get; set; } = false;
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (ParentGroupId <= 0)
            throw new ArgumentException("Parent group ID must be greater than zero", nameof(ParentGroupId));
        
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override ICollection<Group> ExecuteQuery()
    {
        if (!EntityGraph.Groups.TryGetValue(ParentGroupId, out var parentGroup))
        {
            return new List<Group>();
        }

        if (!IncludeNestedChildren)
        {
            // Return only direct children
            return parentGroup.Children.OfType<Group>().ToList();
        }
        else
        {
            // Return all nested children recursively
            var allChildren = new List<Group>();
            CollectNestedChildren(parentGroup, allChildren);
            return allChildren;
        }
    }

    private void CollectNestedChildren(Group parentGroup, List<Group> allChildren)
    {
        var directChildren = parentGroup.Children.OfType<Group>();
        
        foreach (var child in directChildren)
        {
            if (!allChildren.Contains(child)) // Prevent cycles
            {
                allChildren.Add(child);
                CollectNestedChildren(child, allChildren); // Recursive composition
            }
        }
    }
}

/// <summary>
/// Query to get parent groups of a child group
/// </summary>
public class GetParentGroupsQuery : Query<ICollection<Group>>
{
    public int ChildGroupId { get; set; }
    public bool IncludeNestedParents { get; set; } = false;
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (ChildGroupId <= 0)
            throw new ArgumentException("Child group ID must be greater than zero", nameof(ChildGroupId));
        
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override ICollection<Group> ExecuteQuery()
    {
        if (!EntityGraph.Groups.TryGetValue(ChildGroupId, out var childGroup))
        {
            return new List<Group>();
        }

        if (!IncludeNestedParents)
        {
            // Return only direct parents
            return childGroup.Parents.OfType<Group>().ToList();
        }
        else
        {
            // Return all nested parents recursively
            var allParents = new List<Group>();
            CollectNestedParents(childGroup, allParents);
            return allParents;
        }
    }

    private void CollectNestedParents(Group childGroup, List<Group> allParents)
    {
        var directParents = childGroup.Parents.OfType<Group>();
        
        foreach (var parent in directParents)
        {
            if (!allParents.Contains(parent)) // Prevent cycles
            {
                allParents.Add(parent);
                CollectNestedParents(parent, allParents); // Recursive composition
            }
        }
    }
}

/// <summary>
/// Query to check for circular references in group hierarchy
/// </summary>
public class CheckGroupCycleQuery : Query<bool>
{
    public int ParentGroupId { get; set; }
    public int ChildGroupId { get; set; }
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (ParentGroupId <= 0)
            throw new ArgumentException("Parent group ID must be greater than zero", nameof(ParentGroupId));
        
        if (ChildGroupId <= 0)
            throw new ArgumentException("Child group ID must be greater than zero", nameof(ChildGroupId));
        
        if (ParentGroupId == ChildGroupId)
            throw new ArgumentException("Parent and child group IDs cannot be the same");
        
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override bool ExecuteQuery()
    {
        // Use composition: check if adding child to parent would create a cycle
        // by checking if parent is already a descendant of child
        var getParentsQuery = new GetParentGroupsQuery
        {
            ChildGroupId = ParentGroupId,
            IncludeNestedParents = true,
            EntityGraph = EntityGraph
        };

        var allParentsOfProposedParent = getParentsQuery.Execute();
        return allParentsOfProposedParent.Any(p => p.Id == ChildGroupId);
    }
}

/// <summary>
/// Composite query to get groups with their hierarchy information
/// </summary>
public class GetGroupsWithHierarchyQuery : Query<ICollection<(Group Group, int ChildCount, int ParentCount)>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (Page <= 0)
            throw new ArgumentException("Page must be greater than zero", nameof(Page));
        
        if (PageSize <= 0 || PageSize > 1000)
            throw new ArgumentException("Page size must be between 1 and 1000", nameof(PageSize));
        
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override ICollection<(Group Group, int ChildCount, int ParentCount)> ExecuteQuery()
    {
        // First get the groups using composition
        var groupsQuery = new GetGroupsQuery
        {
            Page = Page,
            PageSize = PageSize,
            Search = Search,
            EntityGraph = EntityGraph
        };

        var groups = groupsQuery.Execute();
        var result = new List<(Group Group, int ChildCount, int ParentCount)>();

        foreach (var group in groups)
        {
            // Compose child count query
            var childQuery = new GetChildGroupsQuery
            {
                ParentGroupId = group.Id,
                IncludeNestedChildren = false,
                EntityGraph = EntityGraph
            };
            var childCount = childQuery.Execute().Count;

            // Compose parent count query
            var parentQuery = new GetParentGroupsQuery
            {
                ChildGroupId = group.Id,
                IncludeNestedParents = false,
                EntityGraph = EntityGraph
            };
            var parentCount = parentQuery.Execute().Count;

            result.Add((group, childCount, parentCount));
        }

        return result;
    }
}

/// <summary>
/// Query to get users in a specific group
/// </summary>
public class GetGroupUsersQuery : Query<ICollection<User>>
{
    public int GroupId { get; set; }
    public bool IncludeNestedUsers { get; set; } = false;
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (GroupId <= 0)
            throw new ArgumentException("Group ID must be greater than zero", nameof(GroupId));
        
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override ICollection<User> ExecuteQuery()
    {
        if (!EntityGraph.Groups.TryGetValue(GroupId, out var group))
        {
            return new List<User>();
        }

        var users = new HashSet<User>();

        // Add direct users
        foreach (var user in group.Users)
        {
            users.Add(user);
        }

        if (IncludeNestedUsers)
        {
            // Get child groups and their users recursively
            var childGroupsQuery = new GetChildGroupsQuery
            {
                ParentGroupId = GroupId,
                IncludeNestedChildren = true,
                EntityGraph = EntityGraph
            };

            var childGroups = childGroupsQuery.Execute();
            
            foreach (var childGroup in childGroups)
            {
                foreach (var user in childGroup.Users)
                {
                    users.Add(user);
                }
            }
        }

        return users.ToList();
    }
}

/// <summary>
/// Query to get groups that a user belongs to
/// </summary>
public class GetUserGroupsQuery : Query<ICollection<Group>>
{
    public int UserId { get; set; }
    public bool IncludeNestedGroups { get; set; } = false;
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (UserId <= 0)
            throw new ArgumentException("User ID must be greater than zero", nameof(UserId));
        
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override ICollection<Group> ExecuteQuery()
    {
        if (!EntityGraph.Users.TryGetValue(UserId, out var user))
        {
            return new List<Group>();
        }

        var groups = new HashSet<Group>();

        // Add direct group memberships
        foreach (var group in user.GroupMemberships)
        {
            groups.Add(group);
        }

        if (IncludeNestedGroups)
        {
            // Add parent groups recursively
            var directGroups = user.GroupMemberships.ToList();
            
            foreach (var directGroup in directGroups)
            {
                var parentGroupsQuery = new GetParentGroupsQuery
                {
                    ChildGroupId = directGroup.Id,
                    IncludeNestedParents = true,
                    EntityGraph = EntityGraph
                };

                var parentGroups = parentGroupsQuery.Execute();
                foreach (var parentGroup in parentGroups)
                {
                    groups.Add(parentGroup);
                }
            }
        }

        return groups.ToList();
    }
}