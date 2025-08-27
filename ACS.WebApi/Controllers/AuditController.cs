using Microsoft.AspNetCore.Mvc;
using ACS.WebApi.Services;

namespace ACS.WebApi.Controllers;

/// <summary>
/// DEMO: Pure HTTP API proxy for Audit operations - SIMPLIFIED VERSION
/// Acts as gateway to VerticalHost - contains NO business logic
/// This version uses simple types to demonstrate the proxy pattern works
/// ZERO dependencies on business services - only IVerticalHostClient
/// </summary>
[ApiController]
[Route("api/audit")]
public class AuditController : ControllerBase
{
    private readonly IVerticalHostClient _verticalClient;
    private readonly ILogger<AuditController> _logger;

    public AuditController(
        IVerticalHostClient verticalClient,
        ILogger<AuditController> logger)
    {
        _verticalClient = verticalClient;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/audit/logs - DEMO: Pure HTTP proxy to VerticalHost for audit logs
    /// </summary>
    [HttpGet("logs")]
    public async Task<ActionResult<object>> GetAuditLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? eventType = null)
    {
        try
        {
            _logger.LogInformation("DEMO: Proxying GetAuditLogs request to VerticalHost: Page={Page}, PageSize={PageSize}, EventType={EventType}", 
                page, pageSize, eventType);

            // This demonstrates the proxy pattern - in full implementation would call:
            // var response = await _verticalClient.GetAuditLogsAsync(new GetAuditLogsRequest { Page = page, PageSize = pageSize, EventType = eventType });
            await Task.CompletedTask; // Maintain async signature for future gRPC implementation
            
            var demoResponse = new
            {
                Message = "DEMO: Clean architecture proxy pattern working",
                ProxyedTo = "VerticalHost via gRPC",
                BusinessLogic = "ZERO - Pure proxy",
                Operation = "GetAuditLogs",
                Page = page,
                PageSize = pageSize,
                EventType = eventType ?? "",
                Architecture = "HTTP API -> IVerticalHostClient -> gRPC -> VerticalHost -> CommandBuffer -> Business Logic"
            };
            
            return Ok(demoResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in proxy demonstration");
            return StatusCode(500, "An error occurred in proxy demonstration");
        }
    }

    /// <summary>
    /// GET /api/audit/security-events - DEMO: Pure HTTP proxy to VerticalHost for security events
    /// </summary>
    [HttpGet("security-events")]
    public async Task<ActionResult<object>> GetSecurityEvents([FromQuery] string? riskLevel = null)
    {
        try
        {
            _logger.LogInformation("DEMO: Proxying GetSecurityEvents request to VerticalHost: RiskLevel={RiskLevel}", riskLevel);

            // This demonstrates the proxy pattern - in full implementation would call:
            // var response = await _verticalClient.GetSecurityEventsAsync(new GetSecurityEventsRequest { RiskLevel = riskLevel });
            await Task.CompletedTask; // Maintain async signature for future gRPC implementation
            
            var demoResponse = new
            {
                Message = "DEMO: Clean architecture proxy pattern working",
                ProxyedTo = "VerticalHost via gRPC",
                BusinessLogic = "ZERO - Pure proxy",
                Operation = "GetSecurityEvents",
                RiskLevel = riskLevel ?? "All",
                Architecture = "HTTP API -> IVerticalHostClient -> gRPC -> VerticalHost -> CommandBuffer -> Business Logic"
            };
            
            return Ok(demoResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in proxy demonstration");
            return StatusCode(500, "An error occurred in proxy demonstration");
        }
    }

    /// <summary>
    /// GET /api/audit/compliance-report - DEMO: Pure HTTP proxy to VerticalHost for compliance reports
    /// </summary>
    [HttpGet("compliance-report")]
    public async Task<ActionResult<object>> GetComplianceReport([FromQuery] string standard = "GDPR")
    {
        try
        {
            _logger.LogInformation("DEMO: Proxying GetComplianceReport request to VerticalHost: Standard={Standard}", standard);

            // This demonstrates the proxy pattern - in full implementation would call:
            // var response = await _verticalClient.GetComplianceReportAsync(new GetComplianceReportRequest { Standard = standard });
            await Task.CompletedTask; // Maintain async signature for future gRPC implementation
            
            var demoResponse = new
            {
                Message = "DEMO: Clean architecture proxy pattern working",
                ProxyedTo = "VerticalHost via gRPC",
                BusinessLogic = "ZERO - Pure proxy",
                Operation = "GetComplianceReport",
                Standard = standard,
                ComplianceScore = 95.5,
                Architecture = "HTTP API -> IVerticalHostClient -> gRPC -> VerticalHost -> CommandBuffer -> Business Logic"
            };
            
            return Ok(demoResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in proxy demonstration");
            return StatusCode(500, "An error occurred in proxy demonstration");
        }
    }

    /// <summary>
    /// POST /api/audit/export - DEMO: Pure HTTP proxy to VerticalHost for audit log export
    /// </summary>
    [HttpPost("export")]
    public async Task<ActionResult<object>> ExportAuditLogs([FromBody] ExportAuditRequest request)
    {
        try
        {
            _logger.LogInformation("DEMO: Proxying ExportAuditLogs request to VerticalHost: Format={Format}", request.Format);

            // This demonstrates the proxy pattern - in full implementation would call:
            // var response = await _verticalClient.ExportAuditLogsAsync(request);
            await Task.CompletedTask; // Maintain async signature for future gRPC implementation
            
            var demoResponse = new
            {
                Message = "DEMO: Clean architecture proxy pattern working",
                ProxyedTo = "VerticalHost via gRPC",
                BusinessLogic = "ZERO - Pure proxy",
                Operation = "ExportAuditLogs",
                Format = request.Format,
                Command = "Would be queued in CommandBuffer for sequential processing",
                Architecture = "HTTP API -> IVerticalHostClient -> gRPC -> VerticalHost -> CommandBuffer -> Business Logic"
            };
            
            return Ok(demoResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in proxy demonstration");
            return StatusCode(500, "An error occurred in proxy demonstration");
        }
    }
}

/// <summary>
/// Simple demo request class for audit export
/// </summary>
public class ExportAuditRequest
{
    public string Format { get; set; } = "CSV";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}