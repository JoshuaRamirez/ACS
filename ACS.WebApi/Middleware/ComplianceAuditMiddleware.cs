using ACS.Service.Compliance;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace ACS.WebApi.Middleware;

/// <summary>
/// Middleware for automatic compliance audit logging
/// </summary>
public class ComplianceAuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ComplianceAuditMiddleware> _logger;
    private readonly ComplianceAuditConfiguration _configuration;

    public ComplianceAuditMiddleware(
        RequestDelegate next,
        ILogger<ComplianceAuditMiddleware> logger,
        ComplianceAuditConfiguration configuration)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task InvokeAsync(HttpContext context, IComplianceAuditService auditService)
    {
        // Skip if audit is disabled for this path
        if (ShouldSkipAudit(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Capture request details
        var requestDetails = await CaptureRequestDetailsAsync(context.Request);
        var correlationId = GetOrCreateCorrelationId(context);

        // Store original response body stream
        var originalResponseBody = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            // Process the request
            await _next(context);

            // Capture response details
            var responseDetails = await CaptureResponseDetailsAsync(context.Response);

            // Determine compliance framework and log appropriate event
            await LogComplianceEventAsync(
                auditService,
                context,
                requestDetails,
                responseDetails,
                correlationId);

            // Copy response to original stream
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalResponseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in compliance audit middleware");
            
            // Log security incident for exceptions
            await LogSecurityIncidentAsync(auditService, context, ex, correlationId);
            
            throw;
        }
        finally
        {
            context.Response.Body = originalResponseBody;
        }
    }

    private bool ShouldSkipAudit(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant() ?? string.Empty;
        
        // Skip health checks and metrics
        if (pathValue.Contains("/health") || pathValue.Contains("/metrics"))
            return true;

        // Skip static files
        if (pathValue.Contains(".css") || pathValue.Contains(".js") || 
            pathValue.Contains(".jpg") || pathValue.Contains(".png"))
            return true;

        // Check configured exclusions
        return _configuration.ExcludedPaths.Any(excluded => 
            pathValue.StartsWith(excluded.ToLowerInvariant()));
    }

    private async Task<RequestDetails> CaptureRequestDetailsAsync(HttpRequest request)
    {
        var details = new RequestDetails
        {
            Method = request.Method,
            Path = request.Path,
            QueryString = request.QueryString.ToString(),
            Headers = GetSafeHeaders(request.Headers),
            IpAddress = GetClientIpAddress(request),
            UserAgent = request.Headers["User-Agent"].ToString()
        };

        // Capture request body for specific content types
        if (ShouldCaptureRequestBody(request))
        {
            request.EnableBuffering();
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            details.Body = await reader.ReadToEndAsync();
            request.Body.Position = 0;
        }

        return details;
    }

    private async Task<ResponseDetails> CaptureResponseDetailsAsync(HttpResponse response)
    {
        var details = new ResponseDetails
        {
            StatusCode = response.StatusCode,
            Headers = GetSafeHeaders(response.Headers)
        };

        // Capture response body for audit if needed
        if (ShouldCaptureResponseBody(response))
        {
            response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(response.Body, leaveOpen: true);
            details.Body = await reader.ReadToEndAsync();
            response.Body.Seek(0, SeekOrigin.Begin);
        }

        return details;
    }

    private async Task LogComplianceEventAsync(
        IComplianceAuditService auditService,
        HttpContext context,
        RequestDetails request,
        ResponseDetails response,
        string correlationId)
    {
        var user = context.User;
        var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        var userName = user?.FindFirst(ClaimTypes.Name)?.Value ?? "anonymous";
        var tenantId = context.Items["TenantId"]?.ToString() ?? string.Empty;

        // Determine which compliance frameworks apply
        var frameworks = DetermineApplicableFrameworks(request, response);

        foreach (var framework in frameworks)
        {
            switch (framework)
            {
                case ComplianceFramework.GDPR:
                    await LogGdprEventAsync(auditService, context, request, response, correlationId);
                    break;
                    
                case ComplianceFramework.HIPAA:
                    await LogHipaaEventAsync(auditService, context, request, response, correlationId);
                    break;
                    
                case ComplianceFramework.PCI_DSS:
                    await LogPciDssEventAsync(auditService, context, request, response, correlationId);
                    break;
                    
                case ComplianceFramework.SOC2:
                    await LogSoc2EventAsync(auditService, context, request, response, correlationId);
                    break;
            }
        }
    }

    private async Task LogGdprEventAsync(
        IComplianceAuditService auditService,
        HttpContext context,
        RequestDetails request,
        ResponseDetails response,
        string correlationId)
    {
        // Check for GDPR-relevant operations
        if (IsPersonalDataOperation(request))
        {
            var gdprEvent = new GdprAuditEvent
            {
                UserId = GetUserId(context),
                UserName = GetUserName(context),
                IpAddress = request.IpAddress,
                UserAgent = request.UserAgent,
                TenantId = GetTenantId(context),
                CorrelationId = correlationId,
                EventType = "DataAccess",
                Description = $"{request.Method} {request.Path}",
                GdprEventType = DetermineGdprEventType(request),
                DataSubjectId = ExtractDataSubjectId(request),
                LawfulBasis = "Legitimate Interest",
                Purpose = "Service Provision",
                DataCategories = DetermineDataCategories(request),
                ConsentGiven = HasValidConsent(context),
                ProcessingActivity = $"API Request: {request.Method} {request.Path}"
            };

            await auditService.LogGdprEventAsync(gdprEvent);
        }
    }

    private async Task LogHipaaEventAsync(
        IComplianceAuditService auditService,
        HttpContext context,
        RequestDetails request,
        ResponseDetails response,
        string correlationId)
    {
        // Check for HIPAA-relevant operations (PHI access)
        if (IsPhiOperation(request))
        {
            var hipaaEvent = new HipaaAuditEvent
            {
                UserId = GetUserId(context),
                UserName = GetUserName(context),
                IpAddress = request.IpAddress,
                UserAgent = request.UserAgent,
                TenantId = GetTenantId(context),
                CorrelationId = correlationId,
                EventType = "PhiAccess",
                Description = $"{request.Method} {request.Path}",
                HipaaEventType = DetermineHipaaEventType(request),
                PatientId = ExtractPatientId(request),
                ContainsPhi = true,
                PhiCategories = DeterminePhiCategories(request),
                SafeguardType = "Technical",
                EncryptionStatus = context.Request.IsHttps ? "Encrypted" : "Unencrypted",
                IsMinimumNecessary = true
            };

            await auditService.LogHipaaEventAsync(hipaaEvent);
        }
    }

    private async Task LogPciDssEventAsync(
        IComplianceAuditService auditService,
        HttpContext context,
        RequestDetails request,
        ResponseDetails response,
        string correlationId)
    {
        // Check for PCI-DSS relevant operations (payment card data)
        if (IsPaymentOperation(request))
        {
            var pciEvent = new PciDssAuditEvent
            {
                UserId = GetUserId(context),
                UserName = GetUserName(context),
                IpAddress = request.IpAddress,
                UserAgent = request.UserAgent,
                TenantId = GetTenantId(context),
                CorrelationId = correlationId,
                EventType = "CardDataAccess",
                Description = $"{request.Method} {request.Path}",
                PciEventType = DeterminePciEventType(request),
                IsEncrypted = context.Request.IsHttps,
                IsMasked = true,
                TransactionId = Guid.NewGuid().ToString(),
                PciDssRequirement = 10 // Requirement 10: Track and monitor access
            };

            await auditService.LogPciDssEventAsync(pciEvent);
        }
    }

    private async Task LogSoc2EventAsync(
        IComplianceAuditService auditService,
        HttpContext context,
        RequestDetails request,
        ResponseDetails response,
        string correlationId)
    {
        // SOC2 access control logging
        var soc2Event = new Soc2AuditEvent
        {
            UserId = GetUserId(context),
            UserName = GetUserName(context),
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent,
            TenantId = GetTenantId(context),
            CorrelationId = correlationId,
            EventType = "AccessControl",
            Description = $"{request.Method} {request.Path}",
            TrustPrinciple = Soc2TrustPrinciple.Security,
            Soc2EventType = Soc2EventType.LogicalAccess,
            ControlId = "CC6.1",
            ControlDescription = "Logical Access Controls",
            ControlEffective = response.StatusCode < 500,
            SystemComponent = "API Gateway",
            RiskLevel = DetermineRiskLevel(request, response)
        };

        await auditService.LogSoc2EventAsync(soc2Event);
    }

    private async Task LogSecurityIncidentAsync(
        IComplianceAuditService auditService,
        HttpContext context,
        Exception exception,
        string correlationId)
    {
        // Log as SOC2 security incident
        var incidentEvent = new Soc2AuditEvent
        {
            UserId = GetUserId(context),
            UserName = GetUserName(context),
            IpAddress = GetClientIpAddress(context.Request),
            UserAgent = context.Request.Headers["User-Agent"].ToString(),
            TenantId = GetTenantId(context),
            CorrelationId = correlationId,
            EventType = "SecurityIncident",
            Description = $"Exception in request processing: {exception.Message}",
            Severity = ComplianceSeverity.High,
            TrustPrinciple = Soc2TrustPrinciple.Security,
            Soc2EventType = Soc2EventType.SecurityIncident,
            ControlId = "CC7.2",
            ControlDescription = "Incident Detection and Response",
            ControlEffective = false,
            SystemComponent = "API Gateway",
            RiskLevel = "High",
            Evidence = exception.ToString()
        };

        await auditService.LogSoc2EventAsync(incidentEvent);
    }

    #region Helper Methods

    private string GetOrCreateCorrelationId(HttpContext context)
    {
        const string correlationIdHeader = "X-Correlation-Id";
        
        if (context.Request.Headers.TryGetValue(correlationIdHeader, out var correlationId))
        {
            return correlationId.ToString();
        }

        var newCorrelationId = Guid.NewGuid().ToString();
        context.Response.Headers[correlationIdHeader] = newCorrelationId;
        return newCorrelationId;
    }

    private string GetClientIpAddress(HttpRequest request)
    {
        // Check for proxy headers
        if (request.Headers.ContainsKey("X-Forwarded-For"))
        {
            return request.Headers["X-Forwarded-For"].ToString().Split(',').First().Trim();
        }

        if (request.Headers.ContainsKey("X-Real-IP"))
        {
            return request.Headers["X-Real-IP"].ToString();
        }

        return request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private Dictionary<string, string> GetSafeHeaders(IHeaderDictionary headers)
    {
        var safeHeaders = new Dictionary<string, string>();
        var sensitiveHeaders = new[] { "Authorization", "Cookie", "X-Api-Key" };

        foreach (var header in headers)
        {
            if (sensitiveHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase))
            {
                safeHeaders[header.Key] = "[REDACTED]";
            }
            else
            {
                safeHeaders[header.Key] = header.Value.ToString();
            }
        }

        return safeHeaders;
    }

    private bool ShouldCaptureRequestBody(HttpRequest request)
    {
        if (!_configuration.CaptureRequestBody)
            return false;

        var contentType = request.ContentType?.ToLowerInvariant() ?? string.Empty;
        return contentType.Contains("json") || contentType.Contains("xml");
    }

    private bool ShouldCaptureResponseBody(HttpResponse response)
    {
        if (!_configuration.CaptureResponseBody)
            return false;

        // Only capture for error responses or specific content types
        return response.StatusCode >= 400;
    }

    private List<ComplianceFramework> DetermineApplicableFrameworks(
        RequestDetails request,
        ResponseDetails response)
    {
        var frameworks = new List<ComplianceFramework>();

        // Always include SOC2 for access control
        frameworks.Add(ComplianceFramework.SOC2);

        // Add GDPR if processing personal data
        if (IsPersonalDataOperation(request))
            frameworks.Add(ComplianceFramework.GDPR);

        // Add HIPAA if processing health information
        if (IsPhiOperation(request))
            frameworks.Add(ComplianceFramework.HIPAA);

        // Add PCI-DSS if processing payment data
        if (IsPaymentOperation(request))
            frameworks.Add(ComplianceFramework.PCI_DSS);

        return frameworks;
    }

    private bool IsPersonalDataOperation(RequestDetails request)
    {
        var path = request.Path.ToLowerInvariant();
        return path.Contains("/user") || 
               path.Contains("/profile") || 
               path.Contains("/personal") ||
               path.Contains("/gdpr");
    }

    private bool IsPhiOperation(RequestDetails request)
    {
        var path = request.Path.ToLowerInvariant();
        return path.Contains("/patient") || 
               path.Contains("/health") || 
               path.Contains("/medical") ||
               path.Contains("/phi");
    }

    private bool IsPaymentOperation(RequestDetails request)
    {
        var path = request.Path.ToLowerInvariant();
        return path.Contains("/payment") || 
               path.Contains("/card") || 
               path.Contains("/transaction") ||
               path.Contains("/billing");
    }

    private GdprEventType DetermineGdprEventType(RequestDetails request)
    {
        return request.Method.ToUpperInvariant() switch
        {
            "GET" => GdprEventType.DataAccess,
            "POST" => GdprEventType.DataModification,
            "PUT" => GdprEventType.DataModification,
            "PATCH" => GdprEventType.DataModification,
            "DELETE" => GdprEventType.DataDeletion,
            _ => GdprEventType.DataAccess
        };
    }

    private HipaaEventType DetermineHipaaEventType(RequestDetails request)
    {
        return request.Method.ToUpperInvariant() switch
        {
            "GET" => HipaaEventType.PhiAccess,
            "POST" => HipaaEventType.PhiModification,
            "PUT" => HipaaEventType.PhiModification,
            "DELETE" => HipaaEventType.PhiDeletion,
            _ => HipaaEventType.PhiAccess
        };
    }

    private PciDssEventType DeterminePciEventType(RequestDetails request)
    {
        return request.Method.ToUpperInvariant() switch
        {
            "GET" => PciDssEventType.CardDataAccess,
            "POST" => PciDssEventType.CardDataStorage,
            "DELETE" => PciDssEventType.CardDataDeletion,
            _ => PciDssEventType.CardDataAccess
        };
    }

    private string ExtractDataSubjectId(RequestDetails request)
    {
        // Extract user ID from path or query
        var match = System.Text.RegularExpressions.Regex.Match(
            request.Path, @"/users?/(\w+)");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private string ExtractPatientId(RequestDetails request)
    {
        // Extract patient ID from path or query
        var match = System.Text.RegularExpressions.Regex.Match(
            request.Path, @"/patients?/(\w+)");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private List<string> DetermineDataCategories(RequestDetails request)
    {
        var categories = new List<string>();
        var path = request.Path.ToLowerInvariant();

        if (path.Contains("profile")) categories.Add("Profile Data");
        if (path.Contains("contact")) categories.Add("Contact Information");
        if (path.Contains("preference")) categories.Add("Preferences");
        if (path.Contains("activity")) categories.Add("Activity Data");

        return categories;
    }

    private string DeterminePhiCategories(RequestDetails request)
    {
        var categories = new List<string>();
        var path = request.Path.ToLowerInvariant();

        if (path.Contains("diagnosis")) categories.Add("Diagnosis");
        if (path.Contains("treatment")) categories.Add("Treatment");
        if (path.Contains("medication")) categories.Add("Medication");
        if (path.Contains("lab")) categories.Add("Lab Results");

        return string.Join(", ", categories);
    }

    private bool HasValidConsent(HttpContext context)
    {
        // Check for consent cookie or header
        return context.Request.Cookies.ContainsKey("gdpr-consent") ||
               context.Request.Headers.ContainsKey("X-GDPR-Consent");
    }

    private string DetermineRiskLevel(RequestDetails request, ResponseDetails response)
    {
        if (response.StatusCode >= 500) return "High";
        if (response.StatusCode >= 400) return "Medium";
        if (request.Method == "DELETE") return "Medium";
        if (request.Method == "POST" || request.Method == "PUT") return "Low";
        return "Info";
    }

    private string GetUserId(HttpContext context) =>
        context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";

    private string GetUserName(HttpContext context) =>
        context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "anonymous";

    private string GetTenantId(HttpContext context) =>
        context.Items["TenantId"]?.ToString() ?? string.Empty;

    #endregion

    #region Inner Classes

    private class RequestDetails
    {
        public string Method { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string QueryString { get; set; } = string.Empty;
        public Dictionary<string, string> Headers { get; set; } = new();
        public string Body { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
    }

    private class ResponseDetails
    {
        public int StatusCode { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new();
        public string Body { get; set; } = string.Empty;
    }

    #endregion
}

/// <summary>
/// Configuration for compliance audit middleware
/// </summary>
public class ComplianceAuditConfiguration
{
    public bool Enabled { get; set; } = true;
    public bool CaptureRequestBody { get; set; } = true;
    public bool CaptureResponseBody { get; set; } = false;
    public List<string> ExcludedPaths { get; set; } = new()
    {
        "/health",
        "/metrics",
        "/swagger"
    };
    public List<ComplianceFramework> EnabledFrameworks { get; set; } = new()
    {
        ComplianceFramework.GDPR,
        ComplianceFramework.SOC2,
        ComplianceFramework.HIPAA,
        ComplianceFramework.PCI_DSS
    };
}

/// <summary>
/// Extension methods for compliance audit middleware
/// </summary>
public static class ComplianceAuditMiddlewareExtensions
{
    public static IApplicationBuilder UseComplianceAudit(
        this IApplicationBuilder app,
        ComplianceAuditConfiguration? configuration = null)
    {
        configuration ??= new ComplianceAuditConfiguration();
        return app.UseMiddleware<ComplianceAuditMiddleware>(configuration);
    }
}