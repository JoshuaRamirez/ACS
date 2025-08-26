using ACS.Infrastructure.Authentication;
using ACS.WebApi.DTOs;
using ACS.Service.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IAuthenticationService _authenticationService;

    public AuthController(
        ILogger<AuthController> logger, 
        JwtTokenService jwtTokenService,
        IAuthenticationService authenticationService)
    {
        _logger = logger;
        _jwtTokenService = jwtTokenService;
        _authenticationService = authenticationService;
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
            _logger.LogInformation("Login attempt for user {Email} in tenant {TenantId}", request.Email, request.TenantId);

            // Authenticate using the real authentication service
            var authResult = await _authenticationService.AuthenticateAsync(request.Email, request.Password, request.TenantId);
            
            if (!authResult.IsSuccess)
            {
                _logger.LogWarning("Authentication failed for user {Email}: {Error}", request.Email, authResult.ErrorMessage);
                return Unauthorized(new { Message = authResult.ErrorMessage });
            }

            // Generate JWT token
            var token = _jwtTokenService.GenerateToken(
                userId: authResult.UserId.ToString(),
                tenantId: authResult.TenantId,
                roles: authResult.Roles,
                additionalClaims: new Dictionary<string, object>
                {
                    ["user_name"] = authResult.UserName,
                    ["email"] = authResult.Email,
                    ["login_time"] = DateTime.UtcNow.ToString("O"),
                    ["client_ip"] = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
                });

            var response = new LoginResponse
            {
                Token = token,
                TokenType = "Bearer",
                ExpiresIn = TimeSpan.FromHours(24).TotalSeconds,
                UserId = authResult.UserId.ToString(),
                UserName = authResult.UserName,
                Email = authResult.Email,
                TenantId = authResult.TenantId,
                Roles = authResult.Roles.ToList()
            };

            _logger.LogInformation("Successful login for user {Email} in tenant {TenantId}", request.Email, request.TenantId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user {Email}", request.Email);
            return StatusCode(500, new { Message = "Authentication service error" });
        }
    }

    /// <summary>
    /// Refresh an existing JWT token
    /// </summary>
    [HttpPost("refresh")]
    [Authorize]
    public Task<ActionResult<LoginResponse>> RefreshToken()
    {
        try
        {
            var authContext = HttpContext.Items["AuthContext"] as AuthenticationContext;
            if (authContext == null)
            {
                return Task.FromResult<ActionResult<LoginResponse>>(Unauthorized(new { Message = "Invalid authentication context" }));
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
            return Task.FromResult<ActionResult<LoginResponse>>(Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return Task.FromResult<ActionResult<LoginResponse>>(StatusCode(500, new { Message = "Token refresh error" }));
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
    /// Change password for authenticated user
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            var authContext = HttpContext.Items["AuthContext"] as AuthenticationContext;
            if (authContext == null)
            {
                return Unauthorized();
            }

            if (!int.TryParse(authContext.UserId, out var userId))
            {
                return BadRequest(new { Message = "Invalid user context" });
            }

            var success = await _authenticationService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
            
            if (!success)
            {
                return BadRequest(new { Message = "Current password is incorrect" });
            }

            _logger.LogInformation("Password changed successfully for user {UserId}", userId);
            return Ok(new { Message = "Password changed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password");
            return StatusCode(500, new { Message = "Password change failed" });
        }
    }

    /// <summary>
    /// Unlock a user account (admin only)
    /// </summary>
    [HttpPost("unlock-account")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> UnlockAccount([FromBody] UnlockAccountRequest request)
    {
        try
        {
            await _authenticationService.UnlockAccountAsync(request.Email);
            _logger.LogInformation("Account unlocked for {Email}", request.Email);
            return Ok(new { Message = "Account unlocked successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlocking account for {Email}", request.Email);
            return StatusCode(500, new { Message = "Failed to unlock account" });
        }
    }
}

// DTOs for authentication
public record LoginRequest
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
}

public record LoginResponse
{
    public string Token { get; init; } = string.Empty;
    public string TokenType { get; init; } = string.Empty;
    public double ExpiresIn { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public List<string> Roles { get; init; } = new();
}

public record ChangePasswordRequest
{
    public string CurrentPassword { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
}

public record UnlockAccountRequest
{
    public string Email { get; init; } = string.Empty;
}

public record UserInfoResponse
{
    public string UserId { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public List<string> Roles { get; init; } = new();
    public DateTime AuthenticatedAt { get; init; }
    public Dictionary<string, string> Claims { get; init; } = new();
}