using ACS.VerticalHost.Services;
using ACS.VerticalHost.Commands;
using ACS.Infrastructure.Authentication;
using ACS.Service.Services;
using Microsoft.Extensions.Logging;
using static ACS.VerticalHost.Services.HandlerErrorHandling;
using static ACS.VerticalHost.Services.HandlerExtensions;

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
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(LoginCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { Email = command.Email, TenantId = command.TenantId }, correlationId);

        try
        {
            // Authenticate using the real authentication service
            var authResult = await _authenticationService.AuthenticateAsync(command.Email, command.Password, command.TenantId);
            
            if (!authResult.IsSuccess)
            {
                _logger.LogWarning("Authentication failed for user {Email} in tenant {TenantId}: {Error}. CorrelationId: {CorrelationId}", 
                    command.Email, command.TenantId, authResult.ErrorMessage, correlationId);
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

            var result = new AuthResult
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

            LogCommandSuccess(_logger, context, new { Email = command.Email, TenantId = command.TenantId, UserId = authResult.UserId }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            // For authentication errors, log appropriately but return controlled error response
            // to avoid exposing system details to potential attackers
            _logger.LogWarning(ex, "Authentication failed for user {Email} in tenant {TenantId}. CorrelationId: {CorrelationId}", 
                command.Email, command.TenantId, correlationId);
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
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(RefreshTokenCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { UserId = command.UserId, TenantId = command.TenantId }, correlationId);

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

            var result = new AuthResult
            {
                IsSuccess = true,
                Token = token,
                UserId = command.UserId,
                TenantId = command.TenantId,
                Roles = command.Roles,
                ExpiresIn = TimeSpan.FromHours(24).TotalSeconds
            };

            LogCommandSuccess(_logger, context, new { UserId = command.UserId, TenantId = command.TenantId }, correlationId);
            return result;
        }
        catch (Exception ex)
        {
            // For token refresh errors, log appropriately but return controlled error response
            // to avoid exposing system details to potential attackers
            _logger.LogWarning(ex, "Token refresh failed for user {UserId} in tenant {TenantId}. CorrelationId: {CorrelationId}", 
                command.UserId, command.TenantId, correlationId);
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
        var correlationId = GetCorrelationId();
        var context = GetContext(nameof(ChangePasswordCommandHandler), nameof(HandleAsync));
        
        LogOperationStart(_logger, context, new { UserId = command.UserId }, correlationId);

        try
        {
            var success = await _authenticationService.ChangePasswordAsync(command.UserId, command.CurrentPassword, command.NewPassword);
            
            if (success)
            {
                LogCommandSuccess(_logger, context, new { UserId = command.UserId }, correlationId);
            }
            else
            {
                _logger.LogWarning("Password change failed for user {UserId}. CorrelationId: {CorrelationId}", 
                    command.UserId, correlationId);
            }

            return success;
        }
        catch (Exception ex)
        {
            // For password change errors, log appropriately but return controlled response
            // to avoid exposing system details in security-sensitive operations
            _logger.LogWarning(ex, "Password change failed for user {UserId}. CorrelationId: {CorrelationId}", 
                command.UserId, correlationId);
            return false;
        }
    }
}