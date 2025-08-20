using ACS.Infrastructure.Compression;
using ACS.Infrastructure.Optimization;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using System.IO.Compression;

namespace ACS.WebApi.Middleware;

/// <summary>
/// Middleware for serving pre-compressed static files
/// </summary>
public class StaticFileCompressionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ICompressionService _compressionService;
    private readonly IBundlingService _bundlingService;
    private readonly IFileProvider _fileProvider;
    private readonly IContentTypeProvider _contentTypeProvider;
    private readonly ILogger<StaticFileCompressionMiddleware> _logger;
    private readonly StaticFileCompressionOptions _options;

    public StaticFileCompressionMiddleware(
        RequestDelegate next,
        ICompressionService compressionService,
        IBundlingService bundlingService,
        IWebHostEnvironment environment,
        ILogger<StaticFileCompressionMiddleware> logger,
        StaticFileCompressionOptions options)
    {
        _next = next;
        _compressionService = compressionService;
        _bundlingService = bundlingService;
        _fileProvider = environment.WebRootFileProvider;
        _contentTypeProvider = new FileExtensionContentTypeProvider();
        _logger = logger;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;
        
        // Check if this is a bundle request
        if (path != null && path.StartsWith("/bundles/", StringComparison.OrdinalIgnoreCase))
        {
            await ServeBundleAsync(context, path);
            return;
        }

        // Check if this is a static file request
        if (path != null && ShouldHandleStaticFile(path))
        {
            await ServeStaticFileAsync(context, path);
            return;
        }

        await _next(context);
    }

    private async Task ServeBundleAsync(HttpContext context, string path)
    {
        try
        {
            // Extract bundle name from path
            var bundleName = Path.GetFileNameWithoutExtension(path.Replace("/bundles/", ""));
            
            // Get bundle content
            var content = await _bundlingService.GetBundleContentAsync(bundleName);
            var bundle = await _bundlingService.GetBundleAsync(bundleName);
            
            // Set content type
            context.Response.ContentType = bundle.Type switch
            {
                BundleType.JavaScript => "application/javascript",
                BundleType.Css => "text/css",
                _ => "text/plain"
            };

            // Set caching headers
            SetCacheHeaders(context, bundle.Hash);

            // Check if client supports compression
            var acceptEncoding = context.Request.Headers["Accept-Encoding"].ToString();
            if (!string.IsNullOrEmpty(acceptEncoding) && _options.EnableCompression)
            {
                var contentBytes = System.Text.Encoding.UTF8.GetBytes(content);
                var algorithm = SelectBestAlgorithm(acceptEncoding);
                
                if (algorithm != CompressionAlgorithm.None)
                {
                    contentBytes = await CompressAsync(contentBytes, algorithm);
                    SetCompressionHeader(context, algorithm);
                }
                
                await context.Response.Body.WriteAsync(contentBytes);
            }
            else
            {
                await context.Response.WriteAsync(content);
            }
        }
        catch (KeyNotFoundException)
        {
            context.Response.StatusCode = 404;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving bundle {Path}", path);
            context.Response.StatusCode = 500;
        }
    }

    private async Task ServeStaticFileAsync(HttpContext context, string path)
    {
        var fileInfo = _fileProvider.GetFileInfo(path);
        
        if (!fileInfo.Exists)
        {
            await _next(context);
            return;
        }

        // Determine content type
        if (!_contentTypeProvider.TryGetContentType(path, out var contentType))
        {
            contentType = "application/octet-stream";
        }
        
        context.Response.ContentType = contentType;

        // Set caching headers
        var etag = GenerateETag(fileInfo);
        SetCacheHeaders(context, etag);

        // Check if-none-match
        if (context.Request.Headers["If-None-Match"] == etag)
        {
            context.Response.StatusCode = 304; // Not Modified
            return;
        }

        // Read file content
        using var stream = fileInfo.CreateReadStream();
        var buffer = new byte[fileInfo.Length];
        await stream.ReadAsync(buffer);

        // Check if compression is beneficial
        var acceptEncoding = context.Request.Headers["Accept-Encoding"].ToString();
        if (!string.IsNullOrEmpty(acceptEncoding) && 
            _options.EnableCompression &&
            _compressionService.ShouldCompress(contentType, buffer.Length))
        {
            var algorithm = SelectBestAlgorithm(acceptEncoding);
            
            if (algorithm != CompressionAlgorithm.None)
            {
                // Try to serve pre-compressed version first
                var preCompressed = await TryGetPreCompressedAsync(path, algorithm);
                if (preCompressed != null)
                {
                    buffer = preCompressed;
                }
                else
                {
                    // Compress on the fly
                    buffer = await CompressAsync(buffer, algorithm);
                }
                
                SetCompressionHeader(context, algorithm);
            }
        }

        context.Response.ContentLength = buffer.Length;
        await context.Response.Body.WriteAsync(buffer);
    }

    private bool ShouldHandleStaticFile(string path)
    {
        // Check if path matches static file patterns
        var extension = Path.GetExtension(path);
        return _options.StaticFileExtensions.Contains(extension.ToLowerInvariant());
    }

    private async Task<byte[]?> TryGetPreCompressedAsync(string path, CompressionAlgorithm algorithm)
    {
        var extension = algorithm switch
        {
            CompressionAlgorithm.Brotli => ".br",
            CompressionAlgorithm.Gzip => ".gz",
            _ => null
        };

        if (extension == null)
            return null;

        var compressedPath = path + extension;
        var compressedFile = _fileProvider.GetFileInfo(compressedPath);
        
        if (compressedFile.Exists)
        {
            using var stream = compressedFile.CreateReadStream();
            var buffer = new byte[compressedFile.Length];
            await stream.ReadAsync(buffer);
            return buffer;
        }

        return null;
    }

    private async Task<byte[]> CompressAsync(byte[] content, CompressionAlgorithm algorithm)
    {
        using var output = new MemoryStream();
        
        Stream compressionStream = algorithm switch
        {
            CompressionAlgorithm.Brotli => new BrotliStream(output, CompressionLevel.Optimal),
            CompressionAlgorithm.Gzip => new GZipStream(output, CompressionLevel.Optimal),
            _ => throw new NotSupportedException()
        };

        using (compressionStream)
        {
            await compressionStream.WriteAsync(content);
        }

        return output.ToArray();
    }

    private CompressionAlgorithm SelectBestAlgorithm(string acceptEncoding)
    {
        var encodings = acceptEncoding.ToLowerInvariant();

        if (encodings.Contains("br") && _options.EnableBrotli)
            return CompressionAlgorithm.Brotli;

        if (encodings.Contains("gzip") && _options.EnableGzip)
            return CompressionAlgorithm.Gzip;

        return CompressionAlgorithm.None;
    }

    private void SetCompressionHeader(HttpContext context, CompressionAlgorithm algorithm)
    {
        var encoding = algorithm switch
        {
            CompressionAlgorithm.Brotli => "br",
            CompressionAlgorithm.Gzip => "gzip",
            _ => null
        };

        if (encoding != null)
        {
            context.Response.Headers["Content-Encoding"] = encoding;
            context.Response.Headers.Add("Vary", "Accept-Encoding");
        }
    }

    private void SetCacheHeaders(HttpContext context, string etag)
    {
        context.Response.Headers["ETag"] = etag;
        context.Response.Headers["Cache-Control"] = _options.CacheControl;
        
        if (_options.MaxAge > 0)
        {
            context.Response.Headers["Expires"] = DateTime.UtcNow.AddSeconds(_options.MaxAge).ToString("R");
        }
    }

    private string GenerateETag(IFileInfo fileInfo)
    {
        var hash = $"{fileInfo.Name}-{fileInfo.Length}-{fileInfo.LastModified:yyyyMMddHHmmss}";
        using var md5 = System.Security.Cryptography.MD5.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(hash);
        var hashBytes = md5.ComputeHash(bytes);
        return Convert.ToBase64String(hashBytes);
    }
}

/// <summary>
/// Options for static file compression
/// </summary>
public class StaticFileCompressionOptions
{
    public bool EnableCompression { get; set; } = true;
    public bool EnableBrotli { get; set; } = true;
    public bool EnableGzip { get; set; } = true;
    public string CacheControl { get; set; } = "public, max-age=31536000";
    public int MaxAge { get; set; } = 31536000; // 1 year
    public HashSet<string> StaticFileExtensions { get; set; } = new()
    {
        ".js", ".css", ".html", ".htm", ".json", ".xml",
        ".txt", ".ico", ".jpg", ".jpeg", ".png", ".gif",
        ".svg", ".webp", ".woff", ".woff2", ".ttf", ".eot"
    };
}