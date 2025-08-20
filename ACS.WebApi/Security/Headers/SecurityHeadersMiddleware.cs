using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Text;

namespace ACS.WebApi.Security.Headers;

/// <summary>
/// Security headers configuration options
/// </summary>
public class SecurityHeadersOptions
{
    /// <summary>
    /// Enable Strict-Transport-Security header
    /// </summary>
    public bool UseHsts { get; set; } = true;

    /// <summary>
    /// HSTS max age in seconds (default: 1 year)
    /// </summary>
    public int HstsMaxAge { get; set; } = 31536000;

    /// <summary>
    /// Include subdomains in HSTS
    /// </summary>
    public bool HstsIncludeSubDomains { get; set; } = true;

    /// <summary>
    /// Enable HSTS preload
    /// </summary>
    public bool HstsPreload { get; set; } = true;

    /// <summary>
    /// Content Security Policy directives
    /// </summary>
    public ContentSecurityPolicyOptions ContentSecurityPolicy { get; set; } = new();

    /// <summary>
    /// X-Frame-Options value
    /// </summary>
    public string XFrameOptions { get; set; } = "DENY";

    /// <summary>
    /// X-Content-Type-Options value
    /// </summary>
    public string XContentTypeOptions { get; set; } = "nosniff";

    /// <summary>
    /// X-XSS-Protection value
    /// </summary>
    public string XXssProtection { get; set; } = "1; mode=block";

    /// <summary>
    /// Referrer-Policy value
    /// </summary>
    public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";

    /// <summary>
    /// Permissions-Policy directives
    /// </summary>
    public PermissionsPolicyOptions PermissionsPolicy { get; set; } = new();

    /// <summary>
    /// Enable Cross-Origin-Embedder-Policy
    /// </summary>
    public bool UseCrossOriginEmbedderPolicy { get; set; } = true;

    /// <summary>
    /// Cross-Origin-Embedder-Policy value
    /// </summary>
    public string CrossOriginEmbedderPolicy { get; set; } = "require-corp";

    /// <summary>
    /// Enable Cross-Origin-Opener-Policy
    /// </summary>
    public bool UseCrossOriginOpenerPolicy { get; set; } = true;

    /// <summary>
    /// Cross-Origin-Opener-Policy value
    /// </summary>
    public string CrossOriginOpenerPolicy { get; set; } = "same-origin";

    /// <summary>
    /// Enable Cross-Origin-Resource-Policy
    /// </summary>
    public bool UseCrossOriginResourcePolicy { get; set; } = true;

    /// <summary>
    /// Cross-Origin-Resource-Policy value
    /// </summary>
    public string CrossOriginResourcePolicy { get; set; } = "same-origin";

    /// <summary>
    /// Custom headers to add
    /// </summary>
    public Dictionary<string, string> CustomHeaders { get; set; } = new();

    /// <summary>
    /// Headers to remove from response
    /// </summary>
    public HashSet<string> RemoveHeaders { get; set; } = new()
    {
        "Server",
        "X-Powered-By",
        "X-AspNet-Version",
        "X-AspNetMvc-Version"
    };
}

/// <summary>
/// Content Security Policy configuration
/// </summary>
public class ContentSecurityPolicyOptions
{
    public bool Enabled { get; set; } = true;
    public bool ReportOnly { get; set; } = false;
    public string? ReportUri { get; set; }
    
    public string DefaultSrc { get; set; } = "'self'";
    public string? ScriptSrc { get; set; } = "'self' 'unsafe-inline' 'unsafe-eval'";
    public string? StyleSrc { get; set; } = "'self' 'unsafe-inline'";
    public string? ImgSrc { get; set; } = "'self' data: https:";
    public string? FontSrc { get; set; } = "'self' data:";
    public string? ConnectSrc { get; set; } = "'self'";
    public string? MediaSrc { get; set; } = "'self'";
    public string? ObjectSrc { get; set; } = "'none'";
    public string? FrameSrc { get; set; } = "'none'";
    public string? FrameAncestors { get; set; } = "'none'";
    public string? BaseUri { get; set; } = "'self'";
    public string? FormAction { get; set; } = "'self'";
    public string? ManifestSrc { get; set; } = "'self'";
    public string? WorkerSrc { get; set; } = "'self'";
    public bool UpgradeInsecureRequests { get; set; } = true;
    public bool BlockAllMixedContent { get; set; } = true;

    /// <summary>
    /// Generate CSP header value
    /// </summary>
    public string GenerateCspHeader()
    {
        var directives = new List<string>();

        if (!string.IsNullOrWhiteSpace(DefaultSrc))
            directives.Add($"default-src {DefaultSrc}");
        
        if (!string.IsNullOrWhiteSpace(ScriptSrc))
            directives.Add($"script-src {ScriptSrc}");
        
        if (!string.IsNullOrWhiteSpace(StyleSrc))
            directives.Add($"style-src {StyleSrc}");
        
        if (!string.IsNullOrWhiteSpace(ImgSrc))
            directives.Add($"img-src {ImgSrc}");
        
        if (!string.IsNullOrWhiteSpace(FontSrc))
            directives.Add($"font-src {FontSrc}");
        
        if (!string.IsNullOrWhiteSpace(ConnectSrc))
            directives.Add($"connect-src {ConnectSrc}");
        
        if (!string.IsNullOrWhiteSpace(MediaSrc))
            directives.Add($"media-src {MediaSrc}");
        
        if (!string.IsNullOrWhiteSpace(ObjectSrc))
            directives.Add($"object-src {ObjectSrc}");
        
        if (!string.IsNullOrWhiteSpace(FrameSrc))
            directives.Add($"frame-src {FrameSrc}");
        
        if (!string.IsNullOrWhiteSpace(FrameAncestors))
            directives.Add($"frame-ancestors {FrameAncestors}");
        
        if (!string.IsNullOrWhiteSpace(BaseUri))
            directives.Add($"base-uri {BaseUri}");
        
        if (!string.IsNullOrWhiteSpace(FormAction))
            directives.Add($"form-action {FormAction}");
        
        if (!string.IsNullOrWhiteSpace(ManifestSrc))
            directives.Add($"manifest-src {ManifestSrc}");
        
        if (!string.IsNullOrWhiteSpace(WorkerSrc))
            directives.Add($"worker-src {WorkerSrc}");
        
        if (UpgradeInsecureRequests)
            directives.Add("upgrade-insecure-requests");
        
        if (BlockAllMixedContent)
            directives.Add("block-all-mixed-content");
        
        if (!string.IsNullOrWhiteSpace(ReportUri))
            directives.Add($"report-uri {ReportUri}");

        return string.Join("; ", directives);
    }
}

/// <summary>
/// Permissions Policy configuration
/// </summary>
public class PermissionsPolicyOptions
{
    public bool Enabled { get; set; } = true;
    
    public string Accelerometer { get; set; } = "()";
    public string AmbientLightSensor { get; set; } = "()";
    public string Autoplay { get; set; } = "()";
    public string Battery { get; set; } = "()";
    public string Camera { get; set; } = "()";
    public string DisplayCapture { get; set; } = "()";
    public string DocumentDomain { get; set; } = "()";
    public string EncryptedMedia { get; set; } = "(self)";
    public string Fullscreen { get; set; } = "(self)";
    public string Gamepad { get; set; } = "()";
    public string Geolocation { get; set; } = "()";
    public string Gyroscope { get; set; } = "()";
    public string LayoutAnimations { get; set; } = "()";
    public string LegacyImageFormats { get; set; } = "()";
    public string Magnetometer { get; set; } = "()";
    public string Microphone { get; set; } = "()";
    public string Midi { get; set; } = "()";
    public string OversizedImages { get; set; } = "*(2.0)";
    public string Payment { get; set; } = "()";
    public string PictureInPicture { get; set; } = "()";
    public string PublicKeyCredentialsGet { get; set; } = "()";
    public string SpeakerSelection { get; set; } = "()";
    public string SyncXhr { get; set; } = "()";
    public string UnoptimizedImages { get; set; } = "()";
    public string UnsizedMedia { get; set; } = "()";
    public string Usb { get; set; } = "()";
    public string ScreenWakeLock { get; set; } = "()";
    public string WebShare { get; set; } = "()";
    public string XrSpatialTracking { get; set; } = "()";

    /// <summary>
    /// Generate Permissions-Policy header value
    /// </summary>
    public string GeneratePermissionsPolicyHeader()
    {
        var policies = new List<string>();

        AddPolicy(policies, "accelerometer", Accelerometer);
        AddPolicy(policies, "ambient-light-sensor", AmbientLightSensor);
        AddPolicy(policies, "autoplay", Autoplay);
        AddPolicy(policies, "battery", Battery);
        AddPolicy(policies, "camera", Camera);
        AddPolicy(policies, "display-capture", DisplayCapture);
        AddPolicy(policies, "document-domain", DocumentDomain);
        AddPolicy(policies, "encrypted-media", EncryptedMedia);
        AddPolicy(policies, "fullscreen", Fullscreen);
        AddPolicy(policies, "gamepad", Gamepad);
        AddPolicy(policies, "geolocation", Geolocation);
        AddPolicy(policies, "gyroscope", Gyroscope);
        AddPolicy(policies, "layout-animations", LayoutAnimations);
        AddPolicy(policies, "legacy-image-formats", LegacyImageFormats);
        AddPolicy(policies, "magnetometer", Magnetometer);
        AddPolicy(policies, "microphone", Microphone);
        AddPolicy(policies, "midi", Midi);
        AddPolicy(policies, "oversized-images", OversizedImages);
        AddPolicy(policies, "payment", Payment);
        AddPolicy(policies, "picture-in-picture", PictureInPicture);
        AddPolicy(policies, "publickey-credentials-get", PublicKeyCredentialsGet);
        AddPolicy(policies, "speaker-selection", SpeakerSelection);
        AddPolicy(policies, "sync-xhr", SyncXhr);
        AddPolicy(policies, "unoptimized-images", UnoptimizedImages);
        AddPolicy(policies, "unsized-media", UnsizedMedia);
        AddPolicy(policies, "usb", Usb);
        AddPolicy(policies, "screen-wake-lock", ScreenWakeLock);
        AddPolicy(policies, "web-share", WebShare);
        AddPolicy(policies, "xr-spatial-tracking", XrSpatialTracking);

        return string.Join(", ", policies);
    }

    private void AddPolicy(List<string> policies, string feature, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            policies.Add($"{feature}={value}");
        }
    }
}

/// <summary>
/// Middleware for adding security headers to HTTP responses
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SecurityHeadersOptions _options;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        IOptions<SecurityHeadersOptions> options,
        ILogger<SecurityHeadersMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options?.Value ?? new SecurityHeadersOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers before processing the request
        context.Response.OnStarting(() =>
        {
            AddSecurityHeaders(context);
            RemoveServerHeaders(context);
            return Task.CompletedTask;
        });

        await _next(context);
    }

    private void AddSecurityHeaders(HttpContext context)
    {
        var headers = context.Response.Headers;

        // HSTS (only for HTTPS)
        if (_options.UseHsts && context.Request.IsHttps)
        {
            var hstsValue = new StringBuilder($"max-age={_options.HstsMaxAge}");
            if (_options.HstsIncludeSubDomains)
                hstsValue.Append("; includeSubDomains");
            if (_options.HstsPreload)
                hstsValue.Append("; preload");
            
            headers["Strict-Transport-Security"] = hstsValue.ToString();
        }

        // Content Security Policy
        if (_options.ContentSecurityPolicy.Enabled)
        {
            var cspHeader = _options.ContentSecurityPolicy.GenerateCspHeader();
            if (!string.IsNullOrWhiteSpace(cspHeader))
            {
                var headerName = _options.ContentSecurityPolicy.ReportOnly 
                    ? "Content-Security-Policy-Report-Only" 
                    : "Content-Security-Policy";
                headers[headerName] = cspHeader;
            }
        }

        // X-Frame-Options
        if (!string.IsNullOrWhiteSpace(_options.XFrameOptions))
        {
            headers["X-Frame-Options"] = _options.XFrameOptions;
        }

        // X-Content-Type-Options
        if (!string.IsNullOrWhiteSpace(_options.XContentTypeOptions))
        {
            headers["X-Content-Type-Options"] = _options.XContentTypeOptions;
        }

        // X-XSS-Protection (legacy, but still useful for older browsers)
        if (!string.IsNullOrWhiteSpace(_options.XXssProtection))
        {
            headers["X-XSS-Protection"] = _options.XXssProtection;
        }

        // Referrer-Policy
        if (!string.IsNullOrWhiteSpace(_options.ReferrerPolicy))
        {
            headers["Referrer-Policy"] = _options.ReferrerPolicy;
        }

        // Permissions-Policy
        if (_options.PermissionsPolicy.Enabled)
        {
            var permissionsPolicy = _options.PermissionsPolicy.GeneratePermissionsPolicyHeader();
            if (!string.IsNullOrWhiteSpace(permissionsPolicy))
            {
                headers["Permissions-Policy"] = permissionsPolicy;
            }
        }

        // Cross-Origin-Embedder-Policy
        if (_options.UseCrossOriginEmbedderPolicy && !string.IsNullOrWhiteSpace(_options.CrossOriginEmbedderPolicy))
        {
            headers["Cross-Origin-Embedder-Policy"] = _options.CrossOriginEmbedderPolicy;
        }

        // Cross-Origin-Opener-Policy
        if (_options.UseCrossOriginOpenerPolicy && !string.IsNullOrWhiteSpace(_options.CrossOriginOpenerPolicy))
        {
            headers["Cross-Origin-Opener-Policy"] = _options.CrossOriginOpenerPolicy;
        }

        // Cross-Origin-Resource-Policy
        if (_options.UseCrossOriginResourcePolicy && !string.IsNullOrWhiteSpace(_options.CrossOriginResourcePolicy))
        {
            headers["Cross-Origin-Resource-Policy"] = _options.CrossOriginResourcePolicy;
        }

        // Custom headers
        foreach (var customHeader in _options.CustomHeaders)
        {
            headers[customHeader.Key] = customHeader.Value;
        }

        _logger.LogDebug("Security headers added to response");
    }

    private void RemoveServerHeaders(HttpContext context)
    {
        foreach (var header in _options.RemoveHeaders)
        {
            context.Response.Headers.Remove(header);
        }

        _logger.LogDebug("Server identification headers removed");
    }
}

/// <summary>
/// Extension methods for security headers
/// </summary>
public static class SecurityHeadersExtensions
{
    /// <summary>
    /// Add security headers middleware to the pipeline
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }

    /// <summary>
    /// Add security headers middleware with custom options
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, Action<SecurityHeadersOptions> configureOptions)
    {
        var options = new SecurityHeadersOptions();
        configureOptions(options);
        return app.UseMiddleware<SecurityHeadersMiddleware>(Options.Create(options));
    }

    /// <summary>
    /// Configure security headers services
    /// </summary>
    public static IServiceCollection AddSecurityHeaders(this IServiceCollection services, Action<SecurityHeadersOptions>? configureOptions = null)
    {
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<SecurityHeadersOptions>(options => { });
        }

        return services;
    }
}