using ACS.Infrastructure.Authentication;
using ACS.WebApi.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly JwtTokenService _jwtTokenService;

    public AuthController(ILogger<AuthController> logger, JwtTokenService jwtTokenService)
    {
        _logger = logger;
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>
    /// Authenticate user and generate JWT token
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            _logger.LogInformation("Login attempt for user {Username} in tenant {TenantId}", request.Username, request.TenantId);

            // In a real implementation, this would validate credentials against the database
            // For now, we'll use basic validation for demonstration
            if (!await ValidateCredentialsAsync(request))
            {
                _logger.LogWarning("Invalid credentials for user {Username}", request.Username);
                return Unauthorized(new { Message = "Invalid credentials" });
            }

            // Get user roles (in real implementation, fetch from database)
            var roles = await GetUserRolesAsync(request.Username, request.TenantId);
            
            // Generate JWT token
            var token = _jwtTokenService.GenerateToken(
                userId: request.Username,
                tenantId: request.TenantId,
                roles: roles,
                additionalClaims: new Dictionary<string, object>
                {
                    ["login_time"] = DateTime.UtcNow.ToString("O"),
                    ["client_ip"] = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
                });

            var response = new LoginResponse
            {
                Token = token,
                TokenType = "Bearer",
                ExpiresIn = TimeSpan.FromHours(24).TotalSeconds,
                UserId = request.Username,
                TenantId = request.TenantId,
                Roles = roles.ToList()
            };

            _logger.LogInformation("Successful login for user {Username} in tenant {TenantId}", request.Username, request.TenantId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user {Username}", request.Username);
            return StatusCode(500, new { Message = "Authentication service error" });
        }
    }

    /// <summary>
    /// Refresh an existing JWT token
    /// </summary>
    [HttpPost("refresh")]
    [Authorize]
    public async Task<ActionResult<LoginResponse>> RefreshToken()
    {
        try
        {
            var authContext = HttpContext.Items["AuthContext"] as AuthenticationContext;
            if (authContext == null)
            {
                return Unauthorized(new { Message = "Invalid authentication context" });
            }

            // Generate new token with existing claims
            var token = _jwtTokenService.GenerateToken(
                userId: authContext.UserId,
                tenantId: authContext.TenantId,
                roles: authContext.Roles,
                additionalClaims: new Dictionary<string, object>
                {
                    ["refresh_time"] = DateTime.UtcNow.ToString("O"),
                    ["client_ip"] = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
                });

            var response = new LoginResponse
            {
                Token = token,
                TokenType = "Bearer",
                ExpiresIn = TimeSpan.FromHours(24).TotalSeconds,
                UserId = authContext.UserId,
                TenantId = authContext.TenantId,
                Roles = authContext.Roles.ToList()
            };

            _logger.LogInformation("Token refreshed for user {UserId} in tenant {TenantId}", authContext.UserId, authContext.TenantId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return StatusCode(500, new { Message = "Token refresh error" });
        }
    }

    /// <summary>
    /// Get current user information
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public ActionResult<UserInfoResponse> GetCurrentUser()
    {
        var authContext = HttpContext.Items["AuthContext"] as AuthenticationContext;
        if (authContext == null)
        {
            return Unauthorized();
        }

        var response = new UserInfoResponse
        {
            UserId = authContext.UserId,
            TenantId = authContext.TenantId,
            Roles = authContext.Roles.ToList(),
            AuthenticatedAt = authContext.AuthenticatedAt,
            Claims = authContext.Principal.Claims.ToDictionary(c => c.Type, c => c.Value)
        };

        return Ok(response);
    }

    /// <summary>
    /// Validate user credentials (mock implementation)
    /// </summary>
    private async Task<bool> ValidateCredentialsAsync(LoginRequest request)
    {
        // In a real implementation, this would:
        // 1. Hash the password and compare with stored hash
        // 2. Check account lockout status
        // 3. Verify tenant association
        // 4. Check account expiration/status

        // For demonstration, accept any non-empty credentials
        await Task.Delay(100); // Simulate database lookup

        return !string.IsNullOrEmpty(request.Username) && 
               !string.IsNullOrEmpty(request.Password) && 
               !string.IsNullOrEmpty(request.TenantId);
    }

    /// <summary>
    /// Get user roles for tenant (mock implementation)
    /// </summary>
    private async Task<IEnumerable<string>> GetUserRolesAsync(string username, string tenantId)
    {
        // In a real implementation, this would fetch roles from the ACS domain service
        await Task.Delay(50); // Simulate database lookup

        // Return mock roles based on username patterns
        return username.ToLowerInvariant() switch
        {
            var u when u.Contains("admin") => new[] { "Admin", "Operator", "User" },
            var u when u.Contains("operator") => new[] { "Operator", "User" },
            _ => new[] { "User" }
        };
    }
}

// DTOs for authentication
public record LoginRequest
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
}

public record LoginResponse
{
    public string Token { get; init; } = string.Empty;
    public string TokenType { get; init; } = string.Empty;
    public double ExpiresIn { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public List<string> Roles { get; init; } = new();
}

public record UserInfoResponse
{
    public string UserId { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public List<string> Roles { get; init; } = new();
    public DateTime AuthenticatedAt { get; init; }
    public Dictionary<string, string> Claims { get; init; } = new();
}