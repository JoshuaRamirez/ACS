using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ACS.Service.Data;

namespace ACS.WebApi.Tests.E2E.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory for end-to-end testing with realistic configuration
/// </summary>
public class E2ETestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly bool _useRealDatabase;

    public E2ETestWebApplicationFactory(bool useRealDatabase = false)
    {
        _useRealDatabase = useRealDatabase;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing database context
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            var dbContextServiceDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(ApplicationDbContext));
            if (dbContextServiceDescriptor != null)
            {
                services.Remove(dbContextServiceDescriptor);
            }

            if (_useRealDatabase)
            {
                // Use SQL Server for realistic E2E testing
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ACS_E2E_Test;Trusted_Connection=true;MultipleActiveResultSets=true");
                    options.EnableSensitiveDataLogging(true);
                    options.EnableDetailedErrors(true);
                });
            }
            else
            {
                // Use in-memory database for faster, isolated tests
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase($"E2ETestDb_{Guid.NewGuid()}");
                    options.EnableSensitiveDataLogging(true);
                    options.EnableDetailedErrors(true);
                });
            }
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.AddDebug();
            logging.SetMinimumLevel(LogLevel.Information);
        });

        builder.UseEnvironment("Testing");
    }

    public async Task<ApplicationDbContext> GetDbContextAsync()
    {
        var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        if (_useRealDatabase)
        {
            await context.Database.EnsureCreatedAsync();
        }
        
        return context;
    }

    public async Task SeedE2ETestDataAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Clear existing data if any
        if (context.Users.Any())
        {
            context.Users.RemoveRange(context.Users);
            context.Groups.RemoveRange(context.Groups);
            context.Roles.RemoveRange(context.Roles);
            await context.SaveChangesAsync();
        }

        // Seed comprehensive test data for E2E scenarios
        await SeedVerbTypesAsync(context);
        await SeedRolesAsync(context);
        await SeedGroupsAsync(context);
        await SeedUsersAsync(context);
        await SeedResourcesAsync(context);
        await SeedPermissionsAsync(context);

        await context.SaveChangesAsync();
        Console.WriteLine("E2E test data seeded successfully");
    }

    private Task SeedVerbTypesAsync(ApplicationDbContext context)
    {
        if (!context.VerbTypes.Any())
        {
            var verbTypes = new[]
            {
                new Service.Data.Models.VerbType { Id = 1, VerbName = "GET" },
                new Service.Data.Models.VerbType { Id = 2, VerbName = "POST" },
                new Service.Data.Models.VerbType { Id = 3, VerbName = "PUT" },
                new Service.Data.Models.VerbType { Id = 4, VerbName = "DELETE" }
            };

            context.VerbTypes.AddRange(verbTypes);
        }
        return Task.CompletedTask;
    }

    private Task SeedRolesAsync(ApplicationDbContext context)
    {
        var roles = new[]
        {
            new Service.Data.Models.Role
            {
                Id = 1,
                Name = "Administrator",
                EntityId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Service.Data.Models.Role
            {
                Id = 2,
                Name = "Manager",
                EntityId = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Service.Data.Models.Role
            {
                Id = 3,
                Name = "Employee", 
                EntityId = 3,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Service.Data.Models.Role
            {
                Id = 4,
                Name = "Guest",
                EntityId = 4,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        context.Roles.AddRange(roles);
        return Task.CompletedTask;
    }

    private Task SeedGroupsAsync(ApplicationDbContext context)
    {
        var groups = new[]
        {
            new Service.Data.Models.Group
            {
                Id = 1,
                Name = "IT Department",
                EntityId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Service.Data.Models.Group
            {
                Id = 2,
                Name = "Development Team",
                EntityId = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Service.Data.Models.Group
            {
                Id = 3,
                Name = "QA Team",
                EntityId = 3,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Service.Data.Models.Group
            {
                Id = 4,
                Name = "HR Department",
                EntityId = 4,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Service.Data.Models.Group
            {
                Id = 5,
                Name = "Finance Department",
                EntityId = 5,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        context.Groups.AddRange(groups);
        return Task.CompletedTask;
    }

    private Task SeedUsersAsync(ApplicationDbContext context)
    {
        var users = new[]
        {
            new Service.Data.Models.User
            {
                Id = 1,
                Name = "System Administrator",
                Email = "admin@company.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!")
            },
            new Service.Data.Models.User
            {
                Id = 2,
                Name = "John Smith", 
                Email = "john.smith@company.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Manager123!")
            },
            new Service.Data.Models.User
            {
                Id = 3,
                Name = "Alice Johnson",
                Email = "alice.johnson@company.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Employee123!"),
            },
            new Service.Data.Models.User
            {
                Id = 4,
                Name = "Bob Wilson",
                Email = "bob.wilson@company.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Employee123!"),
            },
            new Service.Data.Models.User
            {
                Id = 5,
                Name = "Carol Davis",
                Email = "carol.davis@company.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Employee123!"),
            },
            new Service.Data.Models.User
            {
                Id = 6,
                Name = "David Brown",
                Email = "david.brown@company.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Employee123!"),
                EntityId = 6,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                UpdatedAt = DateTime.UtcNow.AddDays(-5)
            }
        };

        context.Users.AddRange(users);
        return Task.CompletedTask;
    }

    private Task SeedResourcesAsync(ApplicationDbContext context)
    {
        var resources = new[]
        {
            new Service.Data.Models.Resource
            {
                Id = 1,
                Uri = "/api/users",
                Description = "User management API",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Service.Data.Models.Resource
            {
                Id = 2,
                Uri = "/api/groups",
                Description = "Group management API",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Service.Data.Models.Resource
            {
                Id = 3,
                Uri = "/api/roles",
                Description = "Role management API",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Service.Data.Models.Resource
            {
                Id = 4,
                Uri = "/api/admin",
                Description = "Administrative functions",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        context.Resources.AddRange(resources);
        return Task.CompletedTask;
    }

    private async Task SeedPermissionsAsync(ApplicationDbContext context)
    {
        // TODO: Permission scheme seeding disabled due to schema changes
        // The PermissionScheme model has been updated to use UriAccessId and SchemeTypeId
        // instead of ResourceId and VerbTypeId. This needs to be updated to match the new schema.
        
        /* 
        // Create permission schemes for roles
        var permissionSchemes = new List<Service.Data.Models.PermissionScheme>();

        // Administrator permissions - full access
        permissionSchemes.AddRange(new[]
        {
            new Service.Data.Models.PermissionScheme { EntityId = 1, ResourceId = 1, VerbTypeId = 1 }, // Admin -> Users GET
            new Service.Data.Models.PermissionScheme { EntityId = 1, ResourceId = 1, VerbTypeId = 2 }, // Admin -> Users POST
            new Service.Data.Models.PermissionScheme { EntityId = 1, ResourceId = 1, VerbTypeId = 3 }, // Admin -> Users PUT
            new Service.Data.Models.PermissionScheme { EntityId = 1, ResourceId = 1, VerbTypeId = 4 }, // Admin -> Users DELETE
            new Service.Data.Models.PermissionScheme { EntityId = 1, ResourceId = 2, VerbTypeId = 1 }, // Admin -> Groups GET
            new Service.Data.Models.PermissionScheme { EntityId = 1, ResourceId = 2, VerbTypeId = 2 }, // Admin -> Groups POST
            new Service.Data.Models.PermissionScheme { EntityId = 1, ResourceId = 2, VerbTypeId = 3 }, // Admin -> Groups PUT
            new Service.Data.Models.PermissionScheme { EntityId = 1, ResourceId = 2, VerbTypeId = 4 }, // Admin -> Groups DELETE
            new Service.Data.Models.PermissionScheme { EntityId = 1, ResourceId = 3, VerbTypeId = 1 }, // Admin -> Roles GET
            new Service.Data.Models.PermissionScheme { EntityId = 1, ResourceId = 3, VerbTypeId = 2 }, // Admin -> Roles POST
            new Service.Data.Models.PermissionScheme { EntityId = 1, ResourceId = 3, VerbTypeId = 3 }, // Admin -> Roles PUT
            new Service.Data.Models.PermissionScheme { EntityId = 1, ResourceId = 3, VerbTypeId = 4 }, // Admin -> Roles DELETE
            new Service.Data.Models.PermissionScheme { EntityId = 1, ResourceId = 4, VerbTypeId = 1 }, // Admin -> Admin GET
            new Service.Data.Models.PermissionScheme { EntityId = 1, ResourceId = 4, VerbTypeId = 2 }, // Admin -> Admin POST
            new Service.Data.Models.PermissionScheme { EntityId = 1, ResourceId = 4, VerbTypeId = 3 }, // Admin -> Admin PUT
            new Service.Data.Models.PermissionScheme { EntityId = 1, ResourceId = 4, VerbTypeId = 4 }  // Admin -> Admin DELETE
        });

        // Manager permissions - limited admin access
        permissionSchemes.AddRange(new[]
        {
            new Service.Data.Models.PermissionScheme { EntityId = 2, ResourceId = 1, VerbTypeId = 1 }, // Manager -> Users GET
            new Service.Data.Models.PermissionScheme { EntityId = 2, ResourceId = 1, VerbTypeId = 2 }, // Manager -> Users POST
            new Service.Data.Models.PermissionScheme { EntityId = 2, ResourceId = 1, VerbTypeId = 3 }, // Manager -> Users PUT
            new Service.Data.Models.PermissionScheme { EntityId = 2, ResourceId = 2, VerbTypeId = 1 }, // Manager -> Groups GET
            new Service.Data.Models.PermissionScheme { EntityId = 2, ResourceId = 2, VerbTypeId = 3 }, // Manager -> Groups PUT
            new Service.Data.Models.PermissionScheme { EntityId = 2, ResourceId = 3, VerbTypeId = 1 }  // Manager -> Roles GET
        });

        // Employee permissions - read-only access
        permissionSchemes.AddRange(new[]
        {
            new Service.Data.Models.PermissionScheme { EntityId = 3, ResourceId = 1, VerbTypeId = 1 }, // Employee -> Users GET
            new Service.Data.Models.PermissionScheme { EntityId = 3, ResourceId = 2, VerbTypeId = 1 }, // Employee -> Groups GET
            new Service.Data.Models.PermissionScheme { EntityId = 3, ResourceId = 3, VerbTypeId = 1 }  // Employee -> Roles GET
        });

        // Guest permissions - very limited access
        permissionSchemes.AddRange(new[]
        {
            new Service.Data.Models.PermissionScheme { EntityId = 4, ResourceId = 1, VerbTypeId = 1 }  // Guest -> Users GET (own profile only)
        });

        context.PermissionSchemes.AddRange(permissionSchemes);
        */
        
        await Task.CompletedTask; // Placeholder for disabled permission seeding
    }
}