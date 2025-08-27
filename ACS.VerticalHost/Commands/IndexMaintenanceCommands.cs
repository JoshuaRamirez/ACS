using ACS.VerticalHost.Services;

namespace ACS.VerticalHost.Commands;

// Index Maintenance Commands
public class RebuildIndexCommand : ICommand<bool>
{
    public string TableName { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
}

public class ReorganizeIndexCommand : ICommand<bool>
{
    public string TableName { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
}

// Index Maintenance Queries
public class AnalyzeIndexesQuery : IQuery<IndexAnalysisReport>
{
}

public class GetMissingIndexRecommendationsQuery : IQuery<List<MissingIndexRecommendation>>
{
}

public class GetUnusedIndexesQuery : IQuery<List<UnusedIndexInfo>>
{
    public int DaysSinceLastUse { get; set; } = 30;
}

public class GetFragmentedIndexesQuery : IQuery<List<FragmentedIndexInfo>>
{
    public double FragmentationThreshold { get; set; } = 30.0;
}

// Result Types
public class IndexAnalysisReport
{
    public DateTime AnalysisDate { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public int TotalIndexes { get; set; }
    public double HealthScore { get; set; }
    public List<IndexStatistic> IndexStatistics { get; set; } = new();
    public List<MissingIndexRecommendation> MissingIndexes { get; set; } = new();
    public List<UnusedIndexInfo> UnusedIndexes { get; set; } = new();
    public List<FragmentedIndexInfo> FragmentedIndexes { get; set; } = new();
    public List<DuplicateIndexInfo> DuplicateIndexes { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class IndexStatistic
{
    public string TableName { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
    public long UserSeeks { get; set; }
    public long UserScans { get; set; }
    public long UserLookups { get; set; }
    public double SizeMB { get; set; }
}

public class MissingIndexRecommendation
{
    public string TableName { get; set; } = string.Empty;
    public double ImprovementMeasure { get; set; }
    public double AverageImpact { get; set; }
    public double AverageCost { get; set; }
    public long TotalSeeksScans { get; set; }
    public string EqualityColumns { get; set; } = string.Empty;
    public string InequalityColumns { get; set; } = string.Empty;
    public string IncludedColumns { get; set; } = string.Empty;
    public DateTime? LastUserSeek { get; set; }
    public string CreateStatement { get; set; } = string.Empty;
}

public class UnusedIndexInfo
{
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
    public string IndexType { get; set; } = string.Empty;
    public double SizeMB { get; set; }
    public int DaysSinceLastUse { get; set; }
    public long UserSeeks { get; set; }
    public long UserScans { get; set; }
    public long UserLookups { get; set; }
    public long UserUpdates { get; set; }
}

public class FragmentedIndexInfo
{
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
    public double FragmentationPercent { get; set; }
    public long PageCount { get; set; }
    public long RecordCount { get; set; }
    public double AvgPageSpaceUsed { get; set; }
    public int FillFactor { get; set; }
    public string RecommendedAction { get; set; } = string.Empty;
}

public class DuplicateIndexInfo
{
    public string TableName { get; set; } = string.Empty;
    public string IndexName1 { get; set; } = string.Empty;
    public string IndexName2 { get; set; } = string.Empty;
    public string Columns { get; set; } = string.Empty;
}