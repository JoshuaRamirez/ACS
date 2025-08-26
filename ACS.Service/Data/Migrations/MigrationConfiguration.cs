using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ACS.Service.Data.Migrations;

/// <summary>
/// Design-time factory for creating ApplicationDbContext for migrations
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // Build configuration
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .SetBasePath(System.IO.Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Create options builder
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        
        // Get connection string
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Server=(localdb)\\mssqllocaldb;Database=ACS;Trusted_Connection=True;MultipleActiveResultSets=true";
        
        // Configure SQL Server with migrations assembly
        optionsBuilder.UseSqlServer(connectionString, options =>
        {
            options.MigrationsAssembly("ACS.Service");
            options.CommandTimeout(60);
            options.EnableRetryOnFailure(3);
        });

        // Enable sensitive data logging in development
        if (configuration.GetValue<string>("Environment") == "Development")
        {
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
        }

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}

/// <summary>
/// Migration utilities for database management
/// </summary>
public static class MigrationUtilities
{
    /// <summary>
    /// Apply pending migrations
    /// </summary>
    public static async Task ApplyMigrationsAsync(ApplicationDbContext context)
    {
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            Console.WriteLine($"Applying {pendingMigrations.Count()} pending migrations...");
            await context.Database.MigrateAsync();
            Console.WriteLine("Migrations applied successfully.");
        }
        else
        {
            Console.WriteLine("Database is up to date.");
        }
    }

    /// <summary>
    /// Check if database exists and can connect
    /// </summary>
    public static async Task<bool> CanConnectAsync(ApplicationDbContext context)
    {
        try
        {
            return await context.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Ensure database is created and migrated
    /// </summary>
    public static async Task EnsureDatabaseAsync(ApplicationDbContext context)
    {
        // Check if we can connect
        if (!await CanConnectAsync(context))
        {
            Console.WriteLine("Cannot connect to database. Creating database...");
            await context.Database.EnsureCreatedAsync();
        }

        // Apply migrations
        await ApplyMigrationsAsync(context);
    }

    /// <summary>
    /// Get migration history
    /// </summary>
    public static async Task<IEnumerable<string>> GetAppliedMigrationsAsync(ApplicationDbContext context)
    {
        return await context.Database.GetAppliedMigrationsAsync();
    }

    /// <summary>
    /// Rollback to specific migration
    /// </summary>
    public static async Task RollbackToMigrationAsync(ApplicationDbContext context, string targetMigration)
    {
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        
        using var command = connection.CreateCommand();
        command.CommandText = $@"
            DELETE FROM __EFMigrationsHistory 
            WHERE MigrationId > '{targetMigration}'";
        
        await command.ExecuteNonQueryAsync();
        await connection.CloseAsync();
        
        // Note: This removes migration history but doesn't revert schema changes
        // Manual intervention or down migrations would be needed for full rollback
    }
}