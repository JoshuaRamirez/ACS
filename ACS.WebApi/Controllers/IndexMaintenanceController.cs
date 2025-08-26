using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ACS.Service.Data;

namespace ACS.WebApi.Controllers;

/// <summary>
/// Controller for database index maintenance operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrator,DatabaseAdmin")]
public class IndexMaintenanceController : ControllerBase
{
    private readonly IIndexAnalyzer _indexAnalyzer;
    private readonly ILogger<IndexMaintenanceController> _logger;

    public IndexMaintenanceController(
        IIndexAnalyzer indexAnalyzer,
        ILogger<IndexMaintenanceController> logger)
    {
        _indexAnalyzer = indexAnalyzer;
        _logger = logger;
    }

    /// <summary>
    /// Perform a comprehensive index analysis
    /// </summary>
    /// <returns>Index analysis report</returns>
    [HttpGet("analyze")]
    public async Task<IActionResult> AnalyzeIndexes()
    {
        try
        {
            _logger.LogInformation("Manual index analysis requested");
            var report = await _indexAnalyzer.AnalyzeIndexesAsync();
            
            return Ok(new
            {
                report.AnalysisDate,
                report.DatabaseName,
                report.TotalIndexes,
                report.HealthScore,
                Statistics = new
                {
                    TotalIndexes = report.IndexStatistics.Count,
                    MostUsedIndexes = report.IndexStatistics
                        .OrderByDescending(i => i.UserSeeks + i.UserScans + i.UserLookups)
                        .Take(5)
                        .Select(i => new
                        {
                            i.TableName,
                            i.IndexName,
                            TotalUses = i.UserSeeks + i.UserScans + i.UserLookups,
                            i.SizeMB
                        })
                },
                Issues = new
                {
                    MissingIndexes = report.MissingIndexes.Count,
                    UnusedIndexes = report.UnusedIndexes.Count,
                    FragmentedIndexes = report.FragmentedIndexes.Count,
                    DuplicateIndexes = report.DuplicateIndexes.Count
                },
                report.Recommendations,
                TopMissingIndexes = report.MissingIndexes
                    .OrderByDescending(m => m.ImprovementMeasure)
                    .Take(3)
                    .Select(m => new
                    {
                        m.TableName,
                        m.ImprovementMeasure,
                        m.AverageImpact,
                        m.EqualityColumns,
                        m.InequalityColumns,
                        m.IncludedColumns
                    }),
                TopUnusedIndexes = report.UnusedIndexes
                    .OrderByDescending(u => u.SizeMB)
                    .Take(3)
                    .Select(u => new
                    {
                        u.TableName,
                        u.IndexName,
                        u.SizeMB,
                        u.DaysSinceLastUse
                    }),
                TopFragmentedIndexes = report.FragmentedIndexes
                    .OrderByDescending(f => f.FragmentationPercent)
                    .Take(3)
                    .Select(f => new
                    {
                        f.TableName,
                        f.IndexName,
                        f.FragmentationPercent,
                        f.RecommendedAction
                    })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing index analysis");
            return StatusCode(500, new { error = "Failed to analyze indexes", message = ex.Message });
        }
    }

    /// <summary>
    /// Get missing index recommendations
    /// </summary>
    /// <returns>List of missing index recommendations</returns>
    [HttpGet("missing")]
    public async Task<IActionResult> GetMissingIndexes()
    {
        try
        {
            var recommendations = await _indexAnalyzer.GetMissingIndexRecommendationsAsync();
            
            return Ok(recommendations.Select(r => new
            {
                r.TableName,
                r.ImprovementMeasure,
                r.AverageImpact,
                r.AverageCost,
                r.TotalSeeksScans,
                r.EqualityColumns,
                r.InequalityColumns,
                r.IncludedColumns,
                r.LastUserSeek,
                r.CreateStatement
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting missing index recommendations");
            return StatusCode(500, new { error = "Failed to get missing indexes", message = ex.Message });
        }
    }

    /// <summary>
    /// Get unused indexes
    /// </summary>
    /// <param name="daysSinceLastUse">Number of days since last use (default: 30)</param>
    /// <returns>List of unused indexes</returns>
    [HttpGet("unused")]
    public async Task<IActionResult> GetUnusedIndexes([FromQuery] int daysSinceLastUse = 30)
    {
        try
        {
            var unusedIndexes = await _indexAnalyzer.GetUnusedIndexesAsync(daysSinceLastUse);
            
            return Ok(new
            {
                DaysSinceLastUseThreshold = daysSinceLastUse,
                Count = unusedIndexes.Count,
                TotalSizeMB = unusedIndexes.Sum(i => i.SizeMB),
                Indexes = unusedIndexes.Select(i => new
                {
                    i.SchemaName,
                    i.TableName,
                    i.IndexName,
                    i.IndexType,
                    i.SizeMB,
                    i.DaysSinceLastUse,
                    i.UserSeeks,
                    i.UserScans,
                    i.UserLookups,
                    i.UserUpdates
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unused indexes");
            return StatusCode(500, new { error = "Failed to get unused indexes", message = ex.Message });
        }
    }

    /// <summary>
    /// Get fragmented indexes
    /// </summary>
    /// <param name="fragmentationThreshold">Fragmentation percentage threshold (default: 30)</param>
    /// <returns>List of fragmented indexes</returns>
    [HttpGet("fragmented")]
    public async Task<IActionResult> GetFragmentedIndexes([FromQuery] double fragmentationThreshold = 30.0)
    {
        try
        {
            var fragmentedIndexes = await _indexAnalyzer.GetFragmentedIndexesAsync(fragmentationThreshold);
            
            return Ok(new
            {
                FragmentationThreshold = fragmentationThreshold,
                Count = fragmentedIndexes.Count,
                Indexes = fragmentedIndexes.Select(i => new
                {
                    i.SchemaName,
                    i.TableName,
                    i.IndexName,
                    i.FragmentationPercent,
                    i.PageCount,
                    i.RecordCount,
                    i.AvgPageSpaceUsed,
                    i.FillFactor,
                    i.RecommendedAction
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting fragmented indexes");
            return StatusCode(500, new { error = "Failed to get fragmented indexes", message = ex.Message });
        }
    }

    /// <summary>
    /// Rebuild a specific index
    /// </summary>
    /// <param name="request">Index maintenance request containing table and index names</param>
    /// <returns>Success status</returns>
    [HttpPost("rebuild")]
    public async Task<IActionResult> RebuildIndex([FromBody] IndexMaintenanceRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.TableName) || string.IsNullOrEmpty(request.IndexName))
            {
                return BadRequest(new { error = "TableName and IndexName are required" });
            }

            _logger.LogInformation("Manual index rebuild requested for {IndexName} on {TableName}", 
                request.IndexName, request.TableName);
            
            var success = await _indexAnalyzer.RebuildIndexAsync(request.TableName, request.IndexName);
            
            if (success)
            {
                return Ok(new 
                { 
                    success = true, 
                    message = $"Index {request.IndexName} on table {request.TableName} rebuilt successfully" 
                });
            }
            else
            {
                return StatusCode(500, new 
                { 
                    success = false, 
                    message = $"Failed to rebuild index {request.IndexName} on table {request.TableName}" 
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebuilding index");
            return StatusCode(500, new { error = "Failed to rebuild index", message = ex.Message });
        }
    }

    /// <summary>
    /// Reorganize a specific index
    /// </summary>
    /// <param name="request">Index maintenance request containing table and index names</param>
    /// <returns>Success status</returns>
    [HttpPost("reorganize")]
    public async Task<IActionResult> ReorganizeIndex([FromBody] IndexMaintenanceRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.TableName) || string.IsNullOrEmpty(request.IndexName))
            {
                return BadRequest(new { error = "TableName and IndexName are required" });
            }

            _logger.LogInformation("Manual index reorganize requested for {IndexName} on {TableName}", 
                request.IndexName, request.TableName);
            
            var success = await _indexAnalyzer.ReorganizeIndexAsync(request.TableName, request.IndexName);
            
            if (success)
            {
                return Ok(new 
                { 
                    success = true, 
                    message = $"Index {request.IndexName} on table {request.TableName} reorganized successfully" 
                });
            }
            else
            {
                return StatusCode(500, new 
                { 
                    success = false, 
                    message = $"Failed to reorganize index {request.IndexName} on table {request.TableName}" 
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reorganizing index");
            return StatusCode(500, new { error = "Failed to reorganize index", message = ex.Message });
        }
    }
}

/// <summary>
/// Request model for index maintenance operations
/// </summary>
public class IndexMaintenanceRequest
{
    /// <summary>
    /// Name of the table containing the index
    /// </summary>
    public string TableName { get; set; } = string.Empty;
    
    /// <summary>
    /// Name of the index to maintain
    /// </summary>
    public string IndexName { get; set; } = string.Empty;
}