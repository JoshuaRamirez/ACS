using ACS.Service.Domain;
using ACS.Service.Domain.Events;
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
using System.Text.Json;

namespace ACS.WebApi.Controllers;

/// <summary>
/// Controller for analytics, usage reports, and compliance reporting
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "Administrator,Analyst,Auditor")]
[Produces("application/json")]
public class ReportsController : ControllerBase
{
    private readonly IReportingService _reportingService;
    private readonly IAnalyticsService _analyticsService;
    private readonly IComplianceService _complianceService;
    private readonly IUserService _userService;
    private readonly IRoleService _roleService;
    private readonly IPermissionService _permissionService;
    private readonly IAuditService _auditService;
    private readonly ISpecificationService _specificationService;
    private readonly IDomainEventPublisher _eventPublisher;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        IReportingService reportingService,
        IAnalyticsService analyticsService,
        IComplianceService complianceService,
        IUserService userService,
        IRoleService roleService,
        IPermissionService permissionService,
        IAuditService auditService,
        ISpecificationService specificationService,
        IDomainEventPublisher eventPublisher,
        ILogger<ReportsController> logger)
    {
        _reportingService = reportingService;
        _analyticsService = analyticsService;
        _complianceService = complianceService;
        _userService = userService;
        _roleService = roleService;
        _permissionService = permissionService;
        _auditService = auditService;
        _specificationService = specificationService;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    #region User Analytics Reports

    /// <summary>
    /// Gets user activity analytics report
    /// </summary>
    /// <param name="request">User analytics request parameters</param>
    /// <returns>User activity analytics</returns>
    [HttpGet("user-analytics")]
    [ProducesResponseType(typeof(UserAnalyticsReportResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<UserAnalyticsReportResponse>> GetUserAnalyticsAsync(
        [FromQuery] UserAnalyticsReportRequest request)
    {
        try
        {
            _logger.LogInformation("Generating user analytics report for period: {StartDate} to {EndDate}",
                request.StartDate, request.EndDate);

            var analytics = await _analyticsService.GetUserAnalyticsAsync(
                request.StartDate,
                request.EndDate,
                request.IncludeInactive,
                request.GroupBy);

            var response = new UserAnalyticsReportResponse
            {
                ReportPeriod = new DateRange { StartDate = request.StartDate, EndDate = request.EndDate },
                GeneratedAt = DateTime.UtcNow,
                TotalUsers = analytics.TotalUsers,
                ActiveUsers = analytics.ActiveUsers,
                InactiveUsers = analytics.InactiveUsers,
                NewUsers = analytics.NewUsers,
                UserGrowthRate = analytics.UserGrowthRate,
                UsersByRole = analytics.UsersByRole,
                UsersByGroup = analytics.UsersByGroup,
                UsersByDepartment = analytics.UsersByDepartment,
                LoginFrequency = analytics.LoginFrequency,
                MostActiveUsers = analytics.MostActiveUsers.Take(10).ToList(),
                LeastActiveUsers = analytics.LeastActiveUsers.Take(10).ToList(),
                UserEngagementMetrics = MapToEngagementMetrics(analytics.EngagementData),
                TrendAnalysis = MapToUserTrendAnalysis(analytics.TrendData)
            };

            await _eventPublisher.PublishAsync(new ReportGeneratedEvent(
                "UserAnalytics",
                request.StartDate,
                request.EndDate,
                "User analytics report generated"));

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating user analytics report");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while generating the user analytics report");
        }
    }

    /// <summary>
    /// Gets user access patterns report
    /// </summary>
    /// <param name="request">Access patterns request parameters</param>
    /// <returns>User access patterns analysis</returns>
    [HttpGet("access-patterns")]
    [ProducesResponseType(typeof(AccessPatternsReportResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<AccessPatternsReportResponse>> GetAccessPatternsAsync(
        [FromQuery] AccessPatternsReportRequest request)
    {
        try
        {
            _logger.LogInformation("Generating access patterns report for period: {StartDate} to {EndDate}",
                request.StartDate, request.EndDate);

            var patterns = await _analyticsService.GetAccessPatternsAsync(
                request.StartDate,
                request.EndDate,
                request.ResourceFilters,
                request.UserFilters);

            var response = new AccessPatternsReportResponse
            {
                ReportPeriod = new DateRange { StartDate = request.StartDate, EndDate = request.EndDate },
                GeneratedAt = DateTime.UtcNow,
                TotalAccessAttempts = patterns.TotalAccessAttempts,
                SuccessfulAccesses = patterns.SuccessfulAccesses,
                FailedAccesses = patterns.FailedAccesses,
                SuccessRate = patterns.SuccessRate,
                MostAccessedResources = patterns.MostAccessedResources.Take(20).ToList(),
                AccessByTimeOfDay = patterns.AccessByTimeOfDay,
                AccessByDayOfWeek = patterns.AccessByDayOfWeek,
                AccessByLocation = patterns.AccessByLocation,
                UnusualAccessPatterns = patterns.UnusualAccessPatterns,
                SecurityAlerts = patterns.SecurityAlerts,
                PerformanceMetrics = MapToAccessPerformanceMetrics(patterns.PerformanceData)
            };

            await _eventPublisher.PublishAsync(new ReportGeneratedEvent(
                "AccessPatterns",
                request.StartDate,
                request.EndDate,
                "Access patterns report generated"));

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating access patterns report");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while generating the access patterns report");
        }
    }

    #endregion

    #region Permission Usage Reports

    /// <summary>
    /// Gets permission usage analytics report
    /// </summary>
    /// <param name="request">Permission usage request parameters</param>
    /// <returns>Permission usage analytics</returns>
    [HttpGet("permission-usage")]
    [ProducesResponseType(typeof(PermissionUsageReportResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<PermissionUsageReportResponse>> GetPermissionUsageAsync(
        [FromQuery] PermissionUsageReportRequest request)
    {
        try
        {
            _logger.LogInformation("Generating permission usage report for period: {StartDate} to {EndDate}",
                request.StartDate, request.EndDate);

            var usage = await _analyticsService.GetPermissionUsageAsync(
                request.StartDate,
                request.EndDate,
                request.IncludeUnused,
                request.GroupByEntity);

            var response = new PermissionUsageReportResponse
            {
                ReportPeriod = new DateRange { StartDate = request.StartDate, EndDate = request.EndDate },
                GeneratedAt = DateTime.UtcNow,
                TotalPermissions = usage.TotalPermissions,
                ActivePermissions = usage.ActivePermissions,
                UnusedPermissions = usage.UnusedPermissions,
                ExpiredPermissions = usage.ExpiredPermissions,
                MostUsedPermissions = usage.MostUsedPermissions.Take(20).ToList(),
                UnusedPermissionsList = usage.UnusedPermissionsList.Take(50).ToList(),
                PermissionsByResource = usage.PermissionsByResource,
                PermissionsByEntity = usage.PermissionsByEntity,
                PermissionsByHttpVerb = usage.PermissionsByHttpVerb,
                EfficiencyMetrics = MapToPermissionEfficiencyMetrics(usage.EfficiencyData),
                RecommendedCleanup = usage.RecommendedCleanup
            };

            await _eventPublisher.PublishAsync(new ReportGeneratedEvent(
                "PermissionUsage",
                request.StartDate,
                request.EndDate,
                "Permission usage report generated"));

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating permission usage report");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while generating the permission usage report");
        }
    }

    /// <summary>
    /// Gets role effectiveness analysis report
    /// </summary>
    /// <param name="request">Role analysis request parameters</param>
    /// <returns>Role effectiveness analysis</returns>
    [HttpGet("role-analysis")]
    [ProducesResponseType(typeof(RoleAnalysisReportResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<RoleAnalysisReportResponse>> GetRoleAnalysisAsync(
        [FromQuery] RoleAnalysisReportRequest request)
    {
        try
        {
            _logger.LogInformation("Generating role analysis report");

            var analysis = await _analyticsService.GetRoleAnalysisAsync(
                request.IncludeInactive,
                request.AnalyzeUsage,
                request.IncludeRecommendations);

            var response = new RoleAnalysisReportResponse
            {
                GeneratedAt = DateTime.UtcNow,
                TotalRoles = analysis.TotalRoles,
                ActiveRoles = analysis.ActiveRoles,
                UnusedRoles = analysis.UnusedRoles,
                OverlappingRoles = analysis.OverlappingRoles,
                RoleDistribution = analysis.RoleDistribution,
                RoleHierarchy = MapToRoleHierarchyResponse(analysis.RoleHierarchy),
                PermissionOverlap = analysis.PermissionOverlap,
                RoleEffectiveness = analysis.RoleEffectiveness,
                ConsolidationOpportunities = analysis.ConsolidationOpportunities,
                SecurityRisks = analysis.SecurityRisks,
                Recommendations = analysis.Recommendations
            };

            await _eventPublisher.PublishAsync(new ReportGeneratedEvent(
                "RoleAnalysis",
                DateTime.UtcNow.AddDays(-30),
                DateTime.UtcNow,
                "Role analysis report generated"));

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating role analysis report");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while generating the role analysis report");
        }
    }

    #endregion

    #region Security Reports

    /// <summary>
    /// Gets security dashboard report
    /// </summary>
    /// <param name="request">Security report request parameters</param>
    /// <returns>Security dashboard data</returns>
    [HttpGet("security-dashboard")]
    [ProducesResponseType(typeof(SecurityDashboardResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    [Authorize(Roles = "Administrator,SecurityAnalyst")]
    public async Task<ActionResult<SecurityDashboardResponse>> GetSecurityDashboardAsync(
        [FromQuery] SecurityReportRequest request)
    {
        try
        {
            _logger.LogInformation("Generating security dashboard report");

            var securityData = await _analyticsService.GetSecurityDashboardAsync(
                request.TimeRange,
                request.IncludeThreats,
                request.IncludeVulnerabilities);

            // Execute security analysis queries
            var securityAnalysis = await _specificationService.ExecuteSecurityAuditAsync();
            var complianceAnalysis = await _specificationService.ExecuteComplianceAuditAsync();

            var response = new SecurityDashboardResponse
            {
                GeneratedAt = DateTime.UtcNow,
                TimeRange = request.TimeRange,
                OverallSecurityScore = securityData.OverallSecurityScore,
                SecurityLevel = securityData.SecurityLevel,
                ThreatLevel = securityData.ThreatLevel,
                SecurityEvents = securityData.SecurityEvents,
                CriticalAlerts = securityData.CriticalAlerts,
                RecentIncidents = securityData.RecentIncidents.Take(10).ToList(),
                VulnerabilityAssessment = MapToVulnerabilityAssessment(securityData.Vulnerabilities),
                AccessAnomalies = securityData.AccessAnomalies,
                PermissionRisks = securityData.PermissionRisks,
                ComplianceStatus = MapToComplianceStatus(complianceAnalysis),
                SecurityTrends = securityData.SecurityTrends,
                RecommendedActions = securityData.RecommendedActions
            };

            await _eventPublisher.PublishAsync(new ReportGeneratedEvent(
                "SecurityDashboard",
                DateTime.UtcNow.AddHours(-24),
                DateTime.UtcNow,
                "Security dashboard report generated"));

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating security dashboard report");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while generating the security dashboard report");
        }
    }

    /// <summary>
    /// Gets risk assessment report
    /// </summary>
    /// <param name="request">Risk assessment request parameters</param>
    /// <returns>Risk assessment analysis</returns>
    [HttpGet("risk-assessment")]
    [ProducesResponseType(typeof(RiskAssessmentReportResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    [Authorize(Roles = "Administrator,SecurityAnalyst,RiskManager")]
    public async Task<ActionResult<RiskAssessmentReportResponse>> GetRiskAssessmentAsync(
        [FromQuery] RiskAssessmentReportRequest request)
    {
        try
        {
            _logger.LogInformation("Generating risk assessment report");

            var riskData = await _analyticsService.GetRiskAssessmentAsync(
                request.AssessmentScope,
                request.IncludeHistorical,
                request.RiskThreshold);

            var response = new RiskAssessmentReportResponse
            {
                GeneratedAt = DateTime.UtcNow,
                AssessmentScope = request.AssessmentScope,
                OverallRiskScore = riskData.OverallRiskScore,
                RiskLevel = riskData.RiskLevel,
                CriticalRisks = riskData.CriticalRisks,
                HighRisks = riskData.HighRisks,
                MediumRisks = riskData.MediumRisks,
                LowRisks = riskData.LowRisks,
                RiskCategories = riskData.RiskCategories,
                RiskByEntity = riskData.RiskByEntity,
                RiskTrends = riskData.RiskTrends,
                MitigationStrategies = riskData.MitigationStrategies,
                RiskHeatmap = riskData.RiskHeatmap,
                ComplianceImpact = riskData.ComplianceImpact,
                RecommendedActions = riskData.RecommendedActions
            };

            await _eventPublisher.PublishAsync(new ReportGeneratedEvent(
                "RiskAssessment",
                DateTime.UtcNow.AddDays(-30),
                DateTime.UtcNow,
                "Risk assessment report generated"));

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating risk assessment report");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while generating the risk assessment report");
        }
    }

    #endregion

    #region Compliance Reports

    /// <summary>
    /// Gets comprehensive compliance report
    /// </summary>
    /// <param name="request">Compliance report request parameters</param>
    /// <returns>Compliance assessment report</returns>
    [HttpGet("compliance")]
    [ProducesResponseType(typeof(ComplianceAssessmentReportResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    [Authorize(Roles = "Administrator,ComplianceOfficer,Auditor")]
    public async Task<ActionResult<ComplianceAssessmentReportResponse>> GetComplianceReportAsync(
        [FromQuery] ComplianceReportRequest request)
    {
        try
        {
            _logger.LogInformation("Generating compliance report for standards: {Standards}",
                string.Join(",", request.ComplianceStandards));

            var compliance = await _complianceService.GetComprehensiveComplianceReportAsync(
                request.ComplianceStandards,
                request.StartDate,
                request.EndDate,
                request.IncludeRemediation);

            var response = new ComplianceAssessmentReportResponse
            {
                ReportPeriod = new DateRange { StartDate = request.StartDate, EndDate = request.EndDate },
                GeneratedAt = DateTime.UtcNow,
                ComplianceStandards = request.ComplianceStandards,
                OverallComplianceScore = compliance.OverallComplianceScore,
                ComplianceLevel = compliance.ComplianceLevel,
                StandardsCompliance = compliance.StandardsCompliance.Select(MapToStandardCompliance).ToList(),
                CriticalFindings = compliance.CriticalFindings,
                Violations = compliance.Violations.Select(MapToComplianceViolation).ToList(),
                RemediationPlan = compliance.RemediationPlan,
                ControlEffectiveness = compliance.ControlEffectiveness,
                ComplianceTrends = compliance.ComplianceTrends,
                AuditReadiness = compliance.AuditReadiness,
                CertificationStatus = compliance.CertificationStatus,
                NextAssessmentDate = compliance.NextAssessmentDate
            };

            await _eventPublisher.PublishAsync(new ReportGeneratedEvent(
                "ComplianceAssessment",
                request.StartDate,
                request.EndDate,
                $"Compliance report generated for {request.ComplianceStandards.Count} standards"));

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating compliance report");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while generating the compliance report");
        }
    }

    #endregion

    #region Usage and Performance Reports

    /// <summary>
    /// Gets system usage statistics report
    /// </summary>
    /// <param name="request">Usage statistics request parameters</param>
    /// <returns>System usage statistics</returns>
    [HttpGet("usage-statistics")]
    [ProducesResponseType(typeof(UsageStatisticsReportResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<UsageStatisticsReportResponse>> GetUsageStatisticsAsync(
        [FromQuery] UsageStatisticsReportRequest request)
    {
        try
        {
            _logger.LogInformation("Generating usage statistics report for period: {StartDate} to {EndDate}",
                request.StartDate, request.EndDate);

            var usage = await _analyticsService.GetUsageStatisticsAsync(
                request.StartDate,
                request.EndDate,
                request.IncludeDetails,
                request.GroupBy);

            var response = new UsageStatisticsReportResponse
            {
                ReportPeriod = new DateRange { StartDate = request.StartDate, EndDate = request.EndDate },
                GeneratedAt = DateTime.UtcNow,
                TotalRequests = usage.TotalRequests,
                TotalUsers = usage.TotalUsers,
                ActiveSessions = usage.ActiveSessions,
                PeakConcurrentUsers = usage.PeakConcurrentUsers,
                AverageSessionDuration = usage.AverageSessionDuration,
                RequestsPerHour = usage.RequestsPerHour,
                UsageByEndpoint = usage.UsageByEndpoint,
                UsageByUser = usage.UsageByUser.Take(50).ToList(),
                UsageByTimeOfDay = usage.UsageByTimeOfDay,
                UsageByDayOfWeek = usage.UsageByDayOfWeek,
                GeographicDistribution = usage.GeographicDistribution,
                DeviceTypes = usage.DeviceTypes,
                BrowserTypes = usage.BrowserTypes,
                PerformanceMetrics = MapToUsagePerformanceMetrics(usage.PerformanceData),
                CapacityAnalysis = usage.CapacityAnalysis
            };

            await _eventPublisher.PublishAsync(new ReportGeneratedEvent(
                "UsageStatistics",
                request.StartDate,
                request.EndDate,
                "Usage statistics report generated"));

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating usage statistics report");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while generating the usage statistics report");
        }
    }

    #endregion

    #region Report Export and Scheduling

    /// <summary>
    /// Exports a report in the specified format
    /// </summary>
    /// <param name="request">Report export request</param>
    /// <returns>Exported report file</returns>
    [HttpPost("export")]
    [ProducesResponseType(typeof(FileResult), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> ExportReportAsync([FromBody] ExportReportRequest request)
    {
        try
        {
            _logger.LogInformation("Exporting report: {ReportType}, Format: {Format}",
                request.ReportType, request.ExportFormat);

            var reportData = await _reportingService.GenerateReportDataAsync(
                request.ReportType,
                request.Parameters);

            var exportData = await GenerateExportDataAsync(reportData, request.ExportFormat, request.ReportType);
            var contentType = GetContentType(request.ExportFormat);
            var fileName = $"{request.ReportType}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{request.ExportFormat.ToLower()}";

            await _eventPublisher.PublishAsync(new ReportExportedEvent(
                request.ReportType,
                request.ExportFormat,
                exportData.Length,
                "Report exported via API"));

            return File(exportData, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting report: {ReportType}", request.ReportType);
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while exporting the report");
        }
    }

    /// <summary>
    /// Schedules a recurring report
    /// </summary>
    /// <param name="request">Report schedule request</param>
    /// <returns>Scheduled report information</returns>
    [HttpPost("schedule")]
    [ProducesResponseType(typeof(ScheduledReportResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<ScheduledReportResponse>> ScheduleReportAsync([FromBody] ScheduleReportRequest request)
    {
        try
        {
            _logger.LogInformation("Scheduling report: {ReportType}, Schedule: {Schedule}",
                request.ReportType, request.Schedule);

            var scheduledReport = await _reportingService.ScheduleReportAsync(
                request.ReportType,
                request.Schedule,
                request.Parameters,
                request.ExportFormat,
                request.Recipients);

            await _eventPublisher.PublishAsync(new ReportScheduledEvent(
                request.ReportType,
                request.Schedule,
                request.Recipients.Count,
                "Report scheduled via API"));

            var response = new ScheduledReportResponse
            {
                ScheduleId = scheduledReport.ScheduleId,
                ReportType = scheduledReport.ReportType,
                Schedule = scheduledReport.Schedule,
                NextRunTime = scheduledReport.NextRunTime,
                Recipients = scheduledReport.Recipients,
                IsActive = scheduledReport.IsActive,
                CreatedAt = scheduledReport.CreatedAt,
                CreatedBy = scheduledReport.CreatedBy
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling report: {ReportType}", request.ReportType);
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while scheduling the report");
        }
    }

    /// <summary>
    /// Gets list of scheduled reports
    /// </summary>
    /// <returns>List of scheduled reports</returns>
    [HttpGet("scheduled")]
    [ProducesResponseType(typeof(IEnumerable<ScheduledReportSummaryResponse>), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<IEnumerable<ScheduledReportSummaryResponse>>> GetScheduledReportsAsync()
    {
        try
        {
            var scheduledReports = await _reportingService.GetScheduledReportsAsync();
            
            var response = scheduledReports.Select(sr => new ScheduledReportSummaryResponse
            {
                ScheduleId = sr.ScheduleId,
                ReportType = sr.ReportType,
                Schedule = sr.Schedule,
                NextRunTime = sr.NextRunTime,
                LastRunTime = sr.LastRunTime,
                IsActive = sr.IsActive,
                RecipientCount = sr.Recipients.Count
            });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving scheduled reports");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while retrieving scheduled reports");
        }
    }

    #endregion

    #region Custom Analytics

    /// <summary>
    /// Executes a custom analytics query
    /// </summary>
    /// <param name="request">Custom analytics request</param>
    /// <returns>Custom analytics results</returns>
    [HttpPost("custom-analytics")]
    [ProducesResponseType(typeof(CustomAnalyticsResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    [Authorize(Roles = "Administrator,DataAnalyst")]
    public async Task<ActionResult<CustomAnalyticsResponse>> ExecuteCustomAnalyticsAsync(
        [FromBody] CustomAnalyticsRequest request)
    {
        try
        {
            _logger.LogInformation("Executing custom analytics query: {QueryName}", request.QueryName);

            var results = await _analyticsService.ExecuteCustomQueryAsync(
                request.QueryName,
                request.Parameters,
                request.DateRange);

            var response = new CustomAnalyticsResponse
            {
                QueryName = request.QueryName,
                ExecutedAt = DateTime.UtcNow,
                ExecutionTime = results.ExecutionTime,
                TotalRecords = results.TotalRecords,
                Data = results.Data,
                Metadata = results.Metadata,
                Charts = results.Charts.Select(MapToChartData).ToList(),
                Summary = results.Summary
            };

            await _eventPublisher.PublishAsync(new CustomAnalyticsExecutedEvent(
                request.QueryName,
                results.TotalRecords,
                results.ExecutionTime,
                "Custom analytics query executed"));

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing custom analytics query: {QueryName}", request.QueryName);
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while executing the custom analytics query");
        }
    }

    #endregion

    #region Private Helper Methods

    private UserEngagementMetrics MapToEngagementMetrics(EngagementData engagementData)
    {
        return new UserEngagementMetrics
        {
            DailyActiveUsers = engagementData.DailyActiveUsers,
            WeeklyActiveUsers = engagementData.WeeklyActiveUsers,
            MonthlyActiveUsers = engagementData.MonthlyActiveUsers,
            AverageSessionsPerUser = engagementData.AverageSessionsPerUser,
            AverageTimePerSession = engagementData.AverageTimePerSession,
            RetentionRate = engagementData.RetentionRate,
            ChurnRate = engagementData.ChurnRate
        };
    }

    private UserTrendAnalysis MapToUserTrendAnalysis(TrendData trendData)
    {
        return new UserTrendAnalysis
        {
            GrowthTrend = trendData.GrowthTrend,
            ActivityTrend = trendData.ActivityTrend,
            EngagementTrend = trendData.EngagementTrend,
            PredictedGrowth = trendData.PredictedGrowth,
            SeasonalPatterns = trendData.SeasonalPatterns
        };
    }

    private AccessPerformanceMetrics MapToAccessPerformanceMetrics(PerformanceData performanceData)
    {
        return new AccessPerformanceMetrics
        {
            AverageResponseTime = performanceData.AverageResponseTime,
            P95ResponseTime = performanceData.P95ResponseTime,
            P99ResponseTime = performanceData.P99ResponseTime,
            ThroughputPerSecond = performanceData.ThroughputPerSecond,
            ErrorRate = performanceData.ErrorRate,
            AvailabilityPercentage = performanceData.AvailabilityPercentage
        };
    }

    private PermissionEfficiencyMetrics MapToPermissionEfficiencyMetrics(EfficiencyData efficiencyData)
    {
        return new PermissionEfficiencyMetrics
        {
            UtilizationRate = efficiencyData.UtilizationRate,
            RedundancyRate = efficiencyData.RedundancyRate,
            OptimizationOpportunities = efficiencyData.OptimizationOpportunities,
            MaintenanceRequired = efficiencyData.MaintenanceRequired,
            EfficiencyScore = efficiencyData.EfficiencyScore
        };
    }

    private RoleHierarchyResponse MapToRoleHierarchyResponse(RoleHierarchy hierarchy)
    {
        return new RoleHierarchyResponse
        {
            RoleId = hierarchy.RoleId,
            RoleName = hierarchy.RoleName,
            ParentRoles = hierarchy.ParentRoles.Select(MapToRoleHierarchyResponse).ToList(),
            ChildRoles = hierarchy.ChildRoles.Select(MapToRoleHierarchyResponse).ToList(),
            PermissionCount = hierarchy.PermissionCount,
            UserCount = hierarchy.UserCount,
            Depth = hierarchy.Depth
        };
    }

    private VulnerabilityAssessment MapToVulnerabilityAssessment(VulnerabilityData vulnerabilityData)
    {
        return new VulnerabilityAssessment
        {
            CriticalVulnerabilities = vulnerabilityData.CriticalVulnerabilities,
            HighVulnerabilities = vulnerabilityData.HighVulnerabilities,
            MediumVulnerabilities = vulnerabilityData.MediumVulnerabilities,
            LowVulnerabilities = vulnerabilityData.LowVulnerabilities,
            TotalVulnerabilities = vulnerabilityData.TotalVulnerabilities,
            RemediationTimeline = vulnerabilityData.RemediationTimeline,
            VulnerabilityTrends = vulnerabilityData.VulnerabilityTrends
        };
    }

    private List<ComplianceStatus> MapToComplianceStatus(Dictionary<string, SecurityAnalysisResult> complianceData)
    {
        return complianceData.Select(kvp => new ComplianceStatus
        {
            Standard = kvp.Key,
            Status = kvp.Value.RiskLevel == "Low" ? "Compliant" : "Non-Compliant",
            Score = CalculateComplianceScore(kvp.Value),
            LastAssessed = kvp.Value.AnalysisDate,
            Issues = kvp.Value.MatchingEntities
        }).ToList();
    }

    private StandardCompliance MapToStandardCompliance(ComplianceStandardResult standardResult)
    {
        return new StandardCompliance
        {
            StandardName = standardResult.StandardName,
            ComplianceScore = standardResult.ComplianceScore,
            Status = standardResult.Status,
            Requirements = standardResult.Requirements.Select(MapToComplianceRequirement).ToList(),
            LastAssessment = standardResult.LastAssessment,
            NextAssessment = standardResult.NextAssessment
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
            LastAssessed = requirement.LastAssessed,
            Category = requirement.Category,
            RiskLevel = requirement.RiskLevel
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
            AffectedEntities = violation.AffectedEntities,
            ExpectedResolution = violation.ExpectedResolution,
            AssignedTo = violation.AssignedTo
        };
    }

    private UsagePerformanceMetrics MapToUsagePerformanceMetrics(PerformanceData performanceData)
    {
        return new UsagePerformanceMetrics
        {
            AverageResponseTime = performanceData.AverageResponseTime,
            ThroughputPerSecond = performanceData.ThroughputPerSecond,
            ErrorRate = performanceData.ErrorRate,
            MemoryUtilization = performanceData.MemoryUtilization,
            CpuUtilization = performanceData.CpuUtilization,
            DatabasePerformance = performanceData.DatabasePerformance
        };
    }

    private ChartData MapToChartData(Chart chart)
    {
        return new ChartData
        {
            ChartType = chart.ChartType,
            Title = chart.Title,
            Data = chart.Data,
            Labels = chart.Labels,
            Options = chart.Options
        };
    }

    private async Task<byte[]> GenerateExportDataAsync(object reportData, string format, string reportType)
    {
        return format.ToUpper() switch
        {
            "CSV" => GenerateCsvData(reportData),
            "JSON" => GenerateJsonData(reportData),
            "XML" => GenerateXmlData(reportData),
            "EXCEL" => await GenerateExcelDataAsync(reportData, reportType),
            "PDF" => await GeneratePdfDataAsync(reportData, reportType),
            _ => throw new ArgumentException($"Unsupported export format: {format}")
        };
    }

    private byte[] GenerateCsvData(object reportData)
    {
        var json = JsonSerializer.Serialize(reportData);
        var csv = ConvertJsonToCsv(json);
        return Encoding.UTF8.GetBytes(csv);
    }

    private byte[] GenerateJsonData(object reportData)
    {
        var json = JsonSerializer.Serialize(reportData, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        return Encoding.UTF8.GetBytes(json);
    }

    private byte[] GenerateXmlData(object reportData)
    {
        var json = JsonSerializer.Serialize(reportData);
        var xml = ConvertJsonToXml(json);
        return Encoding.UTF8.GetBytes(xml);
    }

    private async Task<byte[]> GenerateExcelDataAsync(object reportData, string reportType)
    {
        // Implementation would use a library like EPPlus or ClosedXML
        // For now, return CSV data
        return GenerateCsvData(reportData);
    }

    private async Task<byte[]> GeneratePdfDataAsync(object reportData, string reportType)
    {
        // Implementation would use a library like iText or PuppeteerSharp
        // For now, return JSON data
        return GenerateJsonData(reportData);
    }

    private string ConvertJsonToCsv(string json)
    {
        // Simplified CSV conversion - would need proper implementation
        var csv = new StringBuilder();
        csv.AppendLine("Field,Value");
        
        // Basic JSON to CSV conversion logic would go here
        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        foreach (var kvp in dict ?? new Dictionary<string, object>())
        {
            csv.AppendLine($"{kvp.Key},{kvp.Value}");
        }
        
        return csv.ToString();
    }

    private string ConvertJsonToXml(string json)
    {
        // Simplified JSON to XML conversion
        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<Report>");
        xml.AppendLine($"  <Data>{System.Security.SecurityElement.Escape(json)}</Data>");
        xml.AppendLine("</Report>");
        return xml.ToString();
    }

    private string GetContentType(string format)
    {
        return format.ToUpper() switch
        {
            "CSV" => "text/csv",
            "JSON" => "application/json",
            "XML" => "application/xml",
            "EXCEL" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "PDF" => "application/pdf",
            _ => "application/octet-stream"
        };
    }

    private double CalculateComplianceScore(SecurityAnalysisResult result)
    {
        var totalEntities = result.TotalEntities;
        var matchingEntities = result.MatchingEntities;
        
        if (totalEntities == 0) return 100.0;
        
        var compliancePercentage = (double)(totalEntities - matchingEntities) / totalEntities * 100;
        return Math.Max(0, Math.Min(100, compliancePercentage));
    }

    #endregion
}