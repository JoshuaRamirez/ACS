using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Text;

namespace ACS.Infrastructure.Compression;

/// <summary>
/// Implementation of compression service with multiple algorithm support
/// </summary>
public class CompressionService : ICompressionService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CompressionService> _logger;
    private readonly CompressionSettings _settings;

    public CompressionService(
        IConfiguration configuration,
        ILogger<CompressionService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _settings = LoadCompressionSettings();
    }

    public async Task<byte[]> CompressAsync(byte[] data, CompressionLevel level = CompressionLevel.Optimal)
    {
        if (data == null || data.Length == 0)
            return data ?? Array.Empty<byte>();

        var compressionLevel = MapCompressionLevel(level);
        
        using var output = new MemoryStream();
        
        // Try Brotli first (best compression ratio)
        if (_settings.EnableBrotli)
        {
            try
            {
                using (var compressor = new BrotliStream(output, compressionLevel))
                {
                    await compressor.WriteAsync(data, 0, data.Length);
                }
                
                var compressed = output.ToArray();
                if (compressed.Length < data.Length)
                {
                    _logger.LogTrace("Compressed {Original} bytes to {Compressed} bytes using Brotli ({Ratio:P})", 
                        data.Length, compressed.Length, (double)compressed.Length / data.Length);
                    return compressed;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Brotli compression failed, falling back to Gzip");
            }
        }
        
        // Fall back to Gzip
        output.SetLength(0);
        using (var compressor = new GZipStream(output, compressionLevel))
        {
            await compressor.WriteAsync(data, 0, data.Length);
        }
        
        var gzipCompressed = output.ToArray();
        
        // Only return compressed if it's actually smaller
        if (gzipCompressed.Length < data.Length)
        {
            _logger.LogTrace("Compressed {Original} bytes to {Compressed} bytes using Gzip ({Ratio:P})", 
                data.Length, gzipCompressed.Length, (double)gzipCompressed.Length / data.Length);
            return gzipCompressed;
        }
        
        _logger.LogTrace("Compression not beneficial for {Size} bytes of data", data.Length);
        return data;
    }

    public async Task<byte[]> DecompressAsync(byte[] compressedData)
    {
        if (compressedData == null || compressedData.Length == 0)
            return compressedData ?? Array.Empty<byte>();

        // Try to detect compression type
        if (IsBrotliCompressed(compressedData))
        {
            return await DecompressBrotliAsync(compressedData);
        }
        else if (IsGzipCompressed(compressedData))
        {
            return await DecompressGzipAsync(compressedData);
        }
        
        // Not compressed
        return compressedData;
    }

    public async Task<byte[]> CompressStringAsync(string text, CompressionLevel level = CompressionLevel.Optimal)
    {
        if (string.IsNullOrEmpty(text))
            return Array.Empty<byte>();
        
        var bytes = Encoding.UTF8.GetBytes(text);
        return await CompressAsync(bytes, level);
    }

    public async Task<string> DecompressStringAsync(byte[] compressedData)
    {
        var decompressed = await DecompressAsync(compressedData);
        return Encoding.UTF8.GetString(decompressed);
    }

    public CompressionAlgorithm GetOptimalAlgorithm(string contentType, long contentLength)
    {
        // Don't compress if too small
        if (contentLength < _settings.MinSizeToCompress)
            return CompressionAlgorithm.None;
        
        // Don't compress if too large (streaming would be better)
        if (contentLength > _settings.MaxSizeToCompress)
            return CompressionAlgorithm.None;
        
        // Check if content type is compressible
        if (!IsCompressibleContentType(contentType))
            return CompressionAlgorithm.None;
        
        // Use Brotli for text content (best compression)
        if (_settings.EnableBrotli && IsTextContent(contentType))
            return CompressionAlgorithm.Brotli;
        
        // Use Gzip as default
        return CompressionAlgorithm.Gzip;
    }

    public bool ShouldCompress(string contentType, long contentLength)
    {
        return GetOptimalAlgorithm(contentType, contentLength) != CompressionAlgorithm.None;
    }

    private async Task<byte[]> DecompressBrotliAsync(byte[] compressedData)
    {
        using var input = new MemoryStream(compressedData);
        using var output = new MemoryStream();
        using var decompressor = new BrotliStream(input, System.IO.Compression.CompressionMode.Decompress);
        
        await decompressor.CopyToAsync(output);
        return output.ToArray();
    }

    private async Task<byte[]> DecompressGzipAsync(byte[] compressedData)
    {
        using var input = new MemoryStream(compressedData);
        using var output = new MemoryStream();
        using var decompressor = new GZipStream(input, System.IO.Compression.CompressionMode.Decompress);
        
        await decompressor.CopyToAsync(output);
        return output.ToArray();
    }

    private bool IsBrotliCompressed(byte[] data)
    {
        // Brotli doesn't have a standard magic number, but we can try to detect
        // This is a heuristic approach
        return data.Length > 4 && data[0] == 0xCE && data[1] == 0xB2 && data[2] == 0xCF && data[3] == 0x81;
    }

    private bool IsGzipCompressed(byte[] data)
    {
        return data.Length > 2 && data[0] == 0x1F && data[1] == 0x8B;
    }

    private bool IsCompressibleContentType(string contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return false;
        
        var lowerType = contentType.ToLowerInvariant();
        
        // Already compressed formats
        if (_settings.ExcludedContentTypes.Any(excluded => lowerType.Contains(excluded)))
            return false;
        
        // Compressible types
        return _settings.IncludedContentTypes.Any(included => lowerType.Contains(included));
    }

    private bool IsTextContent(string contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return false;
        
        var lowerType = contentType.ToLowerInvariant();
        return lowerType.Contains("text/") || 
               lowerType.Contains("application/json") || 
               lowerType.Contains("application/xml") ||
               lowerType.Contains("application/javascript");
    }

    private System.IO.Compression.CompressionLevel MapCompressionLevel(CompressionLevel level)
    {
        return level switch
        {
            CompressionLevel.Fastest => System.IO.Compression.CompressionLevel.Fastest,
            CompressionLevel.Maximum => System.IO.Compression.CompressionLevel.SmallestSize,
            _ => System.IO.Compression.CompressionLevel.Optimal
        };
    }

    private CompressionSettings LoadCompressionSettings()
    {
        var settings = new CompressionSettings();
        _configuration.GetSection("Compression").Bind(settings);
        return settings;
    }
}

/// <summary>
/// Compression configuration settings
/// </summary>
public class CompressionSettings
{
    public bool EnableCompression { get; set; } = true;
    public bool EnableBrotli { get; set; } = true;
    public bool EnableGzip { get; set; } = true;
    public long MinSizeToCompress { get; set; } = 1024; // 1KB
    public long MaxSizeToCompress { get; set; } = 10 * 1024 * 1024; // 10MB
    
    public List<string> IncludedContentTypes { get; set; } = new()
    {
        "text/",
        "application/json",
        "application/xml",
        "application/javascript",
        "application/x-javascript",
        "text/css",
        "text/html",
        "text/plain",
        "application/x-font-ttf",
        "application/vnd.ms-fontobject",
        "image/svg+xml"
    };
    
    public List<string> ExcludedContentTypes { get; set; } = new()
    {
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
        "video/",
        "audio/",
        "application/zip",
        "application/gzip",
        "application/x-gzip",
        "application/x-compressed",
        "application/x-7z-compressed"
    };
}