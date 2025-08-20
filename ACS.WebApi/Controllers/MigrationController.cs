using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ACS.Service.Data;
using ACS.Service.Services;

namespace ACS.WebApi.Controllers;

/// <summary>
/// Controller for database migration management
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrator")]
public class MigrationController : ControllerBase
{
    private readonly IMigrationValidationService _migrationService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<MigrationController> _logger;

    public MigrationController(
        IMigrationValidationService migrationService,
        ApplicationDbContext dbContext,
        ILogger<MigrationController> logger)
    {
        _migrationService = migrationService;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Get migration history
    /// </summary>
    /// <returns>List of all migrations and their status</returns>
    [HttpGet("history")]
    public async Task<IActionResult> GetMigrationHistory()
    {
        try
        {
            var history = await _migrationService.GetMigrationHistoryAsync();
            
            return Ok(new
            {
                TotalMigrations = history.Count,
                AppliedMigrations = history.Count(m => m.IsApplied),
                PendingMigrations = history.Count(m => !m.IsApplied),
                Migrations = history.Select(m => new
                {
                    m.MigrationId,
                    m.IsApplied,
                    m.AppliedDate,
                    m.ProductVersion,
                    Name = GetFriendlyMigrationName(m.MigrationId)
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving migration history");
            return StatusCode(500, new { error = "Failed to retrieve migration history", message = ex.Message });
        }
    }

    /// <summary>
    /// Get pending migrations
    /// </summary>
    /// <returns>List of migrations that haven't been applied yet</returns>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingMigrations()
    {
        try
        {
            var pending = await _migrationService.GetPendingMigrationsAsync();
            
            return Ok(new
            {
                Count = pending.Count,
                TotalEstimatedDuration = pending
                    .Where(m => m.EstimatedDuration.HasValue)
                    .Sum(m => m.EstimatedDuration.Value.TotalMinutes),
                RequiresDowntime = pending.Any(m => m.RequiresDowntime),
                RequiresBackup = pending.Any(m => m.RequiresBackup),
                Migrations = pending.Select(m => new
                {
                    m.MigrationId,
                    Name = GetFriendlyMigrationName(m.MigrationId),
                    m.EstimatedDuration,
                    m.RequiresDowntime,
                    m.RequiresBackup,
                    ValidationSummary = new
                    {
                        m.ValidationResult.Success,
                        ErrorCount = m.ValidationResult.Errors.Count,
                        WarningCount = m.ValidationResult.Warnings.Count,
                        m.ValidationResult.Errors,
                        m.ValidationResult.Warnings
                    }
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending migrations");
            return StatusCode(500, new { error = "Failed to retrieve pending migrations", message = ex.Message });
        }
    }

    /// <summary>
    /// Validate a specific migration
    /// </summary>
    /// <param name="migrationName">Name of the migration to validate</param>
    /// <returns>Validation result</returns>
    [HttpPost("validate/{migrationName}")]
    public async Task<IActionResult> ValidateMigration(string migrationName)
    {
        try
        {
            _logger.LogInformation("Validation requested for migration: {MigrationName}", migrationName);
            
            var result = await _migrationService.ValidateMigrationAsync(migrationName);
            
            return Ok(new
            {
                result.MigrationName,
                result.Success,
                result.Errors,
                result.Warnings,
                result.ValidationChecks,
                result.EstimatedDuration,
                result.AffectedTables,
                result.AffectedRows,
                result.RequiresDowntime,
                result.RequiresBackup,
                result.ValidationTime,
                FriendlyName = GetFriendlyMigrationName(migrationName)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating migration {MigrationName}", migrationName);
            return StatusCode(500, new { error = "Failed to validate migration", message = ex.Message });
        }
    }

    /// <summary>
    /// Test a migration in an isolated environment
    /// </summary>
    /// <param name="migrationName">Name of the migration to test</param>
    /// <returns>Test result</returns>
    [HttpPost("test/{migrationName}")]
    public async Task<IActionResult> TestMigration(string migrationName)
    {
        try
        {
            _logger.LogInformation("Test requested for migration: {MigrationName}", migrationName);
            
            var result = await _migrationService.TestMigrationAsync(migrationName);
            
            return Ok(new
            {
                result.MigrationName,
                result.Success,
                result.MigrationSucceeded,
                result.RollbackSucceeded,
                MigrationDurationMs = result.MigrationDuration?.TotalMilliseconds,
                RollbackDurationMs = result.RollbackDuration?.TotalMilliseconds,
                result.ValidationResults,
                PerformanceImpact = result.PerformanceImpact != null ? new
                {
                    result.PerformanceImpact.QueryTimeIncrease,
                    result.PerformanceImpact.IndexFragmentation,
                    result.PerformanceImpact.AdditionalStorageBytes
                } : null,
                result.Errors,
                TestDurationMs = result.TestDuration.TotalMilliseconds
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing migration {MigrationName}", migrationName);
            return StatusCode(500, new { error = "Failed to test migration", message = ex.Message });
        }
    }

    /// <summary>
    /// Apply pending migrations
    /// </summary>
    /// <param name="request">Migration application request</param>
    /// <returns>Application result</returns>
    [HttpPost("apply")]
    public async Task<IActionResult> ApplyMigrations([FromBody] ApplyMigrationsRequest request)
    {
        try
        {
            if (!request.ConfirmApply)
            {
                return BadRequest(new { error = "Migration application must be confirmed" });
            }

            _logger.LogWarning("Migration application requested. Target: {TargetMigration}", 
                request.TargetMigration ?? "Latest");

            var startTime = DateTime.UtcNow;
            var appliedBefore = await _dbContext.Database.GetAppliedMigrationsAsync();

            // Apply migrations
            if (string.IsNullOrEmpty(request.TargetMigration))
            {
                await _dbContext.Database.MigrateAsync();
            }
            else
            {
                var migrator = _dbContext.GetService<IMigrator>();
                await migrator.MigrateAsync(request.TargetMigration);
            }

            var appliedAfter = await _dbContext.Database.GetAppliedMigrationsAsync();
            var newMigrations = appliedAfter.Except(appliedBefore).ToList();

            return Ok(new
            {
                Success = true,
                AppliedMigrations = newMigrations,
                Count = newMigrations.Count,
                Duration = (DateTime.UtcNow - startTime).TotalSeconds,
                Message = $"Successfully applied {newMigrations.Count} migration(s)"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying migrations");
            return StatusCode(500, new { error = "Failed to apply migrations", message = ex.Message });
        }
    }

    /// <summary>
    /// Rollback to a specific migration
    /// </summary>
    /// <param name="request">Rollback request</param>
    /// <returns>Rollback result</returns>
    [HttpPost("rollback")]
    public async Task<IActionResult> RollbackMigration([FromBody] RollbackRequest request)
    {
        try
        {
            if (!request.ConfirmRollback)
            {
                return BadRequest(new { error = "Rollback must be confirmed" });
            }

            _logger.LogWarning("Migration rollback requested to: {TargetMigration}", request.TargetMigration);
            
            var result = await _migrationService.RollbackMigrationAsync(request.TargetMigration);
            
            return Ok(new
            {
                result.Success,
                result.Error,
                result.TargetMigration,
                result.FromMigration,
                result.RolledBackMigrations,
                result.BackupPath,
                result.RestoredFromBackup,
                result.Warnings,
                RollbackDurationMs = result.RollbackDuration.TotalMilliseconds
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rolling back migration");
            return StatusCode(500, new { error = "Failed to rollback migration", message = ex.Message });
        }
    }

    /// <summary>
    /// Check migration health
    /// </summary>
    /// <returns>Migration health status</returns>
    [HttpGet("health")]
    public async Task<IActionResult> CheckMigrationHealth()
    {
        try
        {
            var health = await _migrationService.CheckMigrationHealthAsync();
            
            return Ok(new
            {
                health.IsHealthy,
                health.DatabaseConnected,
                health.MigrationHistoryTableExists,
                health.HasPendingMigrations,
                PendingCount = health.PendingMigrations.Count,
                health.PendingMigrations,
                health.HasConflicts,
                health.Conflicts,
                DatabaseIntegrity = health.DatabaseIntegrity != null ? new
                {
                    health.DatabaseIntegrity.IsHealthy,
                    health.DatabaseIntegrity.Issues
                } : null,
                health.Errors,
                health.CheckTime
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking migration health");
            return StatusCode(500, new { error = "Failed to check migration health", message = ex.Message });
        }
    }

    /// <summary>
    /// Create a migration checkpoint
    /// </summary>
    /// <param name="request">Checkpoint creation request</param>
    /// <returns>Checkpoint creation result</returns>
    [HttpPost("checkpoint")]
    public async Task<IActionResult> CreateCheckpoint([FromBody] CreateCheckpointRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Description))
            {
                return BadRequest(new { error = "Description is required" });
            }

            _logger.LogInformation("Creating migration checkpoint: {Description}", request.Description);
            
            var success = await _migrationService.CreateMigrationCheckpointAsync(request.Description);
            
            if (success)
            {
                return Ok(new 
                { 
                    success = true, 
                    message = "Checkpoint created successfully",
                    description = request.Description,
                    createdAt = DateTime.UtcNow
                });
            }
            else
            {
                return StatusCode(500, new 
                { 
                    success = false, 
                    message = "Failed to create checkpoint" 
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating checkpoint");
            return StatusCode(500, new { error = "Failed to create checkpoint", message = ex.Message });
        }
    }

    /// <summary>
    /// Get migration checkpoints
    /// </summary>
    /// <returns>List of available checkpoints</returns>
    [HttpGet("checkpoints")]
    public async Task<IActionResult> GetCheckpoints()
    {
        try
        {
            var checkpoints = await _migrationService.GetCheckpointsAsync();
            
            return Ok(new
            {
                Count = checkpoints.Count,
                Checkpoints = checkpoints.Select(c => new
                {
                    c.CheckpointId,
                    c.Description,
                    c.CreatedAt,
                    c.BackupPath,
                    c.CurrentMigration,
                    FriendlyMigrationName = GetFriendlyMigrationName(c.CurrentMigration),
                    c.Metadata
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving checkpoints");
            return StatusCode(500, new { error = "Failed to retrieve checkpoints", message = ex.Message });
        }
    }

    /// <summary>
    /// Get current database version
    /// </summary>
    /// <returns>Current database version information</returns>
    [HttpGet("version")]
    public async Task<IActionResult> GetDatabaseVersion()
    {
        try
        {
            var appliedMigrations = await _dbContext.Database.GetAppliedMigrationsAsync();
            var pendingMigrations = await _dbContext.Database.GetPendingMigrationsAsync();
            var currentMigration = appliedMigrations.LastOrDefault();

            return Ok(new
            {
                CurrentMigration = currentMigration,
                FriendlyName = GetFriendlyMigrationName(currentMigration),
                AppliedCount = appliedMigrations.Count(),
                PendingCount = pendingMigrations.Count(),
                DatabaseProvider = _dbContext.Database.ProviderName,
                ConnectionString = MaskConnectionString(_dbContext.Database.GetConnectionString())
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting database version");
            return StatusCode(500, new { error = "Failed to get database version", message = ex.Message });
        }
    }

    private string GetFriendlyMigrationName(string migrationId)
    {
        if (string.IsNullOrEmpty(migrationId))
            return "None";

        // Extract meaningful name from migration ID
        // Format is typically: 20240101123456_MigrationName
        var parts = migrationId.Split('_', 2);
        if (parts.Length == 2)
        {
            var name = parts[1];
            // Add spaces before capital letters
            return System.Text.RegularExpressions.Regex.Replace(name, "([A-Z])", " $1").Trim();
        }

        return migrationId;
    }

    private string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return "Not configured";

        // Mask sensitive parts of connection string
        var builder = new System.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
        
        if (!string.IsNullOrEmpty(builder.Password))
        {
            builder.Password = "****";
        }

        return builder.ConnectionString;
    }
}

#region Request Models

public class ApplyMigrationsRequest
{
    public string TargetMigration { get; set; }
    public bool ConfirmApply { get; set; }
}

public class RollbackRequest
{
    public string TargetMigration { get; set; }
    public bool ConfirmRollback { get; set; }
}

public class CreateCheckpointRequest
{
    public string Description { get; set; }
}

#endregion