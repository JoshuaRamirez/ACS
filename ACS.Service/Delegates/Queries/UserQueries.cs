using ACS.Service.Domain;
using ACS.Service.Infrastructure;

namespace ACS.Service.Delegates.Queries;

/// <summary>
/// Query to retrieve a single user by ID
/// </summary>
public class GetUserByIdQuery : Query<User?>
{
    public int UserId { get; set; }
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (UserId <= 0)
            throw new ArgumentException("User ID must be greater than zero", nameof(UserId));
        
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override User? ExecuteQuery()
    {
        EntityGraph.Users.TryGetValue(UserId, out var user);
        return user;
    }
}

/// <summary>
/// Query to retrieve multiple users with filtering, sorting, and pagination
/// </summary>
public class GetUsersQuery : Query<ICollection<User>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
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

    protected override ICollection<User> ExecuteQuery()
    {
        var allUsers = EntityGraph.Users.Values.AsQueryable();

        // Apply search filter
        if (!string.IsNullOrEmpty(Search))
        {
            allUsers = allUsers.Where(u => u.Name.Contains(Search, StringComparison.OrdinalIgnoreCase));
        }

        // Apply sorting
        if (!string.IsNullOrEmpty(SortBy))
        {
            allUsers = SortBy.ToLower() switch
            {
                "name" => SortDescending ? allUsers.OrderByDescending(u => u.Name) : allUsers.OrderBy(u => u.Name),
                "id" => SortDescending ? allUsers.OrderByDescending(u => u.Id) : allUsers.OrderBy(u => u.Id),
                _ => allUsers.OrderBy(u => u.Id)
            };
        }

        // Apply pagination
        return allUsers
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
            .ToList();
    }
}

/// <summary>
/// Query to get total count of users with optional filtering
/// Designed to be composable with GetUsersQuery for pagination
/// </summary>
public class GetUsersCountQuery : Query<int>
{
    public string? Search { get; set; }
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override int ExecuteQuery()
    {
        var allUsers = EntityGraph.Users.Values.AsQueryable();

        // Apply same search filter as GetUsersQuery for consistency
        if (!string.IsNullOrEmpty(Search))
        {
            allUsers = allUsers.Where(u => u.Name.Contains(Search, StringComparison.OrdinalIgnoreCase));
        }

        return allUsers.Count();
    }
}

/// <summary>
/// Query to check if a user exists by ID
/// </summary>
public class UserExistsQuery : Query<bool>
{
    public int UserId { get; set; }
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (UserId <= 0)
            throw new ArgumentException("User ID must be greater than zero", nameof(UserId));
        
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override bool ExecuteQuery()
    {
        return EntityGraph.Users.ContainsKey(UserId);
    }
}

/// <summary>
/// Query to find users by name pattern
/// </summary>
public class FindUsersByNameQuery : Query<ICollection<User>>
{
    public string NamePattern { get; set; } = string.Empty;
    public bool ExactMatch { get; set; } = false;
    public InMemoryEntityGraph EntityGraph { get; set; } = null!;

    protected override void Validate()
    {
        if (string.IsNullOrWhiteSpace(NamePattern))
            throw new ArgumentException("Name pattern cannot be null or empty", nameof(NamePattern));
        
        if (EntityGraph == null)
            throw new ArgumentException("Entity graph cannot be null", nameof(EntityGraph));
    }

    protected override ICollection<User> ExecuteQuery()
    {
        var allUsers = EntityGraph.Users.Values;

        if (ExactMatch)
        {
            return allUsers.Where(u => u.Name.Equals(NamePattern, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        else
        {
            return allUsers.Where(u => u.Name.Contains(NamePattern, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }
}

/// <summary>
/// Composite query that demonstrates query composition
/// Gets users with their total count in a single operation
/// </summary>
public class GetUsersWithCountQuery : Query<(ICollection<User> Users, int TotalCount)>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
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

    protected override (ICollection<User> Users, int TotalCount) ExecuteQuery()
    {
        // Compose queries - execute count query first
        var countQuery = new GetUsersCountQuery
        {
            Search = Search,
            EntityGraph = EntityGraph
        };
        var totalCount = countQuery.Execute();

        // Then execute users query
        var usersQuery = new GetUsersQuery
        {
            Page = Page,
            PageSize = PageSize,
            Search = Search,
            SortBy = SortBy,
            SortDescending = SortDescending,
            EntityGraph = EntityGraph
        };
        var users = usersQuery.Execute();

        return (users, totalCount);
    }
}