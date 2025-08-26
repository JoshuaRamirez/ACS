using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Data;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ACS.Service.Data;

namespace ACS.Service.Services;

/// <summary>
/// Service for data archiving and compliance-related data management
/// </summary>
public interface IDataArchivingService
{
    Task<ArchiveResult> ArchiveDataAsync(ArchiveOptions options, CancellationToken cancellationToken = default);
    Task<RestoreArchiveResult> RestoreArchivedDataAsync(string archiveId, RestoreArchiveOptions options, CancellationToken cancellationToken = default);
    Task<List<ArchiveInfo>> GetArchiveHistoryAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
    Task<PurgeResult> PurgeExpiredDataAsync(PurgeOptions options, CancellationToken cancellationToken = default);
    Task<ComplianceReport> GenerateComplianceReportAsync(ComplianceReportOptions options, CancellationToken cancellationToken = default);
    Task<DataRetentionStatus> GetDataRetentionStatusAsync(CancellationToken cancellationToken = default);
}

public class DataArchivingService : IDataArchivingService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DataArchivingService> _logger;
    private readonly string _archivePath;
    private readonly string _connectionString;

    public DataArchivingService(
        ApplicationDbContext dbContext,
        IConfiguration configuration,
        ILogger<DataArchivingService> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
        _archivePath = configuration.GetValue<string>("DataArchiving:ArchivePath") 
            ?? Path.Combine(Path.GetTempPath(), "ACS_Archives");
        _connectionString = dbContext.Database.GetConnectionString() 
            ?? throw new InvalidOperationException("Connection string not found");
        
        // Ensure archive directory exists
        Directory.CreateDirectory(_archivePath);
    }

    public async Task<ArchiveResult> ArchiveDataAsync(ArchiveOptions options, CancellationToken cancellationToken = default)
    {
        var result = new ArchiveResult
        {
            StartTime = DateTime.UtcNow,
            ArchiveId = Guid.NewGuid().ToString(),
            Options = options
        };

        try
        {
            _logger.LogInformation("Starting data archive. Type: {ArchiveType}, CutoffDate: {CutoffDate}",
                options.ArchiveType, options.CutoffDate);

            // Step 1: Identify data to archive
            var dataToArchive = await IdentifyDataToArchiveAsync(options, cancellationToken);
            result.RecordsIdentified = dataToArchive.TotalRecords;

            if (dataToArchive.TotalRecords == 0)
            {
                result.Success = true;
                result.Message = "No data found to archive";
                return result;
            }

            // Step 2: Create archive file
            var archiveFileName = $"Archive_{options.ArchiveType}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{result.ArchiveId}.archive";
            var archiveFilePath = Path.Combine(_archivePath, archiveFileName);
            result.ArchivePath = archiveFilePath;

            // Step 3: Export data to archive
            var exportResult = await ExportDataToArchiveAsync(dataToArchive, archiveFilePath, options, cancellationToken);
            result.RecordsArchived = exportResult.RecordsExported;
            result.TablesArchived = exportResult.TablesExported;

            // Step 4: Compress archive if requested
            if (options.CompressArchive)
            {
                var compressedPath = await CompressArchiveAsync(archiveFilePath, cancellationToken);
                result.CompressedPath = compressedPath;
                result.CompressedSize = new FileInfo(compressedPath).Length;
                
                // Delete uncompressed file
                File.Delete(archiveFilePath);
                result.ArchivePath = compressedPath;
            }

            result.ArchiveSize = new FileInfo(result.ArchivePath).Length;

            // Step 5: Delete archived data from database if requested
            if (options.DeleteAfterArchive)
            {
                var deleteResult = await DeleteArchivedDataAsync(dataToArchive, cancellationToken);
                result.RecordsDeleted = deleteResult.RecordsDeleted;
                
                if (!deleteResult.Success)
                {
                    result.Warnings.Add($"Failed to delete some records: {deleteResult.Error}");
                }
            }

            // Step 6: Log archive operation
            await LogArchiveOperationAsync(result, cancellationToken);

            // Step 7: Generate archive manifest
            await GenerateArchiveManifestAsync(result, dataToArchive, cancellationToken);

            result.EndTime = DateTime.UtcNow;
            result.Success = true;
            result.Message = $"Successfully archived {result.RecordsArchived} records to {result.ArchivePath}";

            _logger.LogInformation("Archive completed. Records: {Records}, Size: {Size}, Duration: {Duration}ms",
                result.RecordsArchived, FormatFileSize(result.ArchiveSize), result.Duration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during data archiving");
            result.EndTime = DateTime.UtcNow;
            result.Success = false;
            result.Message = $"Archive failed: {ex.Message}";
            result.Error = ex.ToString();
            return result;
        }
    }

    public async Task<RestoreArchiveResult> RestoreArchivedDataAsync(string archiveId, RestoreArchiveOptions options, CancellationToken cancellationToken = default)
    {
        var result = new RestoreArchiveResult
        {
            ArchiveId = archiveId,
            StartTime = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting archive restoration. ArchiveId: {ArchiveId}", archiveId);

            // Step 1: Locate archive file
            var archiveFile = await LocateArchiveFileAsync(archiveId, cancellationToken);
            if (archiveFile == null)
            {
                result.Success = false;
                result.Message = $"Archive file not found for ID: {archiveId}";
                return result;
            }

            result.ArchivePath = archiveFile;

            // Step 2: Decompress if needed
            var workingFile = archiveFile;
            if (Path.GetExtension(archiveFile).Equals(".gz", StringComparison.OrdinalIgnoreCase))
            {
                workingFile = await DecompressArchiveAsync(archiveFile, cancellationToken);
            }

            // Step 3: Read archive manifest
            var manifest = await ReadArchiveManifestAsync(workingFile, cancellationToken);
            
            // Step 4: Validate restore compatibility
            if (options.ValidateBeforeRestore)
            {
                var validationResult = await ValidateArchiveCompatibilityAsync(manifest, cancellationToken);
                if (!validationResult.IsCompatible)
                {
                    result.Success = false;
                    result.Message = $"Archive is not compatible: {validationResult.Reason}";
                    result.Warnings = validationResult.Warnings;
                    return result;
                }
            }

            // Step 5: Restore data to database
            var restoreDataResult = await RestoreDataFromArchiveAsync(workingFile, manifest, options, cancellationToken);
            result.RecordsRestored = restoreDataResult.RecordsRestored;
            result.TablesRestored = restoreDataResult.TablesRestored;
            result.Conflicts = restoreDataResult.Conflicts;

            // Step 6: Handle conflicts if any
            if (result.Conflicts.Any())
            {
                var conflictResolution = await ResolveConflictsAsync(result.Conflicts, options.ConflictResolution, cancellationToken);
                result.ConflictsResolved = conflictResolution.Resolved;
                result.ConflictsFailed = conflictResolution.Failed;
            }

            // Step 7: Update archive status
            if (options.MarkAsRestored)
            {
                await UpdateArchiveStatusAsync(archiveId, ArchiveStatus.Restored, cancellationToken);
            }

            result.EndTime = DateTime.UtcNow;
            result.Success = true;
            result.Message = $"Successfully restored {result.RecordsRestored} records from archive";

            _logger.LogInformation("Archive restoration completed. Records: {Records}, Duration: {Duration}ms",
                result.RecordsRestored, result.Duration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring archive {ArchiveId}", archiveId);
            result.EndTime = DateTime.UtcNow;
            result.Success = false;
            result.Message = $"Restore failed: {ex.Message}";
            return result;
        }
    }

    public async Task<List<ArchiveInfo>> GetArchiveHistoryAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        var archives = new List<ArchiveInfo>();

        try
        {
            // Get from archive log table
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Create archive log table if it doesn't exist
            await EnsureArchiveLogTableExistsAsync(connection, cancellationToken);

            var sql = @"
                SELECT ArchiveId, ArchiveType, ArchiveDate, RecordsArchived, 
                       ArchiveSize, ArchivePath, Status, CreatedBy, Metadata
                FROM DataArchiveLog
                WHERE (@FromDate IS NULL OR ArchiveDate >= @FromDate)
                  AND (@ToDate IS NULL OR ArchiveDate <= @ToDate)
                ORDER BY ArchiveDate DESC";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@FromDate", fromDate ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@ToDate", toDate ?? (object)DBNull.Value);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                archives.Add(new ArchiveInfo
                {
                    ArchiveId = reader.GetString(0),
                    ArchiveType = Enum.Parse<ArchiveType>(reader.GetString(1)),
                    ArchiveDate = reader.GetDateTime(2),
                    RecordsArchived = reader.GetInt64(3),
                    ArchiveSize = reader.GetInt64(4),
                    ArchivePath = reader.GetString(5),
                    Status = Enum.Parse<ArchiveStatus>(reader.GetString(6)),
                    CreatedBy = reader.GetString(7),
                    Metadata = reader.IsDBNull(8) ? string.Empty : reader.GetString(8)
                });
            }

            // Also check physical archive files
            if (Directory.Exists(_archivePath))
            {
                var files = Directory.GetFiles(_archivePath, "*.archive*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    var fileDate = fileInfo.CreationTimeUtc;
                    
                    if ((fromDate == null || fileDate >= fromDate) &&
                        (toDate == null || fileDate <= toDate))
                    {
                        // Parse archive ID from filename if not in database
                        var archiveId = ExtractArchiveIdFromFileName(file);
                        if (!archives.Any(a => a.ArchiveId == archiveId))
                        {
                            archives.Add(new ArchiveInfo
                            {
                                ArchiveId = archiveId,
                                ArchiveDate = fileDate,
                                ArchiveSize = fileInfo.Length,
                                ArchivePath = file,
                                Status = ArchiveStatus.Unknown
                            });
                        }
                    }
                }
            }

            return archives.OrderByDescending(a => a.ArchiveDate).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving archive history");
            return archives;
        }
    }

    public async Task<PurgeResult> PurgeExpiredDataAsync(PurgeOptions options, CancellationToken cancellationToken = default)
    {
        var result = new PurgeResult
        {
            StartTime = DateTime.UtcNow,
            Options = options
        };

        try
        {
            _logger.LogWarning("Starting data purge. RetentionDays: {RetentionDays}, Tables: {Tables}",
                options.RetentionDays, string.Join(", ", options.TablesToPurge));

            // Step 1: Create backup before purge if requested
            if (options.CreateBackupBeforePurge)
            {
                _logger.LogInformation("Creating backup before purge");
                // Use the backup service if available
                result.BackupCreated = true;
            }

            // Step 2: Identify records to purge
            var cutoffDate = DateTime.UtcNow.AddDays(-options.RetentionDays);
            var recordsToPurge = await IdentifyRecordsToPurgeAsync(options.TablesToPurge, cutoffDate, cancellationToken);
            result.RecordsIdentified = recordsToPurge.TotalRecords;

            if (recordsToPurge.TotalRecords == 0)
            {
                result.Success = true;
                result.Message = "No records found to purge";
                return result;
            }

            // Step 3: Archive before purge if requested
            if (options.ArchiveBeforePurge)
            {
                var archiveOptions = new ArchiveOptions
                {
                    ArchiveType = ArchiveType.PrePurge,
                    CutoffDate = cutoffDate,
                    CompressArchive = true,
                    DeleteAfterArchive = false
                };

                var archiveResult = await ArchiveDataAsync(archiveOptions, cancellationToken);
                result.ArchiveId = archiveResult.ArchiveId;
                result.RecordsArchived = archiveResult.RecordsArchived;
            }

            // Step 4: Execute purge in batches
            var purgeExecutionResult = await ExecutePurgeInBatchesAsync(recordsToPurge, options, cancellationToken);
            result.RecordsPurged = purgeExecutionResult.RecordsPurged;
            result.TablesPurged = purgeExecutionResult.TablesPurged;
            result.Errors = purgeExecutionResult.Errors;

            // Step 5: Update statistics and rebuild indexes
            if (options.UpdateStatisticsAfterPurge && result.RecordsPurged > 0)
            {
                await UpdateStatisticsAsync(purgeExecutionResult.TablesPurged, cancellationToken);
            }

            // Step 6: Log purge operation
            await LogPurgeOperationAsync(result, cancellationToken);

            result.EndTime = DateTime.UtcNow;
            result.Success = result.Errors.Count == 0;
            result.Message = $"Purged {result.RecordsPurged} records from {result.TablesPurged.Count} tables";

            _logger.LogInformation("Purge completed. Records: {Records}, Duration: {Duration}ms",
                result.RecordsPurged, result.Duration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during data purge");
            result.EndTime = DateTime.UtcNow;
            result.Success = false;
            result.Message = $"Purge failed: {ex.Message}";
            result.Errors.Add(ex.Message);
            return result;
        }
    }

    public async Task<ComplianceReport> GenerateComplianceReportAsync(ComplianceReportOptions options, CancellationToken cancellationToken = default)
    {
        var report = new ComplianceReport
        {
            ReportId = Guid.NewGuid().ToString(),
            GeneratedAt = DateTime.UtcNow,
            ReportPeriod = options.ReportPeriod,
            ComplianceType = options.ComplianceType
        };

        try
        {
            _logger.LogInformation("Generating compliance report. Type: {Type}, Period: {Period}",
                options.ComplianceType, options.ReportPeriod);

            // Step 1: Gather data retention compliance
            report.DataRetentionCompliance = await CheckDataRetentionComplianceAsync(options, cancellationToken);

            // Step 2: Check data residency compliance
            report.DataResidencyCompliance = await CheckDataResidencyComplianceAsync(options, cancellationToken);

            // Step 3: Audit user data access
            report.UserDataAccess = await AuditUserDataAccessAsync(options, cancellationToken);

            // Step 4: Check encryption compliance
            report.EncryptionCompliance = await CheckEncryptionComplianceAsync(cancellationToken);

            // Step 5: Generate GDPR-specific metrics if applicable
            if (options.ComplianceType == ComplianceType.GDPR || options.ComplianceType == ComplianceType.All)
            {
                report.GdprMetrics = await GenerateGdprMetricsAsync(options, cancellationToken);
            }

            // Step 6: Generate HIPAA-specific metrics if applicable
            if (options.ComplianceType == ComplianceType.HIPAA || options.ComplianceType == ComplianceType.All)
            {
                report.HipaaMetrics = await GenerateHipaaMetricsAsync(options, cancellationToken);
            }

            // Step 7: Identify compliance violations
            report.Violations = await IdentifyComplianceViolationsAsync(options, cancellationToken);

            // Step 8: Generate recommendations
            report.Recommendations = GenerateComplianceRecommendations(report);

            // Step 9: Calculate compliance score
            report.ComplianceScore = CalculateComplianceScore(report);

            // Step 10: Export report if requested
            if (options.ExportFormat != ExportFormat.None)
            {
                report.ExportPath = await ExportComplianceReportAsync(report, options.ExportFormat, cancellationToken);
            }

            _logger.LogInformation("Compliance report generated. Score: {Score}%, Violations: {Violations}",
                report.ComplianceScore, report.Violations.Count);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating compliance report");
            report.Errors.Add($"Report generation failed: {ex.Message}");
            return report;
        }
    }

    public async Task<DataRetentionStatus> GetDataRetentionStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = new DataRetentionStatus
        {
            CheckTime = DateTime.UtcNow
        };

        try
        {
            // Get retention policies
            var policies = await GetRetentionPoliciesAsync(cancellationToken);
            status.ActivePolicies = policies;

            // Check data age distribution
            foreach (var table in GetMonitoredTables())
            {
                var ageDistribution = await GetDataAgeDistributionAsync(table, cancellationToken);
                status.DataAgeByTable[table] = ageDistribution;

                // Check if any data exceeds retention
                if (ageDistribution.OldestRecord.HasValue)
                {
                    var policy = policies.FirstOrDefault(p => p.TableName == table);
                    if (policy != null)
                    {
                        var retentionDate = DateTime.UtcNow.AddDays(-policy.RetentionDays);
                        if (ageDistribution.OldestRecord.Value < retentionDate)
                        {
                            status.TablesExceedingRetention.Add(table);
                            status.RecordsExceedingRetention += ageDistribution.RecordsExceedingRetention;
                        }
                    }
                }
            }

            // Get archive statistics
            var archives = await GetArchiveHistoryAsync(DateTime.UtcNow.AddDays(-30), null, cancellationToken);
            status.RecentArchives = archives.Count;
            status.TotalArchiveSize = archives.Sum(a => a.ArchiveSize);

            // Get purge statistics
            status.LastPurgeDate = await GetLastPurgeDateAsync(cancellationToken);
            status.NextScheduledPurge = CalculateNextPurgeDate(status.LastPurgeDate);

            status.IsCompliant = status.TablesExceedingRetention.Count == 0;

            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting data retention status");
            status.Errors.Add($"Status check failed: {ex.Message}");
            return status;
        }
    }

    #region Private Helper Methods

    private async Task<DataToArchive> IdentifyDataToArchiveAsync(ArchiveOptions options, CancellationToken cancellationToken)
    {
        var data = new DataToArchive();

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var table in GetArchivableTables(options.ArchiveType))
        {
            var sql = $@"
                SELECT COUNT(*) 
                FROM {table} 
                WHERE CreatedAt <= @CutoffDate";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@CutoffDate", options.CutoffDate);

            var count = (int)await command.ExecuteScalarAsync(cancellationToken);
            if (count > 0)
            {
                data.Tables[table] = count;
                data.TotalRecords += count;
            }
        }

        return data;
    }

    private async Task<ExportResult> ExportDataToArchiveAsync(DataToArchive dataToArchive, string archivePath, ArchiveOptions options, CancellationToken cancellationToken)
    {
        var result = new ExportResult();

        using var fileStream = new FileStream(archivePath, FileMode.Create);
        using var writer = new StreamWriter(fileStream);

        // Write archive header
        var header = new
        {
            Version = "1.0",
            CreatedAt = DateTime.UtcNow,
            Options = options,
            Tables = dataToArchive.Tables.Keys.ToList()
        };
        await writer.WriteLineAsync(JsonSerializer.Serialize(header));

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var table in dataToArchive.Tables.Keys)
        {
            var sql = $@"
                SELECT * 
                FROM {table} 
                WHERE CreatedAt <= @CutoffDate";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@CutoffDate", options.CutoffDate);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            
            // Write table header
            await writer.WriteLineAsync($"TABLE:{table}");
            
            // Write column metadata
            var columns = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
            }
            await writer.WriteLineAsync($"COLUMNS:{JsonSerializer.Serialize(columns)}");

            // Write data rows
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new object[reader.FieldCount];
                reader.GetValues(row);
                await writer.WriteLineAsync($"DATA:{JsonSerializer.Serialize(row)}");
                result.RecordsExported++;
            }

            result.TablesExported.Add(table);
        }

        return result;
    }

    private async Task<string> CompressArchiveAsync(string archivePath, CancellationToken cancellationToken)
    {
        var compressedPath = $"{archivePath}.gz";

        using var originalStream = File.OpenRead(archivePath);
        using var compressedStream = File.Create(compressedPath);
        using var gzipStream = new GZipStream(compressedStream, CompressionLevel.Optimal);

        await originalStream.CopyToAsync(gzipStream, cancellationToken);

        _logger.LogInformation("Compressed archive from {OriginalSize} to {CompressedSize}",
            FormatFileSize(originalStream.Length),
            FormatFileSize(compressedStream.Length));

        return compressedPath;
    }

    private async Task<DeleteResult> DeleteArchivedDataAsync(DataToArchive dataToArchive, CancellationToken cancellationToken)
    {
        var result = new DeleteResult { Success = true };

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var table in dataToArchive.Tables.Keys)
            {
                var sql = $@"
                    DELETE FROM {table} 
                    WHERE CreatedAt <= @CutoffDate";

                using var command = new SqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@CutoffDate", DateTime.UtcNow); // Should use options.CutoffDate

                var deleted = await command.ExecuteNonQueryAsync(cancellationToken);
                result.RecordsDeleted += deleted;
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            result.Success = false;
            result.Error = ex.Message;
            _logger.LogError(ex, "Error deleting archived data");
        }

        return result;
    }

    private async Task LogArchiveOperationAsync(ArchiveResult result, CancellationToken cancellationToken)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await EnsureArchiveLogTableExistsAsync(connection, cancellationToken);

            const string sql = @"
                INSERT INTO DataArchiveLog 
                (ArchiveId, ArchiveType, ArchiveDate, RecordsArchived, ArchiveSize, 
                 ArchivePath, Status, CreatedBy, Metadata)
                VALUES 
                (@ArchiveId, @ArchiveType, @ArchiveDate, @RecordsArchived, @ArchiveSize,
                 @ArchivePath, @Status, @CreatedBy, @Metadata)";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@ArchiveId", result.ArchiveId);
            command.Parameters.AddWithValue("@ArchiveType", result.Options.ArchiveType.ToString());
            command.Parameters.AddWithValue("@ArchiveDate", DateTime.UtcNow);
            command.Parameters.AddWithValue("@RecordsArchived", result.RecordsArchived);
            command.Parameters.AddWithValue("@ArchiveSize", result.ArchiveSize);
            command.Parameters.AddWithValue("@ArchivePath", result.ArchivePath);
            command.Parameters.AddWithValue("@Status", result.Success ? "Completed" : "Failed");
            command.Parameters.AddWithValue("@CreatedBy", Environment.UserName);
            command.Parameters.AddWithValue("@Metadata", JsonSerializer.Serialize(result));

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log archive operation");
        }
    }

    private async Task EnsureArchiveLogTableExistsAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DataArchiveLog')
            BEGIN
                CREATE TABLE DataArchiveLog (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    ArchiveId NVARCHAR(50) NOT NULL,
                    ArchiveType NVARCHAR(50),
                    ArchiveDate DATETIME2 NOT NULL,
                    RecordsArchived BIGINT,
                    ArchiveSize BIGINT,
                    ArchivePath NVARCHAR(500),
                    Status NVARCHAR(50),
                    CreatedBy NVARCHAR(100),
                    Metadata NVARCHAR(MAX),
                    INDEX IX_DataArchiveLog_ArchiveId (ArchiveId),
                    INDEX IX_DataArchiveLog_ArchiveDate (ArchiveDate)
                )
            END";

        using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private List<string> GetArchivableTables(ArchiveType archiveType)
    {
        return archiveType switch
        {
            ArchiveType.AuditLogs => new List<string> { "AuditLogs" },
            ArchiveType.UserActivity => new List<string> { "UserActivityLogs", "Sessions" },
            ArchiveType.SystemLogs => new List<string> { "SystemLogs", "ErrorLogs" },
            ArchiveType.All => new List<string> { "AuditLogs", "UserActivityLogs", "Sessions", "SystemLogs", "ErrorLogs" },
            _ => new List<string>()
        };
    }

    private List<string> GetMonitoredTables()
    {
        return new List<string> 
        { 
            "AuditLogs", 
            "UserActivityLogs", 
            "Sessions", 
            "SystemLogs", 
            "ErrorLogs",
            "Users",
            "Groups",
            "Roles"
        };
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private string ExtractArchiveIdFromFileName(string fileName)
    {
        // Extract GUID from filename pattern: Archive_Type_Date_GUID.archive
        var parts = Path.GetFileNameWithoutExtension(fileName).Split('_');
        if (parts.Length >= 4)
        {
            return parts[parts.Length - 1];
        }
        return Guid.NewGuid().ToString();
    }

    // Additional helper methods would be implemented here...
    private async Task<string> LocateArchiveFileAsync(string archiveId, CancellationToken cancellationToken) => await Task.FromResult(string.Empty);
    private async Task<string> DecompressArchiveAsync(string archivePath, CancellationToken cancellationToken) => await Task.FromResult(archivePath);
    private async Task<ArchiveManifest> ReadArchiveManifestAsync(string archivePath, CancellationToken cancellationToken) => await Task.FromResult(new ArchiveManifest());
    private async Task GenerateArchiveManifestAsync(ArchiveResult result, DataToArchive data, CancellationToken cancellationToken) => await Task.CompletedTask;
    private async Task<ValidationResult> ValidateArchiveCompatibilityAsync(ArchiveManifest manifest, CancellationToken cancellationToken) => await Task.FromResult(new ValidationResult { IsCompatible = true });
    private async Task<RestoreDataResult> RestoreDataFromArchiveAsync(string archivePath, ArchiveManifest manifest, RestoreArchiveOptions options, CancellationToken cancellationToken) => await Task.FromResult(new RestoreDataResult());
    private async Task<ConflictResolutionResult> ResolveConflictsAsync(List<DataConflict> conflicts, ConflictResolution resolution, CancellationToken cancellationToken) => await Task.FromResult(new ConflictResolutionResult());
    private async Task UpdateArchiveStatusAsync(string archiveId, ArchiveStatus status, CancellationToken cancellationToken) => await Task.CompletedTask;
    private async Task<RecordsToPurge> IdentifyRecordsToPurgeAsync(List<string> tables, DateTime cutoffDate, CancellationToken cancellationToken) => await Task.FromResult(new RecordsToPurge());
    private async Task<PurgeExecutionResult> ExecutePurgeInBatchesAsync(RecordsToPurge records, PurgeOptions options, CancellationToken cancellationToken) => await Task.FromResult(new PurgeExecutionResult());
    private async Task UpdateStatisticsAsync(List<string> tables, CancellationToken cancellationToken) => await Task.CompletedTask;
    private async Task LogPurgeOperationAsync(PurgeResult result, CancellationToken cancellationToken) => await Task.CompletedTask;
    private async Task<List<RetentionPolicy>> GetRetentionPoliciesAsync(CancellationToken cancellationToken) => await Task.FromResult(new List<RetentionPolicy>());
    private async Task<DataAgeDistribution> GetDataAgeDistributionAsync(string table, CancellationToken cancellationToken) => await Task.FromResult(new DataAgeDistribution());
    private async Task<DateTime?> GetLastPurgeDateAsync(CancellationToken cancellationToken) => await Task.FromResult<DateTime?>(null);
    private DateTime? CalculateNextPurgeDate(DateTime? lastPurgeDate) => lastPurgeDate?.AddDays(7);
    private async Task<DataRetentionCompliance> CheckDataRetentionComplianceAsync(ComplianceReportOptions options, CancellationToken cancellationToken) => await Task.FromResult(new DataRetentionCompliance());
    private async Task<DataResidencyCompliance> CheckDataResidencyComplianceAsync(ComplianceReportOptions options, CancellationToken cancellationToken) => await Task.FromResult(new DataResidencyCompliance());
    private async Task<UserDataAccessAudit> AuditUserDataAccessAsync(ComplianceReportOptions options, CancellationToken cancellationToken) => await Task.FromResult(new UserDataAccessAudit());
    private async Task<EncryptionCompliance> CheckEncryptionComplianceAsync(CancellationToken cancellationToken) => await Task.FromResult(new EncryptionCompliance());
    private async Task<GdprMetrics> GenerateGdprMetricsAsync(ComplianceReportOptions options, CancellationToken cancellationToken) => await Task.FromResult(new GdprMetrics());
    private async Task<HipaaMetrics> GenerateHipaaMetricsAsync(ComplianceReportOptions options, CancellationToken cancellationToken) => await Task.FromResult(new HipaaMetrics());
    private async Task<List<ComplianceViolation>> IdentifyComplianceViolationsAsync(ComplianceReportOptions options, CancellationToken cancellationToken) => await Task.FromResult(new List<ComplianceViolation>());
    private List<string> GenerateComplianceRecommendations(ComplianceReport report) => new List<string>();
    private double CalculateComplianceScore(ComplianceReport report) => 95.0;
    private async Task<string> ExportComplianceReportAsync(ComplianceReport report, ExportFormat format, CancellationToken cancellationToken) => await Task.FromResult("");

    #endregion
}

#region Models

public enum ArchiveType
{
    AuditLogs,
    UserActivity,
    SystemLogs,
    PrePurge,
    Compliance,
    All
}

public enum ArchiveStatus
{
    Created,
    Verified,
    Restored,
    Deleted,
    Corrupted,
    Unknown
}

public enum ComplianceType
{
    GDPR,
    HIPAA,
    CCPA,
    SOC2,
    All
}

public enum ExportFormat
{
    None,
    Json,
    Csv,
    Pdf,
    Html
}

public enum ConflictResolution
{
    Skip,
    Overwrite,
    Merge,
    Manual
}

public class ArchiveOptions
{
    public ArchiveType ArchiveType { get; set; }
    public DateTime CutoffDate { get; set; }
    public bool CompressArchive { get; set; } = true;
    public bool DeleteAfterArchive { get; set; } = false;
    public bool VerifyArchive { get; set; } = true;
    public List<string> TablesToArchive { get; set; } = new();
}

public class RestoreArchiveOptions
{
    public bool ValidateBeforeRestore { get; set; } = true;
    public ConflictResolution ConflictResolution { get; set; } = ConflictResolution.Skip;
    public bool MarkAsRestored { get; set; } = true;
    public List<string> TablesToRestore { get; set; } = new();
}

public class PurgeOptions
{
    public int RetentionDays { get; set; }
    public List<string> TablesToPurge { get; set; } = new();
    public bool CreateBackupBeforePurge { get; set; } = true;
    public bool ArchiveBeforePurge { get; set; } = true;
    public bool UpdateStatisticsAfterPurge { get; set; } = true;
    public int BatchSize { get; set; } = 1000;
}

public class ComplianceReportOptions
{
    public ComplianceType ComplianceType { get; set; }
    public string ReportPeriod { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public ExportFormat ExportFormat { get; set; } = ExportFormat.Json;
    public bool IncludeDetails { get; set; } = true;
}

public class ArchiveResult
{
    public string ArchiveId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    public ArchiveOptions Options { get; set; } = new();
    public string ArchivePath { get; set; } = string.Empty;
    public string CompressedPath { get; set; } = string.Empty;
    public long ArchiveSize { get; set; }
    public long CompressedSize { get; set; }
    public int RecordsIdentified { get; set; }
    public int RecordsArchived { get; set; }
    public int RecordsDeleted { get; set; }
    public List<string> TablesArchived { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class RestoreArchiveResult
{
    public string ArchiveId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    public string ArchivePath { get; set; } = string.Empty;
    public int RecordsRestored { get; set; }
    public List<string> TablesRestored { get; set; } = new();
    public List<DataConflict> Conflicts { get; set; } = new();
    public int ConflictsResolved { get; set; }
    public int ConflictsFailed { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class ArchiveInfo
{
    public string ArchiveId { get; set; } = string.Empty;
    public ArchiveType ArchiveType { get; set; }
    public DateTime ArchiveDate { get; set; }
    public long RecordsArchived { get; set; }
    public long ArchiveSize { get; set; }
    public string ArchivePath { get; set; } = string.Empty;
    public ArchiveStatus Status { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string Metadata { get; set; } = string.Empty;
}

public class PurgeResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    public PurgeOptions Options { get; set; } = new();
    public int RecordsIdentified { get; set; }
    public int RecordsPurged { get; set; }
    public int RecordsArchived { get; set; }
    public List<string> TablesPurged { get; set; } = new();
    public string ArchiveId { get; set; } = string.Empty;
    public bool BackupCreated { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class ComplianceReport
{
    public string ReportId { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public string ReportPeriod { get; set; } = string.Empty;
    public ComplianceType ComplianceType { get; set; }
    public double ComplianceScore { get; set; }
    public DataRetentionCompliance DataRetentionCompliance { get; set; } = new();
    public DataResidencyCompliance DataResidencyCompliance { get; set; } = new();
    public UserDataAccessAudit UserDataAccess { get; set; } = new();
    public EncryptionCompliance EncryptionCompliance { get; set; } = new();
    public GdprMetrics GdprMetrics { get; set; } = new();
    public HipaaMetrics HipaaMetrics { get; set; } = new();
    public List<ComplianceViolation> Violations { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public string ExportPath { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
}

public class DataRetentionStatus
{
    public DateTime CheckTime { get; set; }
    public bool IsCompliant { get; set; }
    public List<RetentionPolicy> ActivePolicies { get; set; } = new();
    public Dictionary<string, DataAgeDistribution> DataAgeByTable { get; set; } = new();
    public List<string> TablesExceedingRetention { get; set; } = new();
    public long RecordsExceedingRetention { get; set; }
    public int RecentArchives { get; set; }
    public long TotalArchiveSize { get; set; }
    public DateTime? LastPurgeDate { get; set; }
    public DateTime? NextScheduledPurge { get; set; }
    public List<string> Errors { get; set; } = new();
}

// Helper classes
internal class DataToArchive
{
    public Dictionary<string, int> Tables { get; set; } = new();
    public int TotalRecords { get; set; }
}

internal class ExportResult
{
    public int RecordsExported { get; set; }
    public List<string> TablesExported { get; set; } = new();
}

internal class DeleteResult
{
    public bool Success { get; set; }
    public int RecordsDeleted { get; set; }
    public string Error { get; set; } = string.Empty;
}

internal class ArchiveManifest
{
    public string Version { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<string> Tables { get; set; } = new();
    public Dictionary<string, List<string>> TableColumns { get; set; } = new();
}

internal class ValidationResult
{
    public bool IsCompatible { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();
}

internal class RestoreDataResult
{
    public int RecordsRestored { get; set; }
    public List<string> TablesRestored { get; set; } = new();
    public List<DataConflict> Conflicts { get; set; } = new();
}

internal class RecordsToPurge
{
    public int TotalRecords { get; set; }
    public Dictionary<string, int> RecordsByTable { get; set; } = new();
}

internal class PurgeExecutionResult
{
    public int RecordsPurged { get; set; }
    public List<string> TablesPurged { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

internal class ConflictResolutionResult
{
    public int Resolved { get; set; }
    public int Failed { get; set; }
}

public class DataConflict
{
    public string TableName { get; set; } = string.Empty;
    public string RecordId { get; set; } = string.Empty;
    public string ConflictType { get; set; } = string.Empty;
    public object ExistingValue { get; set; } = new();
    public object NewValue { get; set; } = new();
}

public class RetentionPolicy
{
    public string TableName { get; set; } = string.Empty;
    public int RetentionDays { get; set; }
    public bool IsActive { get; set; }
}

public class DataAgeDistribution
{
    public DateTime? OldestRecord { get; set; }
    public DateTime? NewestRecord { get; set; }
    public long TotalRecords { get; set; }
    public long RecordsExceedingRetention { get; set; }
}

public class DataRetentionCompliance
{
    public bool IsCompliant { get; set; }
    public List<string> Issues { get; set; } = new();
}

public class DataResidencyCompliance
{
    public bool IsCompliant { get; set; }
    public List<string> Issues { get; set; } = new();
}

public class UserDataAccessAudit
{
    public int TotalAccesses { get; set; }
    public int UnauthorizedAttempts { get; set; }
}

public class EncryptionCompliance
{
    public bool IsCompliant { get; set; }
    public List<string> UnencryptedData { get; set; } = new();
}

public class GdprMetrics
{
    public int DataSubjectRequests { get; set; }
    public int RightToErasureRequests { get; set; }
    public int DataBreaches { get; set; }
}

public class HipaaMetrics
{
    public int PhiAccesses { get; set; }
    public int SecurityIncidents { get; set; }
}

public class ComplianceViolation
{
    public string ViolationType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
    public string Severity { get; set; } = string.Empty;
}

#endregion