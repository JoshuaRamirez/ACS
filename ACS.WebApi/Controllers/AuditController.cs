using ACS.Service.Domain;
using ACS.Service.Domain.Specifications;
using ACS.Service.Services;
using ACS.WebApi.Models;
using ACS.WebApi.Models.Requests;
using ACS.WebApi.Models.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;

namespace ACS.WebApi.Controllers;

/// <summary>
/// Controller for audit logging and compliance reporting
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "Administrator,Auditor")]
[Produces("application/json")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly ISpecificationService _specificationService;
    private readonly IComplianceService _complianceService;
    private readonly ILogger<AuditController> _logger;

    public AuditController(
        IAuditService auditService,
        ISpecificationService specificationService,
        IComplianceService complianceService,
        ILogger<AuditController> logger)
    {
        _auditService = auditService;
        _specificationService = specificationService;
        _complianceService = complianceService;
        _logger = logger;
    }

    /// <summary>
    /// Gets audit log entries with filtering and pagination
    /// </summary>
    /// <param name="request">Audit query parameters</param>
    /// <returns>Paged list of audit entries</returns>
    [HttpGet("logs")]
    [ProducesResponseType(typeof(PagedResponse<AuditLogResponse>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<PagedResponse<AuditLogResponse>>> GetAuditLogsAsync(
        [FromQuery] GetAuditLogsRequest request)
    {
        try
        {
            _logger.LogInformation("Retrieving audit logs with filters: EventType={EventType}, UserId={UserId}, DateRange={StartDate} to {EndDate}",
                request.EventType, request.UserId, request.StartDate, request.EndDate);

            var auditLogs = await _auditService.GetAuditLogsAsync(
                request.StartDate,
                request.EndDate,
                request.UserId,
                request.EventType,
                request.EntityType,
                request.Page,
                request.PageSize);

            var response = new PagedResponse<AuditLogResponse>
            {
                Items = auditLogs.Items.Select(MapToResponse).ToList(),
                TotalCount = auditLogs.TotalCount,
                Page = auditLogs.Page,
                PageSize = auditLogs.PageSize,
                TotalPages = auditLogs.TotalPages
            };

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest($"Invalid request parameters: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while retrieving audit logs");
        }
    }

    /// <summary>
    /// Gets security events with risk analysis
    /// </summary>
    /// <param name="request">Security event query parameters</param>
    /// <returns>Security events with risk assessment</returns>
    [HttpGet("security-events")]
    [ProducesResponseType(typeof(PagedResponse<SecurityEventResponse>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<PagedResponse<SecurityEventResponse>>> GetSecurityEventsAsync(
        [FromQuery] GetSecurityEventsRequest request)
    {
        try
        {
            _logger.LogInformation("Retrieving security events with risk level: {RiskLevel}", request.RiskLevel);

            var securityEvents = await _auditService.GetSecurityEventsAsync(
                request.StartDate,
                request.EndDate,
                request.RiskLevel,
                request.EventCategory,
                request.Page,
                request.PageSize);

            var response = new PagedResponse<SecurityEventResponse>
            {
                Items = securityEvents.Items.Select(MapToSecurityEventResponse).ToList(),
                TotalCount = securityEvents.TotalCount,
                Page = securityEvents.Page,
                PageSize = securityEvents.PageSize,
                TotalPages = securityEvents.TotalPages
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving security events");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while retrieving security events");
        }
    }

    /// <summary>
    /// Gets compliance audit report
    /// </summary>
    /// <param name="request">Compliance report parameters</param>
    /// <returns>Compliance audit report</returns>
    [HttpGet("compliance-report")]
    [ProducesResponseType(typeof(ComplianceReportResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<ComplianceReportResponse>> GetComplianceReportAsync(
        [FromQuery] GetComplianceReportRequest request)
    {
        try
        {
            _logger.LogInformation("Generating compliance report for standard: {ComplianceStandard}, period: {StartDate} to {EndDate}",
                request.ComplianceStandard, request.StartDate, request.EndDate);

            var complianceData = await _complianceService.GenerateComplianceReportAsync(
                request.ComplianceStandard,
                request.StartDate,
                request.EndDate,
                request.IncludeDetails);

            var response = new ComplianceReportResponse
            {
                ComplianceStandard = request.ComplianceStandard,
                ReportPeriod = new DateRange { StartDate = request.StartDate, EndDate = request.EndDate },
                GeneratedAt = DateTime.UtcNow,
                OverallScore = complianceData.OverallScore,
                ComplianceLevel = complianceData.ComplianceLevel,
                Requirements = complianceData.Requirements.Select(MapToComplianceRequirement).ToList(),
                Violations = complianceData.Violations.Select(MapToComplianceViolation).ToList(),
                Recommendations = complianceData.Recommendations
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating compliance report for standard: {ComplianceStandard}", 
                request.ComplianceStandard);
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while generating the compliance report");
        }
    }

    /// <summary>
    /// Exports audit logs to various formats
    /// </summary>
    /// <param name="request">Export request parameters</param>
    /// <returns>Exported audit data</returns>
    [HttpPost("export")]
    [ProducesResponseType(typeof(FileResult), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> ExportAuditLogsAsync([FromBody] ExportAuditLogsRequest request)
    {
        try
        {
            _logger.LogInformation("Exporting audit logs: Format={Format}, DateRange={StartDate} to {EndDate}",
                request.ExportFormat, request.StartDate, request.EndDate);

            var auditData = await _auditService.GetAuditLogsForExportAsync(
                request.StartDate,
                request.EndDate,
                request.UserId,
                request.EventType,
                request.EntityType,
                request.IncludeDetails);

            var exportData = await GenerateExportDataAsync(auditData, request.ExportFormat);
            
            var contentType = GetContentType(request.ExportFormat);
            var fileName = $"audit_logs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{request.ExportFormat.ToLower()}";

            return File(exportData, contentType, fileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest($"Invalid export parameters: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting audit logs");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while exporting audit logs");
        }
    }

    /// <summary>
    /// Gets audit statistics and summary metrics
    /// </summary>
    /// <param name="startDate">Start date for statistics</param>
    /// <param name="endDate">End date for statistics</param>
    /// <returns>Audit statistics</returns>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(AuditStatisticsResponse), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<AuditStatisticsResponse>> GetAuditStatisticsAsync(
        [FromQuery, Required] DateTime startDate,
        [FromQuery, Required] DateTime endDate)
    {
        try
        {
            _logger.LogInformation("Retrieving audit statistics for period: {StartDate} to {EndDate}", startDate, endDate);

            var statistics = await _auditService.GetAuditStatisticsAsync(startDate, endDate);

            var response = new AuditStatisticsResponse
            {
                ReportPeriod = new DateRange { StartDate = startDate, EndDate = endDate },
                TotalEvents = statistics.TotalEvents,
                EventsByType = statistics.EventsByType,
                EventsByUser = statistics.EventsByUser,
                SecurityEvents = statistics.SecurityEvents,
                HighRiskEvents = statistics.HighRiskEvents,
                ComplianceViolations = statistics.ComplianceViolations,
                AverageEventsPerDay = statistics.AverageEventsPerDay,
                PeakEventDate = statistics.PeakEventDate,
                TopUsers = statistics.TopUsers.Take(10).ToList()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit statistics");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while retrieving audit statistics");
        }
    }

    /// <summary>
    /// Executes security analysis using predefined queries
    /// </summary>
    /// <param name="request">Security analysis request</param>
    /// <returns>Security analysis results</returns>
    [HttpPost("security-analysis")]
    [ProducesResponseType(typeof(SecurityAnalysisReportResponse), (int)HttpStatusCode.OK)]
    [Authorize(Roles = "Administrator,SecurityAnalyst")]
    public async Task<ActionResult<SecurityAnalysisReportResponse>> ExecuteSecurityAnalysisAsync(
        [FromBody] SecurityAnalysisRequest request)
    {
        try
        {
            _logger.LogInformation("Executing security analysis: {AnalysisType}", request.AnalysisType);

            var analysisResults = request.AnalysisType.ToLower() switch
            {
                "security-audit" => await _specificationService.ExecuteSecurityAuditAsync(),
                "compliance-audit" => await _specificationService.ExecuteComplianceAuditAsync(),
                "privilege-escalation" => await ExecutePrivilegeEscalationAnalysisAsync(),
                "access-anomaly" => await ExecuteAccessAnomalyAnalysisAsync(),
                _ => throw new ArgumentException($"Unknown analysis type: {request.AnalysisType}")
            };

            var response = new SecurityAnalysisReportResponse
            {
                AnalysisType = request.AnalysisType,
                ExecutedAt = DateTime.UtcNow,
                Results = analysisResults.ToDictionary(
                    kvp => kvp.Key,
                    kvp => MapToSecurityAnalysisResult(kvp.Value)),
                OverallRiskLevel = DetermineOverallRiskLevel(analysisResults.Values),
                Summary = GenerateAnalysisSummary(analysisResults),
                Recommendations = GenerateSecurityRecommendations(analysisResults)
            };

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest($"Invalid analysis request: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing security analysis: {AnalysisType}", request.AnalysisType);
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while executing security analysis");
        }
    }

    /// <summary>
    /// Gets user activity audit trail
    /// </summary>
    /// <param name="userId">User ID to audit</param>
    /// <param name="startDate">Start date for audit trail</param>
    /// <param name="endDate">End date for audit trail</param>
    /// <returns>User activity audit trail</returns>
    [HttpGet("user-activity/{userId:int}")]
    [ProducesResponseType(typeof(UserActivityAuditResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<UserActivityAuditResponse>> GetUserActivityAuditAsync(
        int userId,
        [FromQuery, Required] DateTime startDate,
        [FromQuery, Required] DateTime endDate)
    {
        try
        {
            _logger.LogInformation("Retrieving user activity audit for user {UserId}, period: {StartDate} to {EndDate}",
                userId, startDate, endDate);

            var userActivity = await _auditService.GetUserActivityAuditAsync(userId, startDate, endDate);
            if (userActivity == null)
            {
                return NotFound($"User with ID {userId} not found or no activity in the specified period");
            }

            var response = new UserActivityAuditResponse
            {
                UserId = userId,
                UserName = userActivity.UserName,
                AuditPeriod = new DateRange { StartDate = startDate, EndDate = endDate },
                TotalActions = userActivity.TotalActions,
                SecurityEvents = userActivity.SecurityEvents,
                PermissionChanges = userActivity.PermissionChanges.Select(MapToPermissionChangeEvent).ToList(),
                AccessAttempts = userActivity.AccessAttempts.Select(MapToAccessAttemptEvent).ToList(),
                RiskScore = userActivity.RiskScore,
                RiskFactors = userActivity.RiskFactors,
                LastActivity = userActivity.LastActivity
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user activity audit for user {UserId}", userId);
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while retrieving user activity audit");
        }
    }

    #region Private Helper Methods

    private AuditLogResponse MapToResponse(AuditLogEntry auditLog)
    {
        return new AuditLogResponse
        {
            Id = auditLog.Id,
            EventType = auditLog.EventType,
            EventDescription = auditLog.EventDescription,
            UserId = auditLog.UserId,
            UserName = auditLog.UserName,
            EntityType = auditLog.EntityType,
            EntityId = auditLog.EntityId,
            Timestamp = auditLog.Timestamp,
            IPAddress = auditLog.IPAddress,
            UserAgent = auditLog.UserAgent,
            AdditionalData = auditLog.AdditionalData,
            RiskLevel = auditLog.RiskLevel
        };
    }

    private SecurityEventResponse MapToSecurityEventResponse(SecurityEvent securityEvent)
    {
        return new SecurityEventResponse
        {
            Id = securityEvent.Id,
            EventType = securityEvent.EventType,
            RiskLevel = securityEvent.RiskLevel,
            Description = securityEvent.Description,
            UserId = securityEvent.UserId,
            UserName = securityEvent.UserName,
            IPAddress = securityEvent.IPAddress,
            Timestamp = securityEvent.Timestamp,
            Mitigated = securityEvent.Mitigated,
            MitigationNotes = securityEvent.MitigationNotes,
            ThreatIndicators = securityEvent.ThreatIndicators,
            AffectedResources = securityEvent.AffectedResources
        };
    }

    private ComplianceRequirementResponse MapToComplianceRequirement(ComplianceRequirement requirement)
    {
        return new ComplianceRequirementResponse
        {
            RequirementId = requirement.RequirementId,
            Description = requirement.Description,
            Status = requirement.Status,
            Score = requirement.Score,
            Evidence = requirement.Evidence,
            LastAssessed = requirement.LastAssessed
        };
    }

    private ComplianceViolationResponse MapToComplianceViolation(ComplianceViolation violation)
    {
        return new ComplianceViolationResponse
        {
            ViolationId = violation.ViolationId,
            RequirementId = violation.RequirementId,
            Description = violation.Description,
            Severity = violation.Severity,
            DetectedAt = violation.DetectedAt,
            Status = violation.Status,
            RemedyAction = violation.RemedyAction,
            AffectedEntities = violation.AffectedEntities
        };
    }

    private SecurityAnalysisResultResponse MapToSecurityAnalysisResult(SecurityAnalysisResult result)
    {
        return new SecurityAnalysisResultResponse
        {
            EntityType = result.EntityType,
            TotalEntities = result.TotalEntities,
            MatchingEntities = result.MatchingEntities,
            RiskLevel = result.RiskLevel,
            QueryDescription = result.QueryDescription,
            AnalysisDate = result.AnalysisDate,
            MatchPercentage = result.AdditionalMetrics.ContainsKey("MatchPercentage") 
                ? (double)result.AdditionalMetrics["MatchPercentage"] : 0,
            ExecutionTimeMs = result.AdditionalMetrics.ContainsKey("ExecutionTimeMs") 
                ? (long)result.AdditionalMetrics["ExecutionTimeMs"] : 0,
            Entities = result.Entities.Take(100).ToList() // Limit to first 100 for API response
        };
    }

    private PermissionChangeEventResponse MapToPermissionChangeEvent(PermissionChangeEvent changeEvent)
    {
        return new PermissionChangeEventResponse
        {
            Timestamp = changeEvent.Timestamp,
            ChangeType = changeEvent.ChangeType,
            Permission = changeEvent.Permission,
            OldValue = changeEvent.OldValue,
            NewValue = changeEvent.NewValue,
            ChangedBy = changeEvent.ChangedBy,
            Reason = changeEvent.Reason
        };
    }

    private AccessAttemptEventResponse MapToAccessAttemptEvent(AccessAttemptEvent accessEvent)
    {
        return new AccessAttemptEventResponse
        {
            Timestamp = accessEvent.Timestamp,
            ResourceUri = accessEvent.ResourceUri,
            HttpMethod = accessEvent.HttpMethod,
            Granted = accessEvent.Granted,
            DenialReason = accessEvent.DenialReason,
            IPAddress = accessEvent.IPAddress,
            UserAgent = accessEvent.UserAgent
        };
    }

    private async Task<byte[]> GenerateExportDataAsync(IEnumerable<AuditLogEntry> auditData, string format)
    {
        return format.ToUpper() switch
        {
            "CSV" => GenerateCsvData(auditData),
            "JSON" => GenerateJsonData(auditData),
            "XML" => GenerateXmlData(auditData),
            _ => throw new ArgumentException($"Unsupported export format: {format}")
        };
    }

    private byte[] GenerateCsvData(IEnumerable<AuditLogEntry> auditData)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Id,EventType,Description,UserId,UserName,EntityType,EntityId,Timestamp,IPAddress,RiskLevel");
        
        foreach (var entry in auditData)
        {
            csv.AppendLine($"{entry.Id},{entry.EventType},{entry.EventDescription},{entry.UserId},{entry.UserName},{entry.EntityType},{entry.EntityId},{entry.Timestamp:yyyy-MM-dd HH:mm:ss},{entry.IPAddress},{entry.RiskLevel}");
        }
        
        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    private byte[] GenerateJsonData(IEnumerable<AuditLogEntry> auditData)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(auditData, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        return Encoding.UTF8.GetBytes(json);
    }

    private byte[] GenerateXmlData(IEnumerable<AuditLogEntry> auditData)
    {
        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<AuditLogs>");
        
        foreach (var entry in auditData)
        {
            xml.AppendLine("  <AuditLog>");
            xml.AppendLine($"    <Id>{entry.Id}</Id>");
            xml.AppendLine($"    <EventType>{entry.EventType}</EventType>");
            xml.AppendLine($"    <Description>{System.Security.SecurityElement.Escape(entry.EventDescription)}</Description>");
            xml.AppendLine($"    <UserId>{entry.UserId}</UserId>");
            xml.AppendLine($"    <UserName>{System.Security.SecurityElement.Escape(entry.UserName)}</UserName>");
            xml.AppendLine($"    <EntityType>{entry.EntityType}</EntityType>");
            xml.AppendLine($"    <EntityId>{entry.EntityId}</EntityId>");
            xml.AppendLine($"    <Timestamp>{entry.Timestamp:yyyy-MM-ddTHH:mm:ss}</Timestamp>");
            xml.AppendLine($"    <IPAddress>{entry.IPAddress}</IPAddress>");
            xml.AppendLine($"    <RiskLevel>{entry.RiskLevel}</RiskLevel>");
            xml.AppendLine("  </AuditLog>");
        }
        
        xml.AppendLine("</AuditLogs>");
        return Encoding.UTF8.GetBytes(xml.ToString());
    }

    private string GetContentType(string format)
    {
        return format.ToUpper() switch
        {
            "CSV" => "text/csv",
            "JSON" => "application/json",
            "XML" => "application/xml",
            _ => "application/octet-stream"
        };
    }

    private async Task<Dictionary<string, SecurityAnalysisResult>> ExecutePrivilegeEscalationAnalysisAsync()
    {
        var results = new Dictionary<string, SecurityAnalysisResult>();

        // Users with excessive roles
        var excessiveRolesSpec = ComplexPermissionQueries.ComplianceQueries.UsersViolatingLeastPrivilege();
        results["ExcessiveRoles"] = await _specificationService.ExecuteSecurityAnalysisAsync(excessiveRolesSpec, "Users with privilege escalation risk");

        // Users with sensitive access
        var sensitiveAccessSpec = ComplexPermissionQueries.SensitiveAccessAnalysis.UsersWithUnjustifiedSensitiveAccess();
        results["SensitiveAccess"] = await _specificationService.ExecuteSecurityAnalysisAsync(sensitiveAccessSpec, "Users with unjustified sensitive access");

        // Users with wildcard permissions
        var wildcardSpec = ComplexPermissionQueries.SensitiveAccessAnalysis.UsersWithWildcardPermissions();
        results["WildcardPermissions"] = await _specificationService.ExecuteSecurityAnalysisAsync(wildcardSpec, "Users with wildcard permissions");

        return results;
    }

    private async Task<Dictionary<string, SecurityAnalysisResult>> ExecuteAccessAnomalyAnalysisAsync()
    {
        var results = new Dictionary<string, SecurityAnalysisResult>();

        // Users accessing multiple sensitive systems
        var multiSystemSpec = ComplexPermissionQueries.AccessPatternAnalysis.UsersAccessingMultipleSensitiveSystems();
        results["MultiSystemAccess"] = await _specificationService.ExecuteSecurityAnalysisAsync(multiSystemSpec, "Users with multi-system access");

        // Users with external system access
        var externalAccessSpec = ComplexPermissionQueries.AccessPatternAnalysis.ExternalSystemAccessUsers();
        results["ExternalAccess"] = await _specificationService.ExecuteSecurityAnalysisAsync(externalAccessSpec, "Users with external system access");

        // Users with conflicting roles
        var conflictingRolesSpec = ComplexPermissionQueries.SecurityAnalysis.UsersWithConflictingRoles();
        results["ConflictingRoles"] = await _specificationService.ExecuteSecurityAnalysisAsync(conflictingRolesSpec, "Users with conflicting roles");

        return results;
    }

    private string DetermineOverallRiskLevel(IEnumerable<SecurityAnalysisResult> results)
    {
        var riskLevels = results.Select(r => r.RiskLevel).ToList();
        
        if (riskLevels.Contains("Critical")) return "Critical";
        if (riskLevels.Contains("High")) return "High";
        if (riskLevels.Contains("Medium")) return "Medium";
        return "Low";
    }

    private string GenerateAnalysisSummary(Dictionary<string, SecurityAnalysisResult> results)
    {
        var totalIssues = results.Values.Sum(r => r.MatchingEntities);
        var highRiskIssues = results.Values.Count(r => r.RiskLevel == "High" || r.RiskLevel == "Critical");

        return $"Security analysis completed. Found {totalIssues} total security issues across {results.Count} analysis categories. {highRiskIssues} categories have high or critical risk levels requiring immediate attention.";
    }

    private List<string> GenerateSecurityRecommendations(Dictionary<string, SecurityAnalysisResult> results)
    {
        var recommendations = new List<string>();

        foreach (var result in results.Values)
        {
            if (result.RiskLevel == "Critical" || result.RiskLevel == "High")
            {
                recommendations.Add($"Address {result.QueryDescription}: {result.MatchingEntities} entities require immediate review");
            }
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("Continue monitoring security metrics and maintain current security posture");
        }

        return recommendations;
    }

    #endregion
}