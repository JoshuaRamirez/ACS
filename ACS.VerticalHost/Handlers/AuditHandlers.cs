using ACS.VerticalHost.Services;
using ACS.VerticalHost.Commands;
using ACS.Service.Domain;
using ACS.Service.Services;
using static ACS.VerticalHost.Services.HandlerErrorHandling;
using static ACS.VerticalHost.Services.HandlerExtensions;

namespace ACS.VerticalHost.Handlers;

// Audit Command Handlers
public class RecordAuditEventCommandHandler : ICommandHandler<RecordAuditEventCommand, AuditEventResult>
{
    private readonly IAuditService _auditService;
    private readonly ILogger<RecordAuditEventCommandHandler> _logger;

    public RecordAuditEventCommandHandler(IAuditService auditService, ILogger<RecordAuditEventCommandHandler> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<AuditEventResult> HandleAsync(RecordAuditEventCommand command, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(RecordAuditEventCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new 
        { 
            EventType = command.EventType, 
            EventCategory = command.EventCategory,
            UserId = command.UserId,
            Action = command.Action
        }, correlationId);
        
        try
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(command.EventType))
                throw new ArgumentException("Event type is required");
            if (string.IsNullOrWhiteSpace(command.EventCategory))
                throw new ArgumentException("Event category is required");
            if (string.IsNullOrWhiteSpace(command.Action))
                throw new ArgumentException("Action is required");

            var request = new ACS.Service.Requests.RecordAuditEventRequest
            {
                EventType = command.EventType,
                EventCategory = command.EventCategory,
                UserId = command.UserId,
                EntityId = command.EntityId,
                EntityType = command.EntityType,
                ResourceId = command.ResourceId,
                Action = command.Action,
                Details = command.Details,
                Severity = command.Severity,
                IpAddress = command.IpAddress,
                UserAgent = command.UserAgent,
                SessionId = command.SessionId,
                EventTimestamp = command.EventTimestamp ?? DateTime.UtcNow,
                Metadata = command.Metadata,
                CorrelationId = correlationId
            };
            
            var response = await _auditService.RecordEventAsync(request);
            
            var result = new AuditEventResult
            {
                Success = true,
                AuditEventId = response.AuditEventId,
                RecordedAt = DateTime.UtcNow,
                Message = "Audit event recorded successfully",
                CorrelationId = correlationId
            };
            
            LogCommandSuccess(_logger, context, new 
            { 
                AuditEventId = result.AuditEventId,
                EventType = command.EventType,
                EventCategory = command.EventCategory
            }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleCommandError<AuditEventResult>(_logger, ex, context, correlationId);
        }
    }
}

public class PurgeOldAuditDataCommandHandler : ICommandHandler<PurgeOldAuditDataCommand, AuditPurgeResult>
{
    private readonly IAuditService _auditService;
    private readonly ILogger<PurgeOldAuditDataCommandHandler> _logger;

    public PurgeOldAuditDataCommandHandler(IAuditService auditService, ILogger<PurgeOldAuditDataCommandHandler> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<AuditPurgeResult> HandleAsync(PurgeOldAuditDataCommand command, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(PurgeOldAuditDataCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new 
        { 
            OlderThan = command.OlderThan,
            BatchSize = command.BatchSize,
            DryRun = command.DryRun,
            PreserveCompliance = command.PreserveCompliance
        }, correlationId);
        
        try
        {
            if (command.OlderThan >= DateTime.UtcNow.AddDays(-1))
            {
                throw new ArgumentException("Purge date must be at least 1 day old for safety");
            }

            var startTime = DateTime.UtcNow;
            
            var request = new ACS.Service.Requests.PurgeAuditDataRequest
            {
                OlderThan = command.OlderThan,
                EventCategories = command.EventCategories,
                SeverityLevels = command.SeverityLevels,
                PreserveCompliance = command.PreserveCompliance,
                BatchSize = command.BatchSize,
                DryRun = command.DryRun,
                RequestedBy = command.RequestedBy ?? "system"
            };
            
            var response = await _auditService.PurgeOldDataAsync(request);
            var endTime = DateTime.UtcNow;
            
            var result = new AuditPurgeResult
            {
                Success = true,
                RecordsProcessed = response.RecordsProcessed,
                RecordsDeleted = response.RecordsDeleted,
                RecordsPreserved = response.RecordsPreserved,
                PurgeStartedAt = startTime,
                PurgeCompletedAt = endTime,
                Duration = endTime - startTime,
                Message = command.DryRun ? 
                    $"Dry run completed. Would delete {response.RecordsDeleted} records." :
                    $"Purge completed successfully. Deleted {response.RecordsDeleted} records.",
                PreservedReasons = response.PreservedReasons.ToList()
            };
            
            LogCommandSuccess(_logger, context, new 
            { 
                RecordsProcessed = result.RecordsProcessed,
                RecordsDeleted = result.RecordsDeleted,
                RecordsPreserved = result.RecordsPreserved,
                DryRun = command.DryRun
            }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleCommandError<AuditPurgeResult>(_logger, ex, context, correlationId);
        }
    }
}

// Audit Query Handlers
public class GetAuditLogQueryHandler : IQueryHandler<GetAuditLogQuery, List<AuditLogEntry>>
{
    private readonly IAuditService _auditService;
    private readonly ILogger<GetAuditLogQueryHandler> _logger;

    public GetAuditLogQueryHandler(IAuditService auditService, ILogger<GetAuditLogQueryHandler> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<List<AuditLogEntry>> HandleAsync(GetAuditLogQuery query, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetAuditLogQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new 
        { 
            Page = query.Page,
            PageSize = query.PageSize,
            StartDate = query.StartDate,
            EndDate = query.EndDate,
            UserId = query.UserId
        }, correlationId);
        
        try
        {
            var request = new ACS.Service.Requests.GetAuditLogEnhancedRequest
            {
                Page = query.Page,
                PageSize = query.PageSize,
                StartDate = query.StartDate,
                EndDate = query.EndDate,
                EventTypes = query.EventTypes,
                EventCategories = query.EventCategories,
                UserId = query.UserId,
                EntityId = query.EntityId,
                EntityType = query.EntityType,
                ResourceId = query.ResourceId,
                SeverityLevels = query.SeverityLevels,
                SearchText = query.SearchText,
                IpAddress = query.IpAddress,
                SortBy = query.SortBy,
                SortDescending = query.SortDescending
            };
            
            var response = await _auditService.GetAuditLogAsync(request);
            
            var result = response.Entries.Select(e => new AuditLogEntry
            {
                Id = e.Id,
                EventType = e.EventType,
                EventCategory = e.EventCategory,
                UserId = e.UserId,
                UserName = e.UserName,
                EntityId = e.EntityId,
                EntityType = e.EntityType,
                EntityName = e.EntityName,
                ResourceId = e.ResourceId,
                ResourceName = e.ResourceName,
                Action = e.Action,
                Details = e.Details,
                Severity = e.Severity,
                IpAddress = e.IpAddress,
                UserAgent = e.UserAgent,
                SessionId = e.SessionId,
                EventTimestamp = e.EventTimestamp,
                CreatedAt = e.CreatedAt,
                Metadata = e.Metadata
            }).ToList();
            
            LogQuerySuccess(_logger, context, new 
            { 
                Page = query.Page,
                Count = result.Count,
                TotalAvailable = response.TotalCount
            }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<List<AuditLogEntry>>(_logger, ex, context, correlationId);
        }
    }
}

public class GetUserAuditTrailQueryHandler : IQueryHandler<GetUserAuditTrailQuery, List<UserAuditTrailEntry>>
{
    private readonly IAuditService _auditService;
    private readonly ILogger<GetUserAuditTrailQueryHandler> _logger;

    public GetUserAuditTrailQueryHandler(IAuditService auditService, ILogger<GetUserAuditTrailQueryHandler> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<List<UserAuditTrailEntry>> HandleAsync(GetUserAuditTrailQuery query, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetUserAuditTrailQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new 
        { 
            UserId = query.UserId,
            StartDate = query.StartDate,
            EndDate = query.EndDate,
            Page = query.Page,
            PageSize = query.PageSize
        }, correlationId);
        
        try
        {
            if (query.UserId <= 0)
                throw new ArgumentException("Valid user ID is required");

            var request = new ACS.Service.Requests.GetUserAuditTrailRequest
            {
                UserId = query.UserId,
                StartDate = query.StartDate,
                EndDate = query.EndDate,
                EventCategories = query.EventCategories,
                IncludeSystemEvents = query.IncludeSystemEvents,
                IncludePermissionChanges = query.IncludePermissionChanges,
                IncludeResourceAccess = query.IncludeResourceAccess,
                Page = query.Page,
                PageSize = query.PageSize
            };
            
            var response = await _auditService.GetUserAuditTrailAsync(request);
            
            var result = response.Entries.Select(e => new UserAuditTrailEntry
            {
                Id = e.Id,
                EventType = e.EventType,
                EventCategory = e.EventCategory,
                Action = e.Action,
                Details = e.Details,
                ResourceName = e.ResourceName,
                PermissionName = e.PermissionName,
                Severity = e.Severity,
                IpAddress = e.IpAddress,
                SessionId = e.SessionId,
                EventTimestamp = e.EventTimestamp,
                IsAnomaly = e.IsAnomaly,
                AnomalyReason = e.AnomalyReason
            }).ToList();
            
            LogQuerySuccess(_logger, context, new 
            { 
                UserId = query.UserId,
                Count = result.Count,
                AnomalyCount = result.Count(e => e.IsAnomaly)
            }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<List<UserAuditTrailEntry>>(_logger, ex, context, correlationId);
        }
    }
}

public class GetComplianceReportQueryHandler : IQueryHandler<GetComplianceReportQuery, ComplianceReportResult>
{
    private readonly IComplianceService _complianceService;
    private readonly ILogger<GetComplianceReportQueryHandler> _logger;

    public GetComplianceReportQueryHandler(IComplianceService complianceService, ILogger<GetComplianceReportQueryHandler> logger)
    {
        _complianceService = complianceService;
        _logger = logger;
    }

    public async Task<ComplianceReportResult> HandleAsync(GetComplianceReportQuery query, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(GetComplianceReportQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new 
        { 
            ReportType = query.ReportType,
            StartDate = query.StartDate,
            EndDate = query.EndDate,
            ReportFormat = query.ReportFormat
        }, correlationId);
        
        try
        {
            if (string.IsNullOrWhiteSpace(query.ReportType))
                throw new ArgumentException("Report type is required");
            if (query.StartDate >= query.EndDate)
                throw new ArgumentException("Start date must be before end date");

            var request = new ACS.Service.Services.GenerateComplianceReportRequest
            {
                ReportType = query.ReportType,
                StartDate = query.StartDate,
                EndDate = query.EndDate,
                UserIds = query.UserIds,
                ResourceIds = query.ResourceIds,
                IncludeAnomalies = query.IncludeAnomalies,
                IncludeRiskAssessment = query.IncludeRiskAssessment,
                ReportFormat = query.ReportFormat,
                RequestedBy = query.RequestedBy ?? "system"
            };
            
            var response = await _complianceService.GenerateReportAsync(request);
            
            var result = new ComplianceReportResult
            {
                ReportId = response.ReportId,
                ReportType = response.ReportType,
                ReportGeneratedAt = DateTime.UtcNow,
                RequestedBy = query.RequestedBy,
                CoveredPeriodStart = query.StartDate,
                CoveredPeriodEnd = query.EndDate,
                Summary = new ComplianceReportSummary
                {
                    TotalEvents = response.Summary.TotalEvents,
                    SecurityEvents = response.Summary.SecurityEvents,
                    PermissionChanges = response.Summary.PermissionChanges,
                    ResourceAccesses = response.Summary.ResourceAccesses,
                    UniqueUsers = response.Summary.UniqueUsers,
                    UniqueResources = response.Summary.UniqueResources,
                    ViolationCount = response.Summary.ViolationCount,
                    AnomalyCount = response.Summary.AnomalyCount,
                    OverallRiskLevel = response.Summary.OverallRiskLevel
                },
                Violations = response.Violations.Select(v => new ACS.VerticalHost.Commands.ComplianceViolation
                {
                    ViolationType = v.ViolationType,
                    Description = v.Description,
                    Severity = v.Severity,
                    UserId = v.UserId,
                    UserName = v.UserName,
                    ResourceId = v.ResourceId,
                    ResourceName = v.ResourceName,
                    OccurredAt = v.OccurredAt,
                    RecommendedAction = v.RecommendedAction,
                    Details = v.Details
                }).ToList(),
                Anomalies = response.Anomalies.Select(a => new ComplianceAnomaly
                {
                    AnomalyType = a.AnomalyType,
                    Description = a.Description,
                    ConfidenceScore = a.ConfidenceScore,
                    UserId = a.UserId,
                    UserName = a.UserName,
                    DetectedAt = a.DetectedAt,
                    Pattern = a.Pattern,
                    Context = a.Context
                }).ToList(),
                RiskAssessment = response.RiskAssessment != null ? new ComplianceRiskAssessment
                {
                    OverallRiskLevel = response.RiskAssessment.OverallRiskLevel,
                    RiskScore = response.RiskAssessment.RiskScore,
                    RiskFactors = response.RiskAssessment.RiskFactors.Select(rf => new ACS.VerticalHost.Commands.RiskFactor
                    {
                        Category = rf.Category,
                        Description = rf.Description,
                        Impact = ParseDoubleWithDefault(rf.Impact, 0.5),
                        Probability = ParseDoubleWithDefault(rf.Probability, 0.5),
                        Mitigation = rf.Mitigation ?? string.Empty
                    }).ToList(),
                    Recommendations = response.RiskAssessment.Recommendations.ToList()
                } : null,
                ReportData = response.ReportData != null
                    ? new Dictionary<string, object> { { "reportBytes", response.ReportData } }
                    : new Dictionary<string, object>()
            };
            
            LogQuerySuccess(_logger, context, new 
            { 
                ReportId = result.ReportId,
                ReportType = query.ReportType,
                ViolationCount = result.Violations.Count,
                AnomalyCount = result.Anomalies.Count
            }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<ComplianceReportResult>(_logger, ex, context, correlationId);
        }
    }

    private static double ParseDoubleWithDefault(string value, double defaultValue)
    {
        if (double.TryParse(value, out var result))
        {
            return result;
        }
        return defaultValue;
    }
}

public class ValidateAuditIntegrityQueryHandler : IQueryHandler<ValidateAuditIntegrityQuery, AuditIntegrityResult>
{
    private readonly IAuditService _auditService;
    private readonly ILogger<ValidateAuditIntegrityQueryHandler> _logger;

    public ValidateAuditIntegrityQueryHandler(IAuditService auditService, ILogger<ValidateAuditIntegrityQueryHandler> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<AuditIntegrityResult> HandleAsync(ValidateAuditIntegrityQuery query, CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(ValidateAuditIntegrityQueryHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new 
        { 
            StartDate = query.StartDate,
            EndDate = query.EndDate,
            CheckHashChain = query.CheckHashChain,
            CheckCompleteness = query.CheckCompleteness,
            CheckConsistency = query.CheckConsistency,
            PerformDeepValidation = query.PerformDeepValidation
        }, correlationId);
        
        try
        {
            var request = new ACS.Service.Requests.ValidateAuditIntegrityRequest
            {
                StartDate = query.StartDate,
                EndDate = query.EndDate,
                CheckHashChain = query.CheckHashChain,
                CheckCompleteness = query.CheckCompleteness,
                CheckConsistency = query.CheckConsistency,
                PerformDeepValidation = query.PerformDeepValidation,
                RequestedBy = query.RequestedBy ?? "system"
            };
            
            var response = await _auditService.ValidateIntegrityAsync(request);
            
            var result = new AuditIntegrityResult
            {
                IsIntegrityValid = response.IsIntegrityValid,
                ValidationPerformedAt = DateTime.UtcNow,
                RequestedBy = query.RequestedBy,
                ChecksPerformed = new AuditIntegrityChecks
                {
                    HashChainValidated = response.ChecksPerformed.HashChainValidated,
                    CompletenessValidated = response.ChecksPerformed.CompletenessValidated,
                    ConsistencyValidated = response.ChecksPerformed.ConsistencyValidated,
                    DeepValidationPerformed = response.ChecksPerformed.DeepValidationPerformed
                },
                Issues = response.Issues.Select(i => new AuditIntegrityIssue
                {
                    IssueType = i.IssueType,
                    Description = i.Description,
                    Severity = i.Severity,
                    AffectedAuditId = i.AffectedAuditId,
                    AffectedTimestamp = i.AffectedTimestamp,
                    RecommendedAction = i.RecommendedAction
                }).ToList(),
                Statistics = new AuditIntegrityStatistics
                {
                    TotalRecordsChecked = response.Statistics.TotalRecordsChecked,
                    ValidRecords = response.Statistics.ValidRecords,
                    InvalidRecords = response.Statistics.InvalidRecords,
                    ValidationDuration = response.Statistics.ValidationDuration,
                    EarliestRecord = response.Statistics.EarliestRecord,
                    LatestRecord = response.Statistics.LatestRecord
                },
                Message = response.IsIntegrityValid ? 
                    "Audit integrity validation passed successfully" : 
                    $"Audit integrity validation failed with {response.Issues.Count} issues"
            };
            
            LogQuerySuccess(_logger, context, new 
            { 
                IsIntegrityValid = result.IsIntegrityValid,
                IssueCount = result.Issues.Count,
                RecordsChecked = result.Statistics.TotalRecordsChecked
            }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            return HandleQueryError<AuditIntegrityResult>(_logger, ex, context, correlationId);
        }
    }
}