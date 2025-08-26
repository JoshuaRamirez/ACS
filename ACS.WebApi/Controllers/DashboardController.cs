using ACS.Alerting;
using ACS.Infrastructure.Monitoring;
using ACS.Infrastructure.Performance;
using ACS.Service.Data;
using ACS.Service.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics;

namespace ACS.WebApi.Controllers;

/// <summary>
/// Dashboard controller for monitoring and observability
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly ILogger<DashboardController> _logger;
    private readonly ApplicationDbContext _context;

    public DashboardController(
        ILogger<DashboardController> logger,
        ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Gets basic system overview
    /// </summary>
    /// <returns>System overview</returns>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<ActionResult> GetSystemOverviewAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving system overview");

            var overview = new
            {
                Timestamp = DateTime.UtcNow,
                Status = "Healthy",
                UsersCount = await _context.Users.CountAsync(),
                GroupsCount = await _context.Groups.CountAsync(),
                RolesCount = await _context.Roles.CountAsync(),
                Uptime = GetSystemUptime()
            };

            return Ok(overview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system overview");
            return StatusCode(500, "An error occurred while retrieving system overview");
        }
    }

    /// <summary>
    /// Gets basic system health status
    /// </summary>
    /// <returns>Health status</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), 200)]
    public ActionResult GetHealthStatus()
    {
        try
        {
            var health = new
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Uptime = GetSystemUptime(),
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
            };

            return Ok(health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving health status");
            return StatusCode(500, "An error occurred while retrieving health status");
        }
    }

    private TimeSpan GetSystemUptime()
    {
        return DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
    }
}