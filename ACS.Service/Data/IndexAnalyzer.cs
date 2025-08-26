using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Text;

namespace ACS.Service.Data;

/// <summary>
/// Service for analyzing database index usage and performance
/// </summary>
public interface IIndexAnalyzer
{
    Task<IndexAnalysisReport> AnalyzeIndexesAsync(CancellationToken cancellationToken = default);
    Task<List<MissingIndexRecommendation>> GetMissingIndexRecommendationsAsync(CancellationToken cancellationToken = default);
    Task<List<UnusedIndex>> GetUnusedIndexesAsync(int daysSinceLastUse = 30, CancellationToken cancellationToken = default);
    Task<List<FragmentedIndex>> GetFragmentedIndexesAsync(double fragmentationThreshold = 30.0, CancellationToken cancellationToken = default);
    Task<bool> RebuildIndexAsync(string tableName, string indexName, CancellationToken cancellationToken = default);
    Task<bool> ReorganizeIndexAsync(string tableName, string indexName, CancellationToken cancellationToken = default);
}

public class IndexAnalyzer : IIndexAnalyzer
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IndexAnalyzer> _logger;

    public IndexAnalyzer(
        ApplicationDbContext dbContext,
        IConfiguration configuration,
        ILogger<IndexAnalyzer> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IndexAnalysisReport> AnalyzeIndexesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting comprehensive index analysis");

        var report = new IndexAnalysisReport
        {
            AnalysisDate = DateTime.UtcNow,
            DatabaseName = _dbContext.Database.GetDbConnection().Database
        };

        try
        {
            // Get all indexes
            report.TotalIndexes = await GetTotalIndexCountAsync(cancellationToken);
            
            // Get index statistics
            report.IndexStatistics = await GetIndexStatisticsAsync(cancellationToken);
            
            // Get missing indexes
            report.MissingIndexes = await GetMissingIndexRecommendationsAsync(cancellationToken);
            
            // Get unused indexes
            report.UnusedIndexes = await GetUnusedIndexesAsync(30, cancellationToken);
            
            // Get fragmented indexes
            report.FragmentedIndexes = await GetFragmentedIndexesAsync(30.0, cancellationToken);
            
            // Get duplicate indexes
            report.DuplicateIndexes = await GetDuplicateIndexesAsync(cancellationToken);
            
            // Calculate scores
            report.HealthScore = CalculateHealthScore(report);
            report.Recommendations = GenerateRecommendations(report);

            _logger.LogInformation("Index analysis completed. Health score: {HealthScore}%", report.HealthScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during index analysis");
            throw;
        }

        return report;
    }

    public async Task<List<MissingIndexRecommendation>> GetMissingIndexRecommendationsAsync(CancellationToken cancellationToken = default)
    {
        var recommendations = new List<MissingIndexRecommendation>();

        const string sql = @"
            SELECT TOP 20
                ROUND(s.avg_total_user_cost * s.avg_user_impact * (s.user_seeks + s.user_scans), 0) AS ImprovementMeasure,
                s.avg_total_user_cost AS AvgCost,
                s.avg_user_impact AS AvgImpact,
                s.user_seeks + s.user_scans AS TotalSeeksScans,
                d.statement AS TableName,
                d.equality_columns AS EqualityColumns,
                d.inequality_columns AS InequalityColumns,
                d.included_columns AS IncludedColumns,
                s.unique_compiles AS UniqueCompiles,
                s.last_user_seek AS LastUserSeek,
                'CREATE INDEX IX_' + REPLACE(REPLACE(REPLACE(
                    OBJECT_NAME(d.object_id), ' ', '_'), '[', ''), ']', '') + '_' + 
                    CAST(ROW_NUMBER() OVER(ORDER BY s.avg_total_user_cost * s.avg_user_impact DESC) AS VARCHAR(10)) +
                    ' ON ' + d.statement + ' (' +
                    ISNULL(d.equality_columns, '') +
                    CASE WHEN d.equality_columns IS NOT NULL AND d.inequality_columns IS NOT NULL 
                         THEN ',' ELSE '' END +
                    ISNULL(d.inequality_columns, '') + ')' +
                    CASE WHEN d.included_columns IS NOT NULL 
                         THEN ' INCLUDE (' + d.included_columns + ')' ELSE '' END AS CreateStatement
            FROM sys.dm_db_missing_index_groups g
            INNER JOIN sys.dm_db_missing_index_group_stats s ON s.group_handle = g.index_group_handle
            INNER JOIN sys.dm_db_missing_index_details d ON d.index_handle = g.index_handle
            WHERE d.database_id = DB_ID()
            ORDER BY ImprovementMeasure DESC";

        using var connection = new SqlConnection(_dbContext.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            recommendations.Add(new MissingIndexRecommendation
            {
                ImprovementMeasure = reader.GetDouble(0),
                AverageCost = reader.GetDouble(1),
                AverageImpact = reader.GetDouble(2),
                TotalSeeksScans = reader.GetInt64(3),
                TableName = reader.GetString(4),
                EqualityColumns = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                InequalityColumns = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                IncludedColumns = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                UniqueCompiles = reader.GetInt64(8),
                LastUserSeek = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                CreateStatement = reader.GetString(10)
            });
        }

        return recommendations;
    }

    public async Task<List<UnusedIndex>> GetUnusedIndexesAsync(int daysSinceLastUse = 30, CancellationToken cancellationToken = default)
    {
        var unusedIndexes = new List<UnusedIndex>();

        const string sql = @"
            SELECT 
                OBJECT_SCHEMA_NAME(i.object_id) AS SchemaName,
                OBJECT_NAME(i.object_id) AS TableName,
                i.name AS IndexName,
                i.type_desc AS IndexType,
                s.user_seeks,
                s.user_scans,
                s.user_lookups,
                s.user_updates,
                ps.reserved_page_count * 8 / 1024.0 AS SizeMB,
                DATEDIFF(DAY, 
                    ISNULL(s.last_user_seek, 
                    ISNULL(s.last_user_scan, 
                    ISNULL(s.last_user_lookup, '1900-01-01'))), GETDATE()) AS DaysSinceLastUse
            FROM sys.indexes i
            LEFT JOIN sys.dm_db_index_usage_stats s ON i.object_id = s.object_id AND i.index_id = s.index_id
            LEFT JOIN sys.dm_db_partition_stats ps ON i.object_id = ps.object_id AND i.index_id = ps.index_id
            WHERE OBJECTPROPERTY(i.object_id, 'IsUserTable') = 1
                AND i.type_desc <> 'HEAP'
                AND i.is_primary_key = 0
                AND i.is_unique = 0
                AND (s.user_seeks + s.user_scans + s.user_lookups = 0 
                     OR DATEDIFF(DAY, 
                         ISNULL(s.last_user_seek, 
                         ISNULL(s.last_user_scan, 
                         ISNULL(s.last_user_lookup, '1900-01-01'))), GETDATE()) > @DaysSinceLastUse)
            ORDER BY ps.reserved_page_count DESC";

        using var connection = new SqlConnection(_dbContext.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@DaysSinceLastUse", daysSinceLastUse);
        
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            unusedIndexes.Add(new UnusedIndex
            {
                SchemaName = reader.GetString(0),
                TableName = reader.GetString(1),
                IndexName = reader.GetString(2),
                IndexType = reader.GetString(3),
                UserSeeks = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                UserScans = reader.IsDBNull(5) ? 0 : reader.GetInt64(5),
                UserLookups = reader.IsDBNull(6) ? 0 : reader.GetInt64(6),
                UserUpdates = reader.IsDBNull(7) ? 0 : reader.GetInt64(7),
                SizeMB = reader.IsDBNull(8) ? 0 : reader.GetDouble(8),
                DaysSinceLastUse = reader.IsDBNull(9) ? int.MaxValue : reader.GetInt32(9)
            });
        }

        return unusedIndexes;
    }

    public async Task<List<FragmentedIndex>> GetFragmentedIndexesAsync(double fragmentationThreshold = 30.0, CancellationToken cancellationToken = default)
    {
        var fragmentedIndexes = new List<FragmentedIndex>();

        const string sql = @"
            SELECT 
                s.name AS SchemaName,
                t.name AS TableName,
                i.name AS IndexName,
                ps.avg_fragmentation_in_percent AS FragmentationPercent,
                ps.page_count AS PageCount,
                ps.record_count AS RecordCount,
                ps.avg_page_space_used_in_percent AS AvgPageSpaceUsed,
                i.fill_factor AS FillFactor,
                CASE 
                    WHEN ps.avg_fragmentation_in_percent > 30 THEN 'REBUILD'
                    WHEN ps.avg_fragmentation_in_percent > 5 THEN 'REORGANIZE'
                    ELSE 'OK'
                END AS RecommendedAction
            FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ps
            INNER JOIN sys.indexes i ON ps.object_id = i.object_id AND ps.index_id = i.index_id
            INNER JOIN sys.tables t ON i.object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE ps.avg_fragmentation_in_percent > @FragmentationThreshold
                AND ps.page_count > 1000
                AND i.name IS NOT NULL
            ORDER BY ps.avg_fragmentation_in_percent DESC";

        using var connection = new SqlConnection(_dbContext.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@FragmentationThreshold", fragmentationThreshold);
        
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            fragmentedIndexes.Add(new FragmentedIndex
            {
                SchemaName = reader.GetString(0),
                TableName = reader.GetString(1),
                IndexName = reader.GetString(2),
                FragmentationPercent = reader.GetDouble(3),
                PageCount = reader.GetInt64(4),
                RecordCount = reader.GetInt64(5),
                AvgPageSpaceUsed = reader.GetDouble(6),
                FillFactor = reader.GetByte(7),
                RecommendedAction = reader.GetString(8)
            });
        }

        return fragmentedIndexes;
    }

    public async Task<bool> RebuildIndexAsync(string tableName, string indexName, CancellationToken cancellationToken = default)
    {
        try
        {
            var sql = $"ALTER INDEX [{indexName}] ON [{tableName}] REBUILD WITH (ONLINE = ON, FILLFACTOR = 90)";
            await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
            
            _logger.LogInformation("Successfully rebuilt index {IndexName} on table {TableName}", indexName, tableName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebuilding index {IndexName} on table {TableName}", indexName, tableName);
            return false;
        }
    }

    public async Task<bool> ReorganizeIndexAsync(string tableName, string indexName, CancellationToken cancellationToken = default)
    {
        try
        {
            var sql = $"ALTER INDEX [{indexName}] ON [{tableName}] REORGANIZE";
            await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
            
            _logger.LogInformation("Successfully reorganized index {IndexName} on table {TableName}", indexName, tableName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reorganizing index {IndexName} on table {TableName}", indexName, tableName);
            return false;
        }
    }

    private async Task<int> GetTotalIndexCountAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT COUNT(*) 
            FROM sys.indexes i
            INNER JOIN sys.tables t ON i.object_id = t.object_id
            WHERE t.is_ms_shipped = 0";

        using var connection = new SqlConnection(_dbContext.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(sql, connection);
        return (int)await command.ExecuteScalarAsync(cancellationToken);
    }

    private async Task<List<IndexStatistic>> GetIndexStatisticsAsync(CancellationToken cancellationToken)
    {
        var statistics = new List<IndexStatistic>();

        const string sql = @"
            SELECT TOP 50
                OBJECT_SCHEMA_NAME(i.object_id) AS SchemaName,
                OBJECT_NAME(i.object_id) AS TableName,
                i.name AS IndexName,
                i.type_desc AS IndexType,
                s.user_seeks,
                s.user_scans,
                s.user_lookups,
                s.user_updates,
                ps.reserved_page_count * 8 / 1024.0 AS SizeMB,
                ps.row_count AS RowCount,
                s.last_user_seek,
                s.last_user_scan,
                s.last_user_lookup,
                s.last_user_update
            FROM sys.indexes i
            LEFT JOIN sys.dm_db_index_usage_stats s ON i.object_id = s.object_id AND i.index_id = s.index_id
            LEFT JOIN sys.dm_db_partition_stats ps ON i.object_id = ps.object_id AND i.index_id = ps.index_id
            WHERE OBJECTPROPERTY(i.object_id, 'IsUserTable') = 1
                AND i.type_desc <> 'HEAP'
            ORDER BY (s.user_seeks + s.user_scans + s.user_lookups) DESC";

        using var connection = new SqlConnection(_dbContext.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            statistics.Add(new IndexStatistic
            {
                SchemaName = reader.GetString(0),
                TableName = reader.GetString(1),
                IndexName = reader.GetString(2),
                IndexType = reader.GetString(3),
                UserSeeks = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                UserScans = reader.IsDBNull(5) ? 0 : reader.GetInt64(5),
                UserLookups = reader.IsDBNull(6) ? 0 : reader.GetInt64(6),
                UserUpdates = reader.IsDBNull(7) ? 0 : reader.GetInt64(7),
                SizeMB = reader.IsDBNull(8) ? 0 : reader.GetDouble(8),
                RowCount = reader.IsDBNull(9) ? 0 : reader.GetInt64(9),
                LastUserSeek = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                LastUserScan = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                LastUserLookup = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
                LastUserUpdate = reader.IsDBNull(13) ? null : reader.GetDateTime(13)
            });
        }

        return statistics;
    }

    private async Task<List<DuplicateIndex>> GetDuplicateIndexesAsync(CancellationToken cancellationToken)
    {
        var duplicates = new List<DuplicateIndex>();

        const string sql = @"
            WITH IndexColumns AS (
                SELECT 
                    i.object_id,
                    i.index_id,
                    i.name AS IndexName,
                    STUFF((
                        SELECT ',' + c.name
                        FROM sys.index_columns ic
                        INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
                        ORDER BY ic.key_ordinal
                        FOR XML PATH('')
                    ), 1, 1, '') AS KeyColumns,
                    STUFF((
                        SELECT ',' + c.name
                        FROM sys.index_columns ic
                        INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 1
                        ORDER BY ic.column_id
                        FOR XML PATH('')
                    ), 1, 1, '') AS IncludedColumns
                FROM sys.indexes i
                WHERE i.type_desc <> 'HEAP'
                    AND i.is_primary_key = 0
                    AND OBJECTPROPERTY(i.object_id, 'IsUserTable') = 1
            )
            SELECT 
                OBJECT_SCHEMA_NAME(ic1.object_id) AS SchemaName,
                OBJECT_NAME(ic1.object_id) AS TableName,
                ic1.IndexName AS Index1,
                ic2.IndexName AS Index2,
                ic1.KeyColumns,
                ic1.IncludedColumns
            FROM IndexColumns ic1
            INNER JOIN IndexColumns ic2 ON ic1.object_id = ic2.object_id
                AND ic1.index_id < ic2.index_id
                AND ic1.KeyColumns = ic2.KeyColumns
                AND ISNULL(ic1.IncludedColumns, '') = ISNULL(ic2.IncludedColumns, '')";

        using var connection = new SqlConnection(_dbContext.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            duplicates.Add(new DuplicateIndex
            {
                SchemaName = reader.GetString(0),
                TableName = reader.GetString(1),
                Index1 = reader.GetString(2),
                Index2 = reader.GetString(3),
                KeyColumns = reader.GetString(4),
                IncludedColumns = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
            });
        }

        return duplicates;
    }

    private double CalculateHealthScore(IndexAnalysisReport report)
    {
        double score = 100.0;

        // Deduct points for missing indexes
        score -= Math.Min(report.MissingIndexes.Count * 2, 20);

        // Deduct points for unused indexes
        score -= Math.Min(report.UnusedIndexes.Count * 1, 10);

        // Deduct points for fragmented indexes
        score -= Math.Min(report.FragmentedIndexes.Count * 3, 30);

        // Deduct points for duplicate indexes
        score -= Math.Min(report.DuplicateIndexes.Count * 5, 20);

        return Math.Max(score, 0);
    }

    private List<string> GenerateRecommendations(IndexAnalysisReport report)
    {
        var recommendations = new List<string>();

        if (report.MissingIndexes.Any())
        {
            recommendations.Add($"Consider creating {report.MissingIndexes.Count} missing indexes that could improve query performance.");
        }

        if (report.UnusedIndexes.Any())
        {
            var totalSize = report.UnusedIndexes.Sum(i => i.SizeMB);
            recommendations.Add($"Remove {report.UnusedIndexes.Count} unused indexes to save {totalSize:F2} MB of storage.");
        }

        if (report.FragmentedIndexes.Any())
        {
            var rebuildCount = report.FragmentedIndexes.Count(i => i.RecommendedAction == "REBUILD");
            var reorganizeCount = report.FragmentedIndexes.Count(i => i.RecommendedAction == "REORGANIZE");
            
            if (rebuildCount > 0)
                recommendations.Add($"Rebuild {rebuildCount} heavily fragmented indexes.");
            
            if (reorganizeCount > 0)
                recommendations.Add($"Reorganize {reorganizeCount} moderately fragmented indexes.");
        }

        if (report.DuplicateIndexes.Any())
        {
            recommendations.Add($"Remove {report.DuplicateIndexes.Count} duplicate indexes to improve write performance.");
        }

        if (report.HealthScore < 50)
        {
            recommendations.Add("URGENT: Database index health is poor. Immediate maintenance is recommended.");
        }
        else if (report.HealthScore < 75)
        {
            recommendations.Add("Database index health needs attention. Schedule maintenance soon.");
        }

        return recommendations;
    }
}

#region Models

public class IndexAnalysisReport
{
    public DateTime AnalysisDate { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public int TotalIndexes { get; set; }
    public double HealthScore { get; set; }
    public List<IndexStatistic> IndexStatistics { get; set; } = new();
    public List<MissingIndexRecommendation> MissingIndexes { get; set; } = new();
    public List<UnusedIndex> UnusedIndexes { get; set; } = new();
    public List<FragmentedIndex> FragmentedIndexes { get; set; } = new();
    public List<DuplicateIndex> DuplicateIndexes { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class IndexStatistic
{
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
    public string IndexType { get; set; } = string.Empty;
    public long UserSeeks { get; set; }
    public long UserScans { get; set; }
    public long UserLookups { get; set; }
    public long UserUpdates { get; set; }
    public double SizeMB { get; set; }
    public long RowCount { get; set; }
    public DateTime? LastUserSeek { get; set; }
    public DateTime? LastUserScan { get; set; }
    public DateTime? LastUserLookup { get; set; }
    public DateTime? LastUserUpdate { get; set; }
}

public class MissingIndexRecommendation
{
    public double ImprovementMeasure { get; set; }
    public double AverageCost { get; set; }
    public double AverageImpact { get; set; }
    public long TotalSeeksScans { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string EqualityColumns { get; set; } = string.Empty;
    public string InequalityColumns { get; set; } = string.Empty;
    public string IncludedColumns { get; set; } = string.Empty;
    public long UniqueCompiles { get; set; }
    public DateTime? LastUserSeek { get; set; }
    public string CreateStatement { get; set; } = string.Empty;
}

public class UnusedIndex
{
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
    public string IndexType { get; set; } = string.Empty;
    public long UserSeeks { get; set; }
    public long UserScans { get; set; }
    public long UserLookups { get; set; }
    public long UserUpdates { get; set; }
    public double SizeMB { get; set; }
    public int DaysSinceLastUse { get; set; }
}

public class FragmentedIndex
{
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
    public double FragmentationPercent { get; set; }
    public long PageCount { get; set; }
    public long RecordCount { get; set; }
    public double AvgPageSpaceUsed { get; set; }
    public byte FillFactor { get; set; }
    public string RecommendedAction { get; set; } = string.Empty;
}

public class DuplicateIndex
{
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string Index1 { get; set; } = string.Empty;
    public string Index2 { get; set; } = string.Empty;
    public string KeyColumns { get; set; } = string.Empty;
    public string IncludedColumns { get; set; } = string.Empty;
}

#endregion