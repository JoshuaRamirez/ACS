using ACS.Service.Requests;
using ACS.Service.Responses;

namespace ACS.Service.Services;

/// <summary>
/// Service interface for Compliance operations - minimal interface matching handler requirements
/// </summary>
public interface IComplianceService
{
    // Methods that handlers are calling
    Task<GenerateComplianceReportResponse> GenerateReportAsync(GenerateComplianceReportRequest request);
}

// Minimal request/response types needed by handlers
public record GenerateComplianceReportRequest
{
    public string ReportType { get; init; } = string.Empty; // GDPR, SOC2, HIPAA, PCI-DSS, Custom
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public List<int>? UserIds { get; init; }
    public List<int>? ResourceIds { get; init; }
    public bool IncludeAnomalies { get; init; } = true;
    public bool IncludeRiskAssessment { get; init; } = true;
    public string ReportFormat { get; init; } = "PDF"; // PDF, JSON, XML, CSV
    public string RequestedBy { get; init; } = string.Empty;
    public Dictionary<string, object> Parameters { get; init; } = new();
}

public record GenerateComplianceReportResponse
{
    public string ReportId { get; init; } = string.Empty;
    public string ReportType { get; init; } = string.Empty;
    public ComplianceReportSummaryInfo Summary { get; init; } = new();
    public List<ComplianceViolationInfo> Violations { get; init; } = new();
    public List<ComplianceAnomalyInfo> Anomalies { get; init; } = new();
    public ComplianceRiskAssessmentInfo? RiskAssessment { get; init; }
    public byte[]? ReportData { get; init; }
    public string ReportUrl { get; init; } = string.Empty;
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
}

public record ComplianceReportSummaryInfo
{
    public int TotalEvents { get; init; }
    public int SecurityEvents { get; init; }
    public int PermissionChanges { get; init; }
    public int ResourceAccesses { get; init; }
    public int UniqueUsers { get; init; }
    public int UniqueResources { get; init; }
    public int ViolationCount { get; init; }
    public int AnomalyCount { get; init; }
    public string OverallRiskLevel { get; init; } = string.Empty;
    public double ComplianceScore { get; init; }
}

public record ComplianceViolationInfo
{
    public string ViolationType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public int? UserId { get; init; }
    public string? UserName { get; init; }
    public int? ResourceId { get; init; }
    public string? ResourceName { get; init; }
    public DateTime OccurredAt { get; init; }
    public string RecommendedAction { get; init; } = string.Empty;
    public Dictionary<string, object> Details { get; init; } = new();
    public bool IsResolved { get; init; }
    public DateTime? ResolvedAt { get; init; }
}

public record ComplianceAnomalyInfo
{
    public string AnomalyType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public double ConfidenceScore { get; init; }
    public int? UserId { get; init; }
    public string? UserName { get; init; }
    public DateTime DetectedAt { get; init; }
    public string Pattern { get; init; } = string.Empty;
    public Dictionary<string, object> Context { get; init; } = new();
}

public record ComplianceRiskAssessmentInfo
{
    public string OverallRiskLevel { get; init; } = string.Empty;
    public double RiskScore { get; init; }
    public List<RiskFactorInfo> RiskFactors { get; init; } = new();
    public List<string> Recommendations { get; init; } = new();
    public string RiskJustification { get; init; } = string.Empty;
}

public record RiskFactorInfo
{
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Impact { get; init; } = string.Empty;
    public string Probability { get; init; } = string.Empty;
    public string? Mitigation { get; init; }
}