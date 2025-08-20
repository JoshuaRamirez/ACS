using ACS.Service.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACS.Service.Data.Seeding;

/// <summary>
/// Service for seeding initial data into the database
/// </summary>
public class DataSeeder : IDataSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(ApplicationDbContext context, ILogger<DataSeeder> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Seed all data
    /// </summary>
    public async Task SeedAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting database seeding...");

        try
        {
            // Seed in correct order to respect dependencies
            await SeedVerbTypesAsync(cancellationToken);
            await SeedSchemeTypesAsync(cancellationToken);
            await SeedResourcesAsync(cancellationToken);
            await SeedEntitiesAsync(cancellationToken);
            await SeedDefaultRolesAsync(cancellationToken);
            await SeedDefaultGroupsAsync(cancellationToken);
            await SeedDefaultUsersAsync(cancellationToken);
            await SeedDefaultPermissionsAsync(cancellationToken);
            await SeedAuditLogsAsync(cancellationToken);

            _logger.LogInformation("Database seeding completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during database seeding.");
            throw;
        }
    }

    /// <summary>
    /// Seed verb types
    /// </summary>
    public async Task SeedVerbTypesAsync(CancellationToken cancellationToken = default)
    {
        if (await _context.VerbTypes.AnyAsync(cancellationToken))
        {
            _logger.LogDebug("VerbTypes already seeded, skipping...");
            return;
        }

        _logger.LogInformation("Seeding VerbTypes...");

        var verbTypes = new[]
        {
            new VerbType { Id = 1, VerbName = "GET" },
            new VerbType { Id = 2, VerbName = "POST" },
            new VerbType { Id = 3, VerbName = "PUT" },
            new VerbType { Id = 4, VerbName = "PATCH" },
            new VerbType { Id = 5, VerbName = "DELETE" },
            new VerbType { Id = 6, VerbName = "HEAD" },
            new VerbType { Id = 7, VerbName = "OPTIONS" },
            new VerbType { Id = 8, VerbName = "CONNECT" },
            new VerbType { Id = 9, VerbName = "TRACE" }
        };

        await _context.VerbTypes.AddRangeAsync(verbTypes, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"Seeded {verbTypes.Length} VerbTypes.");
    }

    /// <summary>
    /// Seed scheme types
    /// </summary>
    public async Task SeedSchemeTypesAsync(CancellationToken cancellationToken = default)
    {
        if (await _context.SchemeTypes.AnyAsync(cancellationToken))
        {
            _logger.LogDebug("SchemeTypes already seeded, skipping...");
            return;
        }

        _logger.LogInformation("Seeding SchemeTypes...");

        var schemeTypes = new[]
        {
            new SchemeType { Id = 1, SchemeName = "API Endpoints Authorization" },
            new SchemeType { Id = 2, SchemeName = "Resource-Based Authorization" },
            new SchemeType { Id = 3, SchemeName = "Role-Based Access Control" },
            new SchemeType { Id = 4, SchemeName = "Attribute-Based Access Control" },
            new SchemeType { Id = 5, SchemeName = "Claim-Based Authorization" },
            new SchemeType { Id = 6, SchemeName = "Policy-Based Authorization" }
        };

        await _context.SchemeTypes.AddRangeAsync(schemeTypes, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"Seeded {schemeTypes.Length} SchemeTypes.");
    }

    /// <summary>
    /// Seed resources
    /// </summary>
    public async Task SeedResourcesAsync(CancellationToken cancellationToken = default)
    {
        if (await _context.Resources.AnyAsync(cancellationToken))
        {
            _logger.LogDebug("Resources already seeded, skipping...");
            return;
        }

        _logger.LogInformation("Seeding Resources...");

        var resources = new[]
        {
            new Resource { Id = 1, Uri = "/api/users" },
            new Resource { Id = 2, Uri = "/api/users/*" },
            new Resource { Id = 3, Uri = "/api/users/{id}" },
            new Resource { Id = 4, Uri = "/api/groups" },
            new Resource { Id = 5, Uri = "/api/groups/*" },
            new Resource { Id = 6, Uri = "/api/groups/{id}" },
            new Resource { Id = 7, Uri = "/api/roles" },
            new Resource { Id = 8, Uri = "/api/roles/*" },
            new Resource { Id = 9, Uri = "/api/roles/{id}" },
            new Resource { Id = 10, Uri = "/api/permissions" },
            new Resource { Id = 11, Uri = "/api/permissions/*" },
            new Resource { Id = 12, Uri = "/api/audit" },
            new Resource { Id = 13, Uri = "/api/audit/*" },
            new Resource { Id = 14, Uri = "/api/resources" },
            new Resource { Id = 15, Uri = "/api/resources/*" },
            new Resource { Id = 16, Uri = "/api/admin/*" },
            new Resource { Id = 17, Uri = "/api/reports/*" },
            new Resource { Id = 18, Uri = "/api/bulk/*" },
            new Resource { Id = 19, Uri = "/api/health" },
            new Resource { Id = 20, Uri = "/api/data" }
        };

        await _context.Resources.AddRangeAsync(resources, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"Seeded {resources.Length} Resources.");
    }

    /// <summary>
    /// Seed entities
    /// </summary>
    public async Task SeedEntitiesAsync(CancellationToken cancellationToken = default)
    {
        if (await _context.Entities.AnyAsync(cancellationToken))
        {
            _logger.LogDebug("Entities already seeded, skipping...");
            return;
        }

        _logger.LogInformation("Seeding Entities...");

        var entities = new[]
        {
            new Entity { Id = 1, EntityType = "User", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Entity { Id = 2, EntityType = "User", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Entity { Id = 3, EntityType = "User", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Entity { Id = 4, EntityType = "Role", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Entity { Id = 5, EntityType = "Role", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Entity { Id = 6, EntityType = "Role", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Entity { Id = 7, EntityType = "Role", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Entity { Id = 8, EntityType = "Group", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Entity { Id = 9, EntityType = "Group", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Entity { Id = 10, EntityType = "Group", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };

        await _context.Entities.AddRangeAsync(entities, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"Seeded {entities.Length} Entities.");
    }

    /// <summary>
    /// Seed default roles
    /// </summary>
    public async Task SeedDefaultRolesAsync(CancellationToken cancellationToken = default)
    {
        if (await _context.Roles.AnyAsync(cancellationToken))
        {
            _logger.LogDebug("Roles already seeded, skipping...");
            return;
        }

        _logger.LogInformation("Seeding Default Roles...");

        var roles = new[]
        {
            new Role 
            { 
                Id = 1, 
                Name = "System Administrator", 
                EntityId = 4,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Role 
            { 
                Id = 2, 
                Name = "User Administrator", 
                EntityId = 5,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Role 
            { 
                Id = 3, 
                Name = "Auditor", 
                EntityId = 6,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Role 
            { 
                Id = 4, 
                Name = "Standard User", 
                EntityId = 7,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        await _context.Roles.AddRangeAsync(roles, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"Seeded {roles.Length} Default Roles.");
    }

    /// <summary>
    /// Seed default groups
    /// </summary>
    public async Task SeedDefaultGroupsAsync(CancellationToken cancellationToken = default)
    {
        if (await _context.Groups.AnyAsync(cancellationToken))
        {
            _logger.LogDebug("Groups already seeded, skipping...");
            return;
        }

        _logger.LogInformation("Seeding Default Groups...");

        var groups = new[]
        {
            new Group 
            { 
                Id = 1, 
                Name = "Administrators", 
                EntityId = 8,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Group 
            { 
                Id = 2, 
                Name = "Development", 
                EntityId = 9,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Group 
            { 
                Id = 3, 
                Name = "Operations", 
                EntityId = 10,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        await _context.Groups.AddRangeAsync(groups, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        // Add group roles
        var groupRoles = new[]
        {
            new GroupRole { Id = 1, GroupId = 1, RoleId = 1, CreatedBy = "system", CreatedAt = DateTime.UtcNow }, // Administrators -> System Administrator
            new GroupRole { Id = 2, GroupId = 2, RoleId = 4, CreatedBy = "system", CreatedAt = DateTime.UtcNow }, // Development -> Standard User
            new GroupRole { Id = 3, GroupId = 3, RoleId = 3, CreatedBy = "system", CreatedAt = DateTime.UtcNow }  // Operations -> Auditor
        };

        await _context.GroupRoles.AddRangeAsync(groupRoles, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"Seeded {groups.Length} Default Groups and {groupRoles.Length} Group-Role associations.");
    }

    /// <summary>
    /// Seed default users
    /// </summary>
    public async Task SeedDefaultUsersAsync(CancellationToken cancellationToken = default)
    {
        if (await _context.Users.AnyAsync(cancellationToken))
        {
            _logger.LogDebug("Users already seeded, skipping...");
            return;
        }

        _logger.LogInformation("Seeding Default Users...");

        // Hash passwords (in production, use proper password hashing)
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!");
        var salt = BCrypt.Net.BCrypt.GenerateSalt();

        var users = new[]
        {
            new User 
            { 
                Id = 1, 
                Name = "System Admin", 
                Email = "admin@acs.local",
                PasswordHash = passwordHash,
                Salt = salt,
                EntityId = 1,
                IsActive = true,
                FailedLoginAttempts = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new User 
            { 
                Id = 2, 
                Name = "Alice Developer", 
                Email = "alice@acs.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Dev123!"),
                Salt = salt,
                EntityId = 2,
                IsActive = true,
                FailedLoginAttempts = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new User 
            { 
                Id = 3, 
                Name = "Bob Operations", 
                Email = "bob@acs.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Ops123!"),
                Salt = salt,
                EntityId = 3,
                IsActive = true,
                FailedLoginAttempts = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        await _context.Users.AddRangeAsync(users, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        // Add user-group associations
        var userGroups = new[]
        {
            new UserGroup { Id = 1, UserId = 1, GroupId = 1, CreatedBy = "system", CreatedAt = DateTime.UtcNow }, // Admin -> Administrators
            new UserGroup { Id = 2, UserId = 2, GroupId = 2, CreatedBy = "system", CreatedAt = DateTime.UtcNow }, // Alice -> Development
            new UserGroup { Id = 3, UserId = 3, GroupId = 3, CreatedBy = "system", CreatedAt = DateTime.UtcNow }  // Bob -> Operations
        };

        await _context.UserGroups.AddRangeAsync(userGroups, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        // Add direct user-role associations
        var userRoles = new[]
        {
            new UserRole { Id = 1, UserId = 1, RoleId = 1, CreatedBy = "system", CreatedAt = DateTime.UtcNow } // Admin -> System Administrator
        };

        await _context.UserRoles.AddRangeAsync(userRoles, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"Seeded {users.Length} Default Users with group and role associations.");
    }

    /// <summary>
    /// Seed default permissions
    /// </summary>
    public async Task SeedDefaultPermissionsAsync(CancellationToken cancellationToken = default)
    {
        if (await _context.EntityPermissions.AnyAsync(cancellationToken))
        {
            _logger.LogDebug("Permissions already seeded, skipping...");
            return;
        }

        _logger.LogInformation("Seeding Default Permissions...");

        // Create permission schemes
        var permissionSchemes = new[]
        {
            new PermissionScheme { Id = 1, EntityId = 4, SchemeTypeId = 3 }, // System Administrator - RBAC
            new PermissionScheme { Id = 2, EntityId = 5, SchemeTypeId = 3 }, // User Administrator - RBAC
            new PermissionScheme { Id = 3, EntityId = 6, SchemeTypeId = 3 }, // Auditor - RBAC
            new PermissionScheme { Id = 4, EntityId = 7, SchemeTypeId = 3 }, // Standard User - RBAC
            new PermissionScheme { Id = 5, EntityId = 8, SchemeTypeId = 2 }, // Administrators Group - Resource-Based
            new PermissionScheme { Id = 6, EntityId = 9, SchemeTypeId = 2 }, // Development Group - Resource-Based
            new PermissionScheme { Id = 7, EntityId = 10, SchemeTypeId = 2 } // Operations Group - Resource-Based
        };

        await _context.EntityPermissions.AddRangeAsync(permissionSchemes, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        // Create URI access rules
        var uriAccesses = new List<UriAccess>();
        int accessId = 1;

        // System Administrator - full access to everything
        foreach (var resource in await _context.Resources.ToListAsync(cancellationToken))
        {
            foreach (var verb in await _context.VerbTypes.ToListAsync(cancellationToken))
            {
                uriAccesses.Add(new UriAccess
                {
                    Id = accessId++,
                    ResourceId = resource.Id,
                    VerbTypeId = verb.Id,
                    PermissionSchemeId = 1,
                    Grant = true,
                    Deny = false
                });
            }
        }

        // User Administrator - manage users and groups
        var userAdminResources = new[] { 1, 2, 3, 4, 5, 6 }; // user and group resources
        foreach (var resourceId in userAdminResources)
        {
            foreach (var verb in await _context.VerbTypes.ToListAsync(cancellationToken))
            {
                uriAccesses.Add(new UriAccess
                {
                    Id = accessId++,
                    ResourceId = resourceId,
                    VerbTypeId = verb.Id,
                    PermissionSchemeId = 2,
                    Grant = true,
                    Deny = false
                });
            }
        }

        // Auditor - read-only access to audit and reports
        var auditorResources = new[] { 12, 13, 17 }; // audit and reports
        var readVerbs = new[] { 1, 6, 7 }; // GET, HEAD, OPTIONS
        foreach (var resourceId in auditorResources)
        {
            foreach (var verbId in readVerbs)
            {
                uriAccesses.Add(new UriAccess
                {
                    Id = accessId++,
                    ResourceId = resourceId,
                    VerbTypeId = verbId,
                    PermissionSchemeId = 3,
                    Grant = true,
                    Deny = false
                });
            }
        }

        // Standard User - basic read access
        var standardUserResources = new[] { 1, 4, 7, 19, 20 }; // users (read), groups (read), roles (read), health, data
        foreach (var resourceId in standardUserResources)
        {
            uriAccesses.Add(new UriAccess
            {
                Id = accessId++,
                ResourceId = resourceId,
                VerbTypeId = 1, // GET only
                PermissionSchemeId = 4,
                Grant = true,
                Deny = false
            });
        }

        await _context.UriAccesses.AddRangeAsync(uriAccesses, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"Seeded {permissionSchemes.Length} Permission Schemes and {uriAccesses.Count} URI Access rules.");
    }

    /// <summary>
    /// Seed audit logs
    /// </summary>
    public async Task SeedAuditLogsAsync(CancellationToken cancellationToken = default)
    {
        if (await _context.AuditLogs.AnyAsync(cancellationToken))
        {
            _logger.LogDebug("Audit logs already seeded, skipping...");
            return;
        }

        _logger.LogInformation("Seeding Initial Audit Logs...");

        var auditLogs = new[]
        {
            new AuditLog
            {
                Id = 1,
                EntityType = "System",
                EntityId = 0,
                ChangeType = "Initialize",
                ChangedBy = "system",
                ChangeDate = DateTime.UtcNow,
                ChangeDetails = "System initialized with default data seed"
            },
            new AuditLog
            {
                Id = 2,
                EntityType = "User",
                EntityId = 1,
                ChangeType = "Create",
                ChangedBy = "system",
                ChangeDate = DateTime.UtcNow,
                ChangeDetails = "Created system administrator account"
            },
            new AuditLog
            {
                Id = 3,
                EntityType = "Role",
                EntityId = 1,
                ChangeType = "Create",
                ChangedBy = "system",
                ChangeDate = DateTime.UtcNow,
                ChangeDetails = "Created System Administrator role with full permissions"
            },
            new AuditLog
            {
                Id = 4,
                EntityType = "Group",
                EntityId = 1,
                ChangeType = "Create",
                ChangedBy = "system",
                ChangeDate = DateTime.UtcNow,
                ChangeDetails = "Created Administrators group"
            }
        };

        await _context.AuditLogs.AddRangeAsync(auditLogs, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"Seeded {auditLogs.Length} Initial Audit Logs.");
    }
}

/// <summary>
/// Interface for data seeding service
/// </summary>
public interface IDataSeeder
{
    Task SeedAllAsync(CancellationToken cancellationToken = default);
    Task SeedVerbTypesAsync(CancellationToken cancellationToken = default);
    Task SeedSchemeTypesAsync(CancellationToken cancellationToken = default);
    Task SeedResourcesAsync(CancellationToken cancellationToken = default);
    Task SeedEntitiesAsync(CancellationToken cancellationToken = default);
    Task SeedDefaultRolesAsync(CancellationToken cancellationToken = default);
    Task SeedDefaultGroupsAsync(CancellationToken cancellationToken = default);
    Task SeedDefaultUsersAsync(CancellationToken cancellationToken = default);
    Task SeedDefaultPermissionsAsync(CancellationToken cancellationToken = default);
    Task SeedAuditLogsAsync(CancellationToken cancellationToken = default);
}