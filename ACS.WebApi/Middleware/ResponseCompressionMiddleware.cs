using ACS.Infrastructure.Compression;
using ACS.Infrastructure.Optimization;
using Microsoft.Extensions.Options;
using System.IO.Compression;
using System.Text;

namespace ACS.WebApi.Middleware;

/// <summary>
/// Middleware for response compression and optimization
/// </summary>
public class ResponseCompressionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ICompressionService _compressionService;
    private readonly IMinificationService _minificationService;
    private readonly ILogger<ResponseCompressionMiddleware> _logger;
    private readonly ResponseCompressionOptions _options;

    public ResponseCompressionMiddleware(
        RequestDelegate next,
        ICompressionService compressionService,
        IMinificationService minificationService,
        ILogger<ResponseCompressionMiddleware> logger,
        IOptions<ResponseCompressionOptions> options)
    {
        _next = next;
        _compressionService = compressionService;
        _minificationService = minificationService;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Check if client accepts compression
        var acceptEncoding = context.Request.Headers["Accept-Encoding"].ToString();
        var supportsCompression = !string.IsNullOrEmpty(acceptEncoding);

        if (!supportsCompression || !_options.EnableCompression)
        {
            await _next(context);
            return;
        }

        // Replace response body stream
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            // Process the request
            await _next(context);

            // Check if we should process the response
            if (ShouldProcessResponse(context))
            {
                // Get response content
                responseBody.Seek(0, SeekOrigin.Begin);
                var responseContent = await new StreamReader(responseBody).ReadToEndAsync();
                
                // Minify if applicable
                if (_options.EnableMinification && 
                    _minificationService.ShouldMinify(context.Response.ContentType, !_options.IsDevelopment))
                {
                    responseContent = await _minificationService.MinifyAsync(
                        responseContent, 
                        context.Response.ContentType);
                }

                // Compress if beneficial
                var contentBytes = Encoding.UTF8.GetBytes(responseContent);
                if (_compressionService.ShouldCompress(context.Response.ContentType, contentBytes.Length))
                {
                    var algorithm = SelectCompressionAlgorithm(acceptEncoding);
                    if (algorithm != CompressionAlgorithm.None)
                    {
                        contentBytes = await CompressContentAsync(contentBytes, algorithm);
                        SetCompressionHeaders(context, algorithm);
                    }
                }

                // Write to original stream
                context.Response.ContentLength = contentBytes.Length;
                await originalBodyStream.WriteAsync(contentBytes);
            }
            else
            {
                // Copy original response
                responseBody.Seek(0, SeekOrigin.Begin);
                await responseBody.CopyToAsync(originalBodyStream);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in response compression middleware");
            
            // Try to copy original response
            try
            {
                responseBody.Seek(0, SeekOrigin.Begin);
                await responseBody.CopyToAsync(originalBodyStream);
            }
            catch
            {
                // Best effort - response may be corrupted
            }
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private bool ShouldProcessResponse(HttpContext context)
    {
        // Don't process if already compressed
        if (context.Response.Headers.ContainsKey("Content-Encoding"))
            return false;

        // Don't process error responses
        if (context.Response.StatusCode >= 400)
            return false;

        // Check content type
        var contentType = context.Response.ContentType;
        if (string.IsNullOrEmpty(contentType))
            return false;

        // Check excluded paths
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        if (_options.ExcludedPaths.Any(excluded => path.StartsWith(excluded)))
            return false;

        return true;
    }

    private CompressionAlgorithm SelectCompressionAlgorithm(string acceptEncoding)
    {
        var encodings = acceptEncoding.ToLowerInvariant();

        // Prefer Brotli
        if (encodings.Contains("br") && _options.EnableBrotli)
            return CompressionAlgorithm.Brotli;

        // Fall back to Gzip
        if (encodings.Contains("gzip") && _options.EnableGzip)
            return CompressionAlgorithm.Gzip;

        // Try Deflate
        if (encodings.Contains("deflate") && _options.EnableDeflate)
            return CompressionAlgorithm.Deflate;

        return CompressionAlgorithm.None;
    }

    private async Task<byte[]> CompressContentAsync(byte[] content, CompressionAlgorithm algorithm)
    {
        using var output = new MemoryStream();

        Stream compressionStream = algorithm switch
        {
            CompressionAlgorithm.Brotli => new BrotliStream(output, _options.CompressionLevel),
            CompressionAlgorithm.Gzip => new GZipStream(output, _options.CompressionLevel),
            CompressionAlgorithm.Deflate => new DeflateStream(output, _options.CompressionLevel),
            _ => throw new NotSupportedException($"Compression algorithm {algorithm} not supported")
        };

        using (compressionStream)
        {
            await compressionStream.WriteAsync(content);
        }

        var compressed = output.ToArray();
        
        _logger.LogTrace("Compressed response from {Original} to {Compressed} bytes using {Algorithm}",
            content.Length, compressed.Length, algorithm);

        return compressed;
    }

    private void SetCompressionHeaders(HttpContext context, CompressionAlgorithm algorithm)
    {
        var encoding = algorithm switch
        {
            CompressionAlgorithm.Brotli => "br",
            CompressionAlgorithm.Gzip => "gzip",
            CompressionAlgorithm.Deflate => "deflate",
            _ => null
        };

        if (encoding != null)
        {
            context.Response.Headers["Content-Encoding"] = encoding;
            context.Response.Headers.Add("Vary", "Accept-Encoding");
        }
    }
}

/// <summary>
/// Options for response compression middleware
/// </summary>
public class ResponseCompressionOptions
{
    public bool EnableCompression { get; set; } = true;
    public bool EnableMinification { get; set; } = true;
    public bool EnableBrotli { get; set; } = true;
    public bool EnableGzip { get; set; } = true;
    public bool EnableDeflate { get; set; } = false;
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;
    public bool IsDevelopment { get; set; }
    public List<string> ExcludedPaths { get; set; } = new()
    {
        "/health",
        "/metrics",
        "/swagger",
        "/api/diagnostics"
    };
}