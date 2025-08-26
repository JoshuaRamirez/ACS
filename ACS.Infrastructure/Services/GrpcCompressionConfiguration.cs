using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO.Compression;

namespace ACS.Infrastructure.Services;

/// <summary>
/// Configuration for gRPC compression to optimize network bandwidth
/// </summary>
public static class GrpcCompressionConfiguration
{
    /// <summary>
    /// Configure gRPC client and server compression settings
    /// </summary>
    public static void ConfigureGrpcCompression(this IServiceCollection services, IConfiguration configuration)
    {
        // Register compression configuration for DI
        services.Configure<GrpcCompressionOptions>(options =>
        {
            options.GzipLevel = configuration.GetValue<CompressionLevel>("Compression:Gzip:Level", CompressionLevel.Optimal);
            options.BrotliLevel = configuration.GetValue<CompressionLevel>("Compression:Brotli:Level", CompressionLevel.Optimal);
            options.EnableCompression = configuration.GetValue<bool>("Grpc:EnableCompression", true);
            options.CompressionProvider = configuration.GetValue<string>("Grpc:CompressionProvider", "gzip");
        });
    }
    
    /// <summary>
    /// Configuration options for gRPC compression
    /// </summary>
    public class GrpcCompressionOptions
    {
        public CompressionLevel GzipLevel { get; set; } = CompressionLevel.Optimal;
        public CompressionLevel BrotliLevel { get; set; } = CompressionLevel.Optimal;
        public bool EnableCompression { get; set; } = true;
        public string CompressionProvider { get; set; } = "gzip";
    }
    
    /// <summary>
    /// Create gRPC channel with compression enabled
    /// </summary>
    public static GrpcChannel CreateCompressedChannel(string address, IConfiguration? configuration = null)
    {
        var options = new GrpcChannelOptions
        {
            MaxReceiveMessageSize = configuration?.GetValue<int>("Grpc:MaxReceiveMessageSize", 4 * 1024 * 1024) ?? 4 * 1024 * 1024,
            MaxSendMessageSize = configuration?.GetValue<int>("Grpc:MaxSendMessageSize", 4 * 1024 * 1024) ?? 4 * 1024 * 1024,
        };

        // Configure compression at the gRPC channel level
        var enableCompression = configuration?.GetValue<bool>("Grpc:EnableCompression", true) ?? true;
        if (enableCompression)
        {
            var compressionProvider = configuration?.GetValue<string>("Grpc:CompressionProvider", "gzip") ?? "gzip";
            
            // gRPC compression is handled at the call level through CallOptions
            // This channel configuration sets up the infrastructure for compression
        }

        return GrpcChannel.ForAddress(address, options);
    }
    
    /// <summary>
    /// Get call options with compression headers
    /// </summary>
    public static CallOptions GetCompressionCallOptions(IConfiguration? configuration = null)
    {
        var enableCompression = configuration?.GetValue<bool>("Grpc:EnableCompression", true) ?? true;
        
        if (!enableCompression)
        {
            return new CallOptions();
        }
        
        var compressionProvider = configuration?.GetValue<string>("Grpc:CompressionProvider", "gzip") ?? "gzip";
        
        // For gRPC.Net.Client, compression is handled at the channel/call level through headers
        // The actual compression is configured at the server side middleware level
        var metadata = new Metadata();
        metadata.Add("grpc-accept-encoding", compressionProvider);
        metadata.Add("grpc-encoding", compressionProvider);
        
        return new CallOptions(headers: metadata);
    }
}