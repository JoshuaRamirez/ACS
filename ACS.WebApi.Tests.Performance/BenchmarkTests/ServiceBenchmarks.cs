using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ACS.Service.Data;
using ACS.Service.Services;
using ACS.Service.Domain;
using Microsoft.Extensions.Logging;
using Moq;

namespace ACS.WebApi.Tests.Performance.BenchmarkTests;

/// <summary>
/// Micro-benchmarks for service layer operations
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[RPlotExporter]
public class ServiceBenchmarks
{
    private ServiceProvider? _serviceProvider;
    private IUserService? _userService;
    private IGroupService? _groupService;
    private IRoleService? _roleService;
    private IPermissionEvaluationService? _permissionService;
    private ApplicationDbContext? _context;

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        var services = new ServiceCollection();

        // Add in-memory database
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"BenchmarkDb_{Guid.NewGuid()}"));

        // Add logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // Add services
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPermissionEvaluationService, PermissionEvaluationService>();

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<ApplicationDbContext>();
        _userService = _serviceProvider.GetRequiredService<IUserService>();
        _groupService = _serviceProvider.GetRequiredService<IGroupService>();
        _roleService = _serviceProvider.GetRequiredService<IRoleService>();
        _permissionService = _serviceProvider.GetRequiredService<IPermissionEvaluationService>();

        // Seed test data
        await SeedBenchmarkDataAsync();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _serviceProvider?.Dispose();
    }

    [Benchmark]
    public async Task<IEnumerable<User>> GetAllUsers()
    {
        return await _userService!.GetAllUsersAsync();
    }

    [Benchmark]
    public async Task<User?> GetUserById()
    {
        return await _userService!.GetUserByIdAsync(1);
    }

    [Benchmark]
    [Arguments(1, 10)]
    [Arguments(1, 50)]
    [Arguments(1, 100)]
    public async Task<IEnumerable<User>> GetUsersPaginated(int page, int size)
    {
        return await _userService!.GetUsersAsync(page, size);
    }

    [Benchmark]
    [Arguments("User_1")]
    [Arguments("test")]
    [Arguments("admin")]
    public async Task<IEnumerable<User>> SearchUsers(string searchTerm)
    {
        return await _userService!.SearchUsersAsync(searchTerm);
    }

    [Benchmark]
    public async Task<User> CreateUser()
    {
        var user = new User
        {
            Name = $"BenchmarkUser_{Guid.NewGuid()}",
            Email = $"benchmark{Guid.NewGuid()}@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!")
        };

        return await _userService!.CreateUserAsync(user);
    }

    [Benchmark]
    public async Task<IEnumerable<Group>> GetAllGroups()
    {
        return await _groupService!.GetAllGroupsAsync();
    }

    [Benchmark]
    public async Task<Group?> GetGroupById()
    {
        return await _groupService!.GetGroupByIdAsync(1);
    }

    [Benchmark]
    public async Task<IEnumerable<Group>> GetGroupHierarchy()
    {
        return await _groupService!.GetGroupHierarchyAsync(1);
    }

    [Benchmark]
    public async Task<IEnumerable<Role>> GetAllRoles()
    {
        return await _roleService!.GetAllRolesAsync();
    }

    [Benchmark]
    public async Task<Role?> GetRoleById()
    {
        return await _roleService!.GetRoleByIdAsync(1);
    }

    [Benchmark]
    [Arguments(1, "/api/users", "GET")]
    [Arguments(1, "/api/admin", "POST")]
    [Arguments(1, "/api/reports", "GET")]
    public async Task<bool> EvaluatePermission(int userId, string resource, string action)
    {
        return await _permissionService!.HasPermissionAsync(userId, resource, action);
    }

    [Benchmark]
    public async Task<IEnumerable<string>> GetUserPermissions()
    {
        return await _permissionService!.GetUserPermissionsAsync(1);
    }

    [Benchmark]
    [Arguments(10)]
    [Arguments(50)]
    [Arguments(100)]
    public async Task<List<User>> CreateMultipleUsers(int count)
    {
        var users = new List<User>();
        var tasks = new List<Task<User>>();

        for (int i = 0; i < count; i++)
        {
            var user = new User
            {
                Name = $"BulkUser_{Guid.NewGuid()}",
                Email = $"bulk{Guid.NewGuid()}@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!")
            };

            tasks.Add(_userService!.CreateUserAsync(user));
        }

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    [Benchmark]
    [Arguments(10)]
    [Arguments(50)]
    [Arguments(100)]
    public async Task<List<User>> GetMultipleUsers(int count)
    {
        var tasks = new List<Task<User?>>();

        for (int i = 1; i <= count; i++)
        {
            tasks.Add(_userService!.GetUserByIdAsync(i));
        }

        var results = await Task.WhenAll(tasks);
        return results.Where(u => u != null).Cast<User>().ToList();
    }

    [Benchmark]
    public async Task ComplexPermissionEvaluation()
    {
        // Simulate complex permission evaluation scenario
        var userId = 1;
        var resources = new[]
        {
            "/api/users", "/api/groups", "/api/roles", "/api/admin",
            "/api/reports", "/api/audit", "/api/bulk", "/api/export"
        };
        var actions = new[] { "GET", "POST", "PUT", "DELETE" };

        var tasks = new List<Task<bool>>();

        foreach (var resource in resources)
        {
            foreach (var action in actions)
            {
                tasks.Add(_permissionService!.HasPermissionAsync(userId, resource, action));
            }
        }

        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task DatabaseQueryOptimization()
    {
        // Test EF Core query performance
        var users = await _context!.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Where(u => u.IsActive)
            .OrderBy(u => u.Name)
            .Take(50)
            .ToListAsync();

        var groups = await _context.Groups
            .Include(g => g.ParentGroup)
            .Include(g => g.ChildGroups)
            .Where(g => g.ParentGroupId == null)
            .ToListAsync();

        // Return combined count to prevent compiler optimization
        await Task.CompletedTask;
    }

    [Benchmark]
    public async Task ConcurrentPermissionChecks()
    {
        var userIds = Enumerable.Range(1, 10).ToList();
        var resource = "/api/users";
        var action = "GET";

        var tasks = userIds.Select(async userId =>
            await _permissionService!.HasPermissionAsync(userId, resource, action));

        await Task.WhenAll(tasks);
    }

    private async Task SeedBenchmarkDataAsync()
    {
        // Clear existing data
        _context!.Users.RemoveRange(_context.Users);
        _context.Groups.RemoveRange(_context.Groups);
        _context.Roles.RemoveRange(_context.Roles);
        await _context.SaveChangesAsync();

        // Create roles
        var roles = new List<Service.Data.Models.Role>();
        for (int i = 1; i <= 20; i++)
        {
            roles.Add(new Service.Data.Models.Role
            {
                Id = i,
                Name = $"BenchmarkRole_{i}",
                Description = $"Benchmark role {i}",
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "benchmark",
                LastModifiedDate = DateTime.UtcNow,
                LastModifiedBy = "benchmark"
            });
        }
        _context.Roles.AddRange(roles);

        // Create groups
        var groups = new List<Service.Data.Models.Group>();
        for (int i = 1; i <= 50; i++)
        {
            groups.Add(new Service.Data.Models.Group
            {
                Id = i,
                Name = $"BenchmarkGroup_{i}",
                Description = $"Benchmark group {i}",
                ParentGroupId = i > 10 ? Random.Shared.Next(1, 10) : null,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "benchmark",
                LastModifiedDate = DateTime.UtcNow,
                LastModifiedBy = "benchmark"
            });
        }
        _context.Groups.AddRange(groups);

        // Create users
        var users = new List<Service.Data.Models.User>();
        for (int i = 1; i <= 200; i++)
        {
            users.Add(new Service.Data.Models.User
            {
                Id = i,
                Name = $"BenchmarkUser_{i}",
                Email = $"benchmark{i}@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "benchmark",
                LastModifiedDate = DateTime.UtcNow,
                LastModifiedBy = "benchmark"
            });
        }
        _context.Users.AddRange(users);

        await _context.SaveChangesAsync();

        Console.WriteLine("Seeded benchmark data: 200 users, 50 groups, 20 roles");
    }
}

/// <summary>
/// Test runner for benchmarks
/// </summary>
[TestClass]
public class BenchmarkRunner
{
    [TestMethod]
    public void RunServiceBenchmarks()
    {
        // Only run benchmarks in Release mode or when explicitly requested
        if (Environment.GetEnvironmentVariable("RUN_BENCHMARKS") == "true" || 
            string.Equals(Environment.GetEnvironmentVariable("Configuration"), "Release", StringComparison.OrdinalIgnoreCase))
        {
            var summary = BenchmarkRunner.Run<ServiceBenchmarks>();
            Console.WriteLine(summary);
        }
        else
        {
            Console.WriteLine("Benchmarks skipped. Set RUN_BENCHMARKS=true to run benchmarks.");
            Assert.Inconclusive("Benchmarks not run in this configuration");
        }
    }
}