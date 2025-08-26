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
using System.Reflection;
using System.Text.Json;

namespace ACS.WebApi.Controllers;

/// <summary>
/// Controller for administrative operations including system configuration, maintenance, and tenant management
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "Administrator,SystemAdmin")]
[Produces("application/json")]
public class AdminController : ControllerBase
{
    private readonly ACS.Infrastructure.HealthChecks.IHealthCheckService _healthCheckService;
    private readonly ACS.Service.Services.IAuditService _auditService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        ACS.Infrastructure.HealthChecks.IHealthCheckService healthCheckService,
        ACS.Service.Services.IAuditService auditService,
        ILogger<AdminController> logger)
    {
        _healthCheckService = healthCheckService;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Gets comprehensive system health status
    /// </summary>
    /// <returns>System health information</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(SystemHealthResponse), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<SystemHealthResponse>> GetSystemHealthAsync()
    {
        try
        {
            _logger.LogDebug("Retrieving system health status");

            var healthStatus = await _healthCheckService.CheckHealthAsync();
            var systemInfo = await GetSystemInformationInternalAsync();

            var response = new SystemHealthResponse
            {
                OverallStatus = healthStatus.Status.ToString(),
                CheckedAt = DateTime.UtcNow,
                HealthChecks = healthStatus.Entries.Select(e => MapToHealthCheckResponse(e.Key, e.Value.Status, e.Value.Duration, e.Value.Description, e.Value.Data, e.Value.Exception)).ToList(),
                SystemInformation = systemInfo,
                Uptime = GetSystemUptime(),
                Version = GetApplicationVersion()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system health");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while retrieving system health");
        }
    }

    /// <summary>
    /// Gets comprehensive system information
    /// </summary>
    /// <returns>System information</returns>
    [HttpGet("system-info")]
    [ProducesResponseType(typeof(SystemInformationResponse), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<SystemInformationResponse>> GetSystemInformationAsync()
    {
        try
        {
            var systemInfo = await GetSystemInformationInternalAsync();
            return Ok(systemInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system information");
            return StatusCode((int)HttpStatusCode.InternalServerError, "An error occurred while retrieving system information");
        }
    }

    #region Private Helper Methods

    private HealthCheckResponse MapToHealthCheckResponse(string name, Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus status, TimeSpan duration, string? description, IReadOnlyDictionary<string, object>? data, Exception? exception)
    {
        return new HealthCheckResponse
        {
            Name = name,
            Status = status.ToString(),
            Duration = duration,
            Description = description ?? string.Empty,
            Data = data?.ToDictionary(k => k.Key, v => v.Value) ?? new Dictionary<string, object>(),
            Exception = exception?.Message
        };
    }

    private Task<SystemInformationResponse> GetSystemInformationInternalAsync()
    {
        return Task.FromResult(new SystemInformationResponse
        {
            ApplicationName = "Access Control System",
            Version = GetApplicationVersion(),
            BuildDate = GetBuildDate(),
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
            MachineName = Environment.MachineName,
            ProcessorCount = Environment.ProcessorCount,
            OSVersion = Environment.OSVersion.ToString(),
            WorkingSet = Environment.WorkingSet,
            ManagedMemory = GC.GetTotalMemory(false),
            Uptime = GetSystemUptime(),
            StartTime = GetApplicationStartTime(),
            ThreadCount = System.Diagnostics.Process.GetCurrentProcess().Threads.Count,
            HandleCount = System.Diagnostics.Process.GetCurrentProcess().HandleCount
        });
    }

    private string GetApplicationVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "Unknown";
    }

    private DateTime GetBuildDate()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var attribute = assembly.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>();
        if (DateTime.TryParse(attribute?.InformationalVersion, out var buildDate))
        {
            return buildDate;
        }
        return System.IO.File.GetLastWriteTime(assembly.Location);
    }

    private TimeSpan GetSystemUptime()
    {
        return DateTime.UtcNow - GetApplicationStartTime();
    }

    private DateTime GetApplicationStartTime()
    {
        return System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
    }

    #endregion
}