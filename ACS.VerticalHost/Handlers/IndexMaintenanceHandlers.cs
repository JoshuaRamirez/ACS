using ACS.VerticalHost.Services;
using ACS.VerticalHost.Commands;
using ACS.Service.Data;
using Microsoft.Extensions.Logging;
using static ACS.VerticalHost.Services.HandlerErrorHandling;
using static ACS.VerticalHost.Services.HandlerExtensions;
using ServiceMissingIndex = ACS.Service.Data.MissingIndexRecommendation;
using CommandMissingIndex = ACS.VerticalHost.Commands.MissingIndexRecommendation;

using ServiceIndexAnalysisReport = ACS.Service.Data.IndexAnalysisReport;
using CommandIndexAnalysisReport = ACS.VerticalHost.Commands.IndexAnalysisReport;
namespace ACS.VerticalHost.Handlers;

public class RebuildIndexCommandHandler : ICommandHandler<RebuildIndexCommand, bool>
{
    private readonly IIndexAnalyzer _indexAnalyzer;
    private readonly ILogger<RebuildIndexCommandHandler> _logger;

    public RebuildIndexCommandHandler(
        IIndexAnalyzer indexAnalyzer,
        ILogger<RebuildIndexCommandHandler> logger)
    {
        _indexAnalyzer = indexAnalyzer;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(RebuildIndexCommand command, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(RebuildIndexCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, 
            new { TableName = command.TableName, IndexName = command.IndexName }, correlationId);

        try
        {
            if (string.IsNullOrEmpty(command.TableName) || string.IsNullOrEmpty(command.IndexName))
            {
                throw new ArgumentException("TableName and IndexName are required");
            }
            
            var success = await _indexAnalyzer.RebuildIndexAsync(command.TableName, command.IndexName);
            
            if (success)
            {
                LogCommandSuccess(_logger, context, 
                    new { TableName = command.TableName, IndexName = command.IndexName }, correlationId);
            }
            else
            {
                _logger.LogWarning("Failed to rebuild index {IndexName} on table {TableName}. CorrelationId: {CorrelationId}", 
                    command.IndexName, command.TableName, correlationId);
            }

            return success;
        }
        catch (Exception ex)
        {
            return HandleCommandError<bool>(_logger, ex, context, correlationId);
        }
    }
}

public class ReorganizeIndexCommandHandler : ICommandHandler<ReorganizeIndexCommand, bool>
{
    private readonly IIndexAnalyzer _indexAnalyzer;
    private readonly ILogger<ReorganizeIndexCommandHandler> _logger;

    public ReorganizeIndexCommandHandler(
        IIndexAnalyzer indexAnalyzer,
        ILogger<ReorganizeIndexCommandHandler> logger)
    {
        _indexAnalyzer = indexAnalyzer;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(ReorganizeIndexCommand command, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(ReorganizeIndexCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, 
            new { TableName = command.TableName, IndexName = command.IndexName }, correlationId);

        try
        {
            if (string.IsNullOrEmpty(command.TableName) || string.IsNullOrEmpty(command.IndexName))
            {
                throw new ArgumentException("TableName and IndexName are required");
            }
            
            var success = await _indexAnalyzer.ReorganizeIndexAsync(command.TableName, command.IndexName);
            
            if (success)
            {
                LogCommandSuccess(_logger, context, 
                    new { TableName = command.TableName, IndexName = command.IndexName }, correlationId);
            }
            else
            {
                _logger.LogWarning("Failed to reorganize index {IndexName} on table {TableName}. CorrelationId: {CorrelationId}", 
                    command.IndexName, command.TableName, correlationId);
            }

            return success;
        }
        catch (Exception ex)
        {
            return HandleCommandError<bool>(_logger, ex, context, correlationId);
        }
    }
}

public class AnalyzeIndexesQueryHandler : IQueryHandler<AnalyzeIndexesQuery, CommandIndexAnalysisReport>
{
    private readonly IIndexAnalyzer _indexAnalyzer;
    private readonly ILogger<AnalyzeIndexesQueryHandler> _logger;

    public AnalyzeIndexesQueryHandler(
        IIndexAnalyzer indexAnalyzer,
        ILogger<AnalyzeIndexesQueryHandler> logger)
    {
        _indexAnalyzer = indexAnalyzer;
        _logger = logger;
    }

    public async Task<CommandIndexAnalysisReport> HandleAsync(AnalyzeIndexesQuery query, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(AnalyzeIndexesQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { }, correlationId);

        try
        {
            var report = await _indexAnalyzer.AnalyzeIndexesAsync();
            
            var result = new Commands.IndexAnalysisReport
            {
                AnalysisDate = report.AnalysisDate,
                DatabaseName = report.DatabaseName,
                TotalIndexes = report.TotalIndexes,
                HealthScore = report.HealthScore,
                IndexStatistics = report.IndexStatistics.Select(i => new Commands.IndexStatistic
                {
                    TableName = i.TableName,
                    IndexName = i.IndexName,
                    UserSeeks = i.UserSeeks,
                    UserScans = i.UserScans,
                    UserLookups = i.UserLookups,
                    SizeMB = i.SizeMB
                }).ToList(),
                MissingIndexes = report.MissingIndexes.Select(m => new CommandMissingIndex
                {
                    TableName = m.TableName,
                    ImprovementMeasure = m.ImprovementMeasure,
                    AverageImpact = m.AverageImpact,
                    AverageCost = m.AverageCost,
                    TotalSeeksScans = m.TotalSeeksScans,
                    EqualityColumns = m.EqualityColumns,
                    InequalityColumns = m.InequalityColumns,
                    IncludedColumns = m.IncludedColumns,
                    LastUserSeek = m.LastUserSeek,
                    CreateStatement = m.CreateStatement
                }).ToList(),
                UnusedIndexes = report.UnusedIndexes.Select(u => new Commands.UnusedIndexInfo
                {
                    SchemaName = u.SchemaName,
                    TableName = u.TableName,
                    IndexName = u.IndexName,
                    IndexType = u.IndexType,
                    SizeMB = u.SizeMB,
                    DaysSinceLastUse = u.DaysSinceLastUse,
                    UserSeeks = u.UserSeeks,
                    UserScans = u.UserScans,
                    UserLookups = u.UserLookups,
                    UserUpdates = u.UserUpdates
                }).ToList(),
                FragmentedIndexes = report.FragmentedIndexes.Select(f => new Commands.FragmentedIndexInfo
                {
                    SchemaName = f.SchemaName,
                    TableName = f.TableName,
                    IndexName = f.IndexName,
                    FragmentationPercent = f.FragmentationPercent,
                    PageCount = f.PageCount,
                    RecordCount = f.RecordCount,
                    AvgPageSpaceUsed = f.AvgPageSpaceUsed,
                    FillFactor = f.FillFactor,
                    RecommendedAction = f.RecommendedAction
                }).ToList(),
                DuplicateIndexes = report.DuplicateIndexes.Select(d => new Commands.DuplicateIndexInfo
                {
                    TableName = d.TableName,
                    IndexName1 = d.Index1,
                    IndexName2 = d.Index2,
                    Columns = d.KeyColumns
                }).ToList(),
                Recommendations = report.Recommendations
            };

            LogQuerySuccess(_logger, context, 
                new { TotalIndexes = result.TotalIndexes, HealthScore = result.HealthScore, MissingCount = result.MissingIndexes.Count }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<CommandIndexAnalysisReport>(_logger, ex, context, correlationId);
        }
    }
}

public class GetMissingIndexRecommendationsQueryHandler : IQueryHandler<GetMissingIndexRecommendationsQuery, List<CommandMissingIndex>>
{
    private readonly IIndexAnalyzer _indexAnalyzer;
    private readonly ILogger<GetMissingIndexRecommendationsQueryHandler> _logger;

    public GetMissingIndexRecommendationsQueryHandler(
        IIndexAnalyzer indexAnalyzer,
        ILogger<GetMissingIndexRecommendationsQueryHandler> logger)
    {
        _indexAnalyzer = indexAnalyzer;
        _logger = logger;
    }

    public async Task<List<CommandMissingIndex>> HandleAsync(GetMissingIndexRecommendationsQuery query, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetMissingIndexRecommendationsQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { }, correlationId);

        try
        {
            var recommendations = await _indexAnalyzer.GetMissingIndexRecommendationsAsync();
            
            var result = recommendations.Select(r => new CommandMissingIndex
            {
                TableName = r.TableName,
                ImprovementMeasure = r.ImprovementMeasure,
                AverageImpact = r.AverageImpact,
                AverageCost = r.AverageCost,
                TotalSeeksScans = r.TotalSeeksScans,
                EqualityColumns = r.EqualityColumns,
                InequalityColumns = r.InequalityColumns,
                IncludedColumns = r.IncludedColumns,
                LastUserSeek = r.LastUserSeek,
                CreateStatement = r.CreateStatement
            }).ToList();

            LogQuerySuccess(_logger, context, new { RecommendationCount = result.Count }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<List<CommandMissingIndex>>(_logger, ex, context, correlationId);
        }
    }
}

public class GetUnusedIndexesQueryHandler : IQueryHandler<GetUnusedIndexesQuery, List<UnusedIndexInfo>>
{
    private readonly IIndexAnalyzer _indexAnalyzer;
    private readonly ILogger<GetUnusedIndexesQueryHandler> _logger;

    public GetUnusedIndexesQueryHandler(
        IIndexAnalyzer indexAnalyzer,
        ILogger<GetUnusedIndexesQueryHandler> logger)
    {
        _indexAnalyzer = indexAnalyzer;
        _logger = logger;
    }

    public async Task<List<UnusedIndexInfo>> HandleAsync(GetUnusedIndexesQuery query, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetUnusedIndexesQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { DaysSinceLastUse = query.DaysSinceLastUse }, correlationId);

        try
        {
            var unusedIndexes = await _indexAnalyzer.GetUnusedIndexesAsync(query.DaysSinceLastUse);
            
            var result = unusedIndexes.Select(i => new Commands.UnusedIndexInfo
            {
                SchemaName = i.SchemaName,
                TableName = i.TableName,
                IndexName = i.IndexName,
                IndexType = i.IndexType,
                SizeMB = i.SizeMB,
                DaysSinceLastUse = i.DaysSinceLastUse,
                UserSeeks = i.UserSeeks,
                UserScans = i.UserScans,
                UserLookups = i.UserLookups,
                UserUpdates = i.UserUpdates
            }).ToList();

            LogQuerySuccess(_logger, context, 
                new { DaysSinceLastUse = query.DaysSinceLastUse, UnusedIndexCount = result.Count }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<List<UnusedIndexInfo>>(_logger, ex, context, correlationId);
        }
    }
}

public class GetFragmentedIndexesQueryHandler : IQueryHandler<GetFragmentedIndexesQuery, List<FragmentedIndexInfo>>
{
    private readonly IIndexAnalyzer _indexAnalyzer;
    private readonly ILogger<GetFragmentedIndexesQueryHandler> _logger;

    public GetFragmentedIndexesQueryHandler(
        IIndexAnalyzer indexAnalyzer,
        ILogger<GetFragmentedIndexesQueryHandler> logger)
    {
        _indexAnalyzer = indexAnalyzer;
        _logger = logger;
    }

    public async Task<List<FragmentedIndexInfo>> HandleAsync(GetFragmentedIndexesQuery query, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetFragmentedIndexesQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { FragmentationThreshold = query.FragmentationThreshold }, correlationId);

        try
        {
            var fragmentedIndexes = await _indexAnalyzer.GetFragmentedIndexesAsync(query.FragmentationThreshold);
            
            var result = fragmentedIndexes.Select(i => new Commands.FragmentedIndexInfo
            {
                SchemaName = i.SchemaName,
                TableName = i.TableName,
                IndexName = i.IndexName,
                FragmentationPercent = i.FragmentationPercent,
                PageCount = i.PageCount,
                RecordCount = i.RecordCount,
                AvgPageSpaceUsed = i.AvgPageSpaceUsed,
                FillFactor = i.FillFactor,
                RecommendedAction = i.RecommendedAction
            }).ToList();

            LogQuerySuccess(_logger, context, 
                new { FragmentationThreshold = query.FragmentationThreshold, FragmentedIndexCount = result.Count }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<List<FragmentedIndexInfo>>(_logger, ex, context, correlationId);
        }
    }
}