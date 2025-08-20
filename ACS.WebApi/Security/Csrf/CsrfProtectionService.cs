using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace ACS.WebApi.Security.Csrf;

/// <summary>
/// Service for CSRF protection with double submit cookie pattern
/// </summary>
public interface ICsrfProtectionService
{
    /// <summary>
    /// Generate CSRF token for the current session
    /// </summary>
    string GenerateToken();

    /// <summary>
    /// Validate CSRF token from request
    /// </summary>
    bool ValidateToken(string token);

    /// <summary>
    /// Get CSRF token from request
    /// </summary>
    string? GetTokenFromRequest(HttpRequest request);

    /// <summary>
    /// Set CSRF token in response
    /// </summary>
    void SetTokenInResponse(HttpResponse response, string token);

    /// <summary>
    /// Validate request for CSRF protection
    /// </summary>
    Task<bool> ValidateRequestAsync(HttpContext context);
}

/// <summary>
/// CSRF protection options
/// </summary>
public class CsrfProtectionOptions
{
    /// <summary>
    /// Cookie name for CSRF token
    /// </summary>
    public string CookieName { get; set; } = "X-CSRF-TOKEN";

    /// <summary>
    /// Header name for CSRF token
    /// </summary>
    public string HeaderName { get; set; } = "X-CSRF-TOKEN";

    /// <summary>
    /// Form field name for CSRF token
    /// </summary>
    public string FormFieldName { get; set; } = "__RequestVerificationToken";

    /// <summary>
    /// Token expiration time
    /// </summary>
    public TimeSpan TokenExpiration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Cookie SameSite mode
    /// </summary>
    public SameSiteMode SameSite { get; set; } = SameSiteMode.Strict;

    /// <summary>
    /// Require HTTPS for cookies
    /// </summary>
    public bool RequireHttps { get; set; } = true;

    /// <summary>
    /// HTTP methods that require CSRF protection
    /// </summary>
    public HashSet<string> ProtectedMethods { get; set; } = new()
    {
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete
    };

    /// <summary>
    /// Paths to exclude from CSRF protection
    /// </summary>
    public HashSet<string> ExcludedPaths { get; set; } = new()
    {
        "/api/auth/login",
        "/api/auth/refresh",
        "/api/health",
        "/api/metrics"
    };
}

/// <summary>
/// CSRF protection service implementation
/// </summary>
public class CsrfProtectionService : ICsrfProtectionService
{
    private readonly CsrfProtectionOptions _options;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CsrfProtectionService> _logger;
    private readonly Dictionary<string, TokenInfo> _tokenCache = new();
    private readonly object _lockObject = new();

    public CsrfProtectionService(
        IOptions<CsrfProtectionOptions> options,
        IHttpContextAccessor httpContextAccessor,
        ILogger<CsrfProtectionService> logger)
    {
        _options = options?.Value ?? new CsrfProtectionOptions();
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string GenerateToken()
    {
        // Generate cryptographically secure random token
        var tokenBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenBytes);
        }

        var token = Convert.ToBase64String(tokenBytes);
        
        // Store token with expiration
        lock (_lockObject)
        {
            CleanExpiredTokens();
            _tokenCache[token] = new TokenInfo
            {
                Token = token,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(_options.TokenExpiration)
            };
        }

        _logger.LogDebug("Generated new CSRF token");
        return token;
    }

    public bool ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("CSRF token validation failed: empty token");
            return false;
        }

        lock (_lockObject)
        {
            CleanExpiredTokens();

            if (_tokenCache.TryGetValue(token, out var tokenInfo))
            {
                if (tokenInfo.ExpiresAt > DateTime.UtcNow)
                {
                    _logger.LogDebug("CSRF token validated successfully");
                    return true;
                }
                else
                {
                    _logger.LogWarning("CSRF token validation failed: token expired");
                    _tokenCache.Remove(token);
                }
            }
            else
            {
                _logger.LogWarning("CSRF token validation failed: token not found");
            }
        }

        return false;
    }

    public string? GetTokenFromRequest(HttpRequest request)
    {
        // Try to get token from header
        if (request.Headers.TryGetValue(_options.HeaderName, out var headerToken))
        {
            return headerToken.ToString();
        }

        // Try to get token from form
        if (request.HasFormContentType && request.Form.TryGetValue(_options.FormFieldName, out var formToken))
        {
            return formToken.ToString();
        }

        // Try to get token from cookie
        if (request.Cookies.TryGetValue(_options.CookieName, out var cookieToken))
        {
            return cookieToken;
        }

        return null;
    }

    public void SetTokenInResponse(HttpResponse response, string token)
    {
        // Set token in cookie
        response.Cookies.Append(_options.CookieName, token, new CookieOptions
        {
            HttpOnly = false, // Allow JavaScript to read for double-submit pattern
            Secure = _options.RequireHttps,
            SameSite = _options.SameSite,
            Expires = DateTime.UtcNow.Add(_options.TokenExpiration),
            Path = "/"
        });

        // Also set in response header for easy access
        response.Headers[_options.HeaderName] = token;

        _logger.LogDebug("CSRF token set in response");
    }

    public async Task<bool> ValidateRequestAsync(HttpContext context)
    {
        var request = context.Request;
        var path = request.Path.Value?.ToLowerInvariant() ?? string.Empty;

        // Check if path is excluded
        if (_options.ExcludedPaths.Any(excludedPath => path.StartsWith(excludedPath.ToLowerInvariant())))
        {
            _logger.LogDebug("Path {Path} is excluded from CSRF protection", path);
            return true;
        }

        // Check if method requires protection
        if (!_options.ProtectedMethods.Contains(request.Method))
        {
            _logger.LogDebug("Method {Method} does not require CSRF protection", request.Method);
            return true;
        }

        // Get token from request
        var token = GetTokenFromRequest(request);
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("CSRF validation failed: no token in request for {Method} {Path}", 
                request.Method, path);
            return false;
        }

        // Validate token
        var isValid = ValidateToken(token);
        if (!isValid)
        {
            _logger.LogWarning("CSRF validation failed: invalid token for {Method} {Path}", 
                request.Method, path);
        }

        return await Task.FromResult(isValid);
    }

    private void CleanExpiredTokens()
    {
        var now = DateTime.UtcNow;
        var expiredTokens = _tokenCache
            .Where(kvp => kvp.Value.ExpiresAt <= now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var token in expiredTokens)
        {
            _tokenCache.Remove(token);
        }

        if (expiredTokens.Any())
        {
            _logger.LogDebug("Cleaned {Count} expired CSRF tokens", expiredTokens.Count);
        }
    }

    private class TokenInfo
    {
        public string Token { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}

/// <summary>
/// CSRF protection middleware
/// </summary>
public class CsrfProtectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ICsrfProtectionService _csrfService;
    private readonly ILogger<CsrfProtectionMiddleware> _logger;

    public CsrfProtectionMiddleware(
        RequestDelegate next,
        ICsrfProtectionService csrfService,
        ILogger<CsrfProtectionMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _csrfService = csrfService ?? throw new ArgumentNullException(nameof(csrfService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip CSRF validation for GET and HEAD requests
        if (context.Request.Method == HttpMethods.Get || 
            context.Request.Method == HttpMethods.Head ||
            context.Request.Method == HttpMethods.Options)
        {
            // Generate and set token for GET requests if not present
            if (context.Request.Method == HttpMethods.Get)
            {
                var existingToken = _csrfService.GetTokenFromRequest(context.Request);
                if (string.IsNullOrWhiteSpace(existingToken))
                {
                    var newToken = _csrfService.GenerateToken();
                    _csrfService.SetTokenInResponse(context.Response, newToken);
                }
            }

            await _next(context);
            return;
        }

        // Validate CSRF token for state-changing requests
        if (!await _csrfService.ValidateRequestAsync(context))
        {
            _logger.LogWarning("CSRF validation failed for {Method} {Path} from {IP}",
                context.Request.Method,
                context.Request.Path,
                context.Connection.RemoteIpAddress);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("CSRF validation failed");
            return;
        }

        await _next(context);
    }
}

/// <summary>
/// Attribute to skip CSRF protection for specific actions
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class SkipCsrfProtectionAttribute : Attribute
{
}

/// <summary>
/// Action filter for CSRF protection
/// </summary>
public class CsrfProtectionActionFilter : IAsyncActionFilter
{
    private readonly ICsrfProtectionService _csrfService;
    private readonly ILogger<CsrfProtectionActionFilter> _logger;

    public CsrfProtectionActionFilter(
        ICsrfProtectionService csrfService,
        ILogger<CsrfProtectionActionFilter> logger)
    {
        _csrfService = csrfService ?? throw new ArgumentNullException(nameof(csrfService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Check if action or controller has SkipCsrfProtection attribute
        var skipCsrf = context.ActionDescriptor.EndpointMetadata
            .Any(m => m is SkipCsrfProtectionAttribute);

        if (skipCsrf)
        {
            await next();
            return;
        }

        // Validate CSRF token
        if (!await _csrfService.ValidateRequestAsync(context.HttpContext))
        {
            _logger.LogWarning("CSRF validation failed for action {Action}",
                context.ActionDescriptor.DisplayName);

            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }

        await next();
    }
}