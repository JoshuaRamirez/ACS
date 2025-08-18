using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ACS.Infrastructure.Authentication;

/// <summary>
/// Service for generating and validating JWT tokens for ACS authentication
/// </summary>
public class JwtTokenService
{
    private readonly ILogger<JwtTokenService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly TimeSpan _tokenLifetime;

    public JwtTokenService(ILogger<JwtTokenService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _secretKey = configuration.GetValue<string>("Authentication:Jwt:SecretKey") 
                     ?? throw new InvalidOperationException("JWT SecretKey not configured");
        _issuer = configuration.GetValue<string>("Authentication:Jwt:Issuer") ?? "ACS.WebApi";
        _audience = configuration.GetValue<string>("Authentication:Jwt:Audience") ?? "ACS.VerticalHost";
        _tokenLifetime = TimeSpan.FromHours(configuration.GetValue<int>("Authentication:Jwt:ExpirationHours", 24));
    }

    /// <summary>
    /// Generate a JWT token for an authenticated user
    /// </summary>
    public string GenerateToken(string userId, string tenantId, IEnumerable<string> roles, IDictionary<string, object>? additionalClaims = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userId),
            new("tenant_id", tenantId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Aud, _audience)
        };

        // Add role claims
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // Add additional claims if provided
        if (additionalClaims != null)
        {
            foreach (var claim in additionalClaims)
            {
                claims.Add(new Claim(claim.Key, claim.Value.ToString() ?? string.Empty));
            }
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.Add(_tokenLifetime),
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        _logger.LogDebug("Generated JWT token for user {UserId} in tenant {TenantId}", userId, tenantId);
        return tokenString;
    }

    /// <summary>
    /// Validate a JWT token and return the claims principal
    /// </summary>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5) // Allow 5 minutes clock skew
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            
            if (validatedToken is JwtSecurityToken jwtToken)
            {
                var tenantId = jwtToken.Claims.FirstOrDefault(x => x.Type == "tenant_id")?.Value;
                var userId = jwtToken.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;
                
                _logger.LogDebug("Successfully validated JWT token for user {UserId} in tenant {TenantId}", userId, tenantId);
            }

            return principal;
        }
        catch (SecurityTokenExpiredException ex)
        {
            _logger.LogWarning("JWT token has expired: {Message}", ex.Message);
            return null;
        }
        catch (SecurityTokenInvalidSignatureException ex)
        {
            _logger.LogWarning("JWT token has invalid signature: {Message}", ex.Message);
            return null;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning("JWT token validation failed: {Message}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error validating JWT token");
            return null;
        }
    }

    /// <summary>
    /// Extract tenant ID from JWT token
    /// </summary>
    public string? GetTenantIdFromToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);
            return jwtToken.Claims.FirstOrDefault(x => x.Type == "tenant_id")?.Value;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extract user ID from JWT token
    /// </summary>
    public string? GetUserIdFromToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);
            return jwtToken.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Check if token is expired without full validation
    /// </summary>
    public bool IsTokenExpired(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);
            return jwtToken.ValidTo <= DateTime.UtcNow;
        }
        catch
        {
            return true;
        }
    }
}

/// <summary>
/// Authentication context for the current request
/// </summary>
public class AuthenticationContext
{
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public IEnumerable<string> Roles { get; set; } = Enumerable.Empty<string>();
    public ClaimsPrincipal Principal { get; set; } = new();
    public DateTime AuthenticatedAt { get; set; } = DateTime.UtcNow;
    
    public bool IsInRole(string role) => Roles.Contains(role);
    public bool HasClaim(string claimType) => Principal.HasClaim(claimType, null);
    public string? GetClaimValue(string claimType) => Principal.FindFirst(claimType)?.Value;
}