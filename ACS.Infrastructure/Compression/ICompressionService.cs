namespace ACS.Infrastructure.Compression;

/// <summary>
/// Service for handling response compression and optimization
/// </summary>
public interface ICompressionService
{
    /// <summary>
    /// Compresses data using the optimal algorithm
    /// </summary>
    Task<byte[]> CompressAsync(byte[] data, CompressionLevel level = CompressionLevel.Optimal);
    
    /// <summary>
    /// Decompresses data
    /// </summary>
    Task<byte[]> DecompressAsync(byte[] compressedData);
    
    /// <summary>
    /// Compresses a string
    /// </summary>
    Task<byte[]> CompressStringAsync(string text, CompressionLevel level = CompressionLevel.Optimal);
    
    /// <summary>
    /// Decompresses to a string
    /// </summary>
    Task<string> DecompressStringAsync(byte[] compressedData);
    
    /// <summary>
    /// Gets the best compression algorithm for the content type
    /// </summary>
    CompressionAlgorithm GetOptimalAlgorithm(string contentType, long contentLength);
    
    /// <summary>
    /// Checks if content should be compressed
    /// </summary>
    bool ShouldCompress(string contentType, long contentLength);
}

/// <summary>
/// Compression algorithms
/// </summary>
public enum CompressionAlgorithm
{
    None,
    Gzip,
    Brotli,
    Deflate
}

/// <summary>
/// Compression levels
/// </summary>
public enum CompressionLevel
{
    Fastest,
    Optimal,
    Maximum
}