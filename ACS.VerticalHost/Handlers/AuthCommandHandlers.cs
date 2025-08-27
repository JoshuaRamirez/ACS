using ACS.VerticalHost.Services;
using ACS.VerticalHost.Commands;
using ACS.Infrastructure.Authentication;
using ACS.Service.Services;
using Microsoft.Extensions.Logging;

namespace ACS.VerticalHost.Handlers;

public class LoginCommandHandler : ICommandHandler<LoginCommand, AuthResult>
{
    private readonly IAuthenticationService _authenticationService;
    private readonly JwtTokenService _jwtTokenService;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IAuthenticationService authenticationService,
        JwtTokenService jwtTokenService,
        ILogger<LoginCommandHandler> logger)
    {
        _authenticationService = authenticationService;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task<AuthResult> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Processing login for user {Email} in tenant {TenantId}", command.Email, command.TenantId);

            // Authenticate using the real authentication service
            var authResult = await _authenticationService.AuthenticateAsync(command.Email, command.Password, command.TenantId);
            
            if (!authResult.IsSuccess)
            {
                _logger.LogWarning("Authentication failed for user {Email}: {Error}", command.Email, authResult.ErrorMessage);
                return new AuthResult
                {
                    IsSuccess = false,
                    ErrorMessage = authResult.ErrorMessage ?? "Authentication failed"
                };
            }

            // Generate JWT token
            var token = _jwtTokenService.GenerateToken(
                userId: authResult.UserId.ToString(),
                tenantId: authResult.TenantId,
                roles: authResult.Roles,
                additionalClaims: new Dictionary<string, object>
                {
                    ["user_name"] = authResult.UserName ?? "",
                    ["email"] = authResult.Email ?? "",
                    ["login_time"] = DateTime.UtcNow.ToString("O")
                });

            _logger.LogInformation("Successful authentication for user {Email} in tenant {TenantId}", command.Email, command.TenantId);

            return new AuthResult
            {
                IsSuccess = true,
                Token = token,
                UserId = authResult.UserId.ToString(),
                UserName = authResult.UserName ?? "",
                Email = authResult.Email ?? "",
                TenantId = authResult.TenantId,
                Roles = authResult.Roles.ToList(),
                ExpiresIn = TimeSpan.FromHours(24).TotalSeconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during authentication for user {Email}", command.Email);
            return new AuthResult
            {
                IsSuccess = false,
                ErrorMessage = "Authentication service error"
            };
        }
    }
}

public class RefreshTokenCommandHandler : ICommandHandler<RefreshTokenCommand, AuthResult>
{
    private readonly JwtTokenService _jwtTokenService;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        JwtTokenService jwtTokenService,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task<AuthResult> HandleAsync(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        try
        {
            await Task.CompletedTask; // For async signature

            // Generate new token with existing claims
            var token = _jwtTokenService.GenerateToken(
                userId: command.UserId,
                tenantId: command.TenantId,
                roles: command.Roles,
                additionalClaims: new Dictionary<string, object>
                {
                    ["refresh_time"] = DateTime.UtcNow.ToString("O")
                });

            _logger.LogInformation("Token refreshed for user {UserId} in tenant {TenantId}", command.UserId, command.TenantId);

            return new AuthResult
            {
                IsSuccess = true,
                Token = token,
                UserId = command.UserId,
                TenantId = command.TenantId,
                Roles = command.Roles,
                ExpiresIn = TimeSpan.FromHours(24).TotalSeconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh for user {UserId}", command.UserId);
            return new AuthResult
            {
                IsSuccess = false,
                ErrorMessage = "Token refresh failed"
            };
        }
    }
}

public class ChangePasswordCommandHandler : ICommandHandler<ChangePasswordCommand, bool>
{
    private readonly IAuthenticationService _authenticationService;
    private readonly ILogger<ChangePasswordCommandHandler> _logger;

    public ChangePasswordCommandHandler(
        IAuthenticationService authenticationService,
        ILogger<ChangePasswordCommandHandler> logger)
    {
        _authenticationService = authenticationService;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var success = await _authenticationService.ChangePasswordAsync(command.UserId, command.CurrentPassword, command.NewPassword);
            
            if (success)
            {
                _logger.LogInformation("Password changed successfully for user {UserId}", command.UserId);
            }
            else
            {
                _logger.LogWarning("Password change failed for user {UserId}", command.UserId);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user {UserId}", command.UserId);
            return false;
        }
    }
}