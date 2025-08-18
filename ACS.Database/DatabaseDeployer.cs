using Microsoft.Data.SqlClient;
using System.Reflection;

namespace ACS.Database;

/// <summary>
/// Utility class for deploying database schema and data
/// </summary>
public static class DatabaseDeployer
{
    /// <summary>
    /// Deploy database schema and seed data
    /// </summary>
    /// <param name="connectionString">SQL Server connection string</param>
    /// <param name="recreateDatabase">Whether to drop and recreate the database</param>
    public static async Task DeployAsync(string connectionString, bool recreateDatabase = false)
    {
        var scripts = await GetSqlScriptsAsync();
        
        if (recreateDatabase)
        {
            await RecreateDatabase(connectionString);
        }
        
        await ExecuteScripts(connectionString, scripts);
    }
    
    /// <summary>
    /// Get embedded SQL script files in execution order
    /// </summary>
    private static async Task<Dictionary<string, string>> GetSqlScriptsAsync()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var scripts = new Dictionary<string, string>();
        
        // Define execution order for schema creation
        var scriptOrder = new[]
        {
            "Tables/Entity.sql",
            "Tables/User.sql", 
            "Tables/Group.sql",
            "Tables/Role.sql",
            "Tables/Permission.sql",
            "Tables/PermissionScheme.sql",
            "Tables/Resource.sql",
            "Tables/UriAccess.sql",
            "Tables/VerbType.sql",
            "Tables/AuditLog.sql",
            "Data/SeedData.sql"
        };
        
        foreach (var scriptPath in scriptOrder)
        {
            var resourceName = $"ACS.Database.{scriptPath.Replace('/', '.')}";
            var stream = assembly.GetManifestResourceStream(resourceName);
            
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                scripts[scriptPath] = reader.ReadToEnd();
            }
            else
            {
                // Try to load from file system if embedded resource not found
                var filePath = Path.Combine(GetAssemblyDirectory(), scriptPath);
                if (File.Exists(filePath))
                {
                    scripts[scriptPath] = await File.ReadAllTextAsync(filePath);
                }
            }
        }
        
        return scripts;
    }
    
    /// <summary>
    /// Execute SQL scripts in order
    /// </summary>
    private static async Task ExecuteScripts(string connectionString, Dictionary<string, string> scripts)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        
        foreach (var script in scripts)
        {
            try
            {
                Console.WriteLine($"Executing script: {script.Key}");
                
                // Split script by GO statements
                var batches = script.Value.Split(new[] { "\nGO\n", "\rGO\r", "\r\nGO\r\n" }, 
                    StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var batch in batches)
                {
                    if (!string.IsNullOrWhiteSpace(batch))
                    {
                        using var command = new SqlCommand(batch.Trim(), connection);
                        command.CommandTimeout = 300; // 5 minutes timeout
                        await command.ExecuteNonQueryAsync();
                    }
                }
                
                Console.WriteLine($"✓ Successfully executed: {script.Key}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error executing {script.Key}: {ex.Message}");
                throw;
            }
        }
    }
    
    /// <summary>
    /// Recreate database (drop and create)
    /// </summary>
    private static async Task RecreateDatabase(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;
        
        // Connect to master database
        builder.InitialCatalog = "master";
        
        using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        
        // Drop database if exists
        var dropScript = $@"
            IF EXISTS (SELECT name FROM sys.databases WHERE name = N'{databaseName}')
            BEGIN
                ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{databaseName}];
            END";
            
        using var dropCommand = new SqlCommand(dropScript, connection);
        await dropCommand.ExecuteNonQueryAsync();
        
        // Create database
        var createScript = $"CREATE DATABASE [{databaseName}]";
        using var createCommand = new SqlCommand(createScript, connection);
        await createCommand.ExecuteNonQueryAsync();
        
        Console.WriteLine($"✓ Database {databaseName} recreated successfully");
    }
    
    /// <summary>
    /// Get directory where assembly is located
    /// </summary>
    private static string GetAssemblyDirectory()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var location = assembly.Location;
        return Path.GetDirectoryName(location) ?? Environment.CurrentDirectory;
    }
}