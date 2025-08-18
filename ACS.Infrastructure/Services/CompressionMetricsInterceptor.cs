using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ACS.Infrastructure.Services;

/// <summary>
/// gRPC interceptor for tracking compression metrics
/// </summary>
public class CompressionMetricsInterceptor : Interceptor
{
    private readonly ILogger<CompressionMetricsInterceptor> _logger;
    private readonly ActivitySource _activitySource = new("ACS.Grpc.Compression");
    private readonly CompressionMetrics _metrics = new();
    
    public CompressionMetricsInterceptor(ILogger<CompressionMetricsInterceptor> logger)
    {
        _logger = logger;
    }
    
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        using var activity = _activitySource.StartActivity("grpc.compression.client");
        activity?.SetTag("rpc.method", context.Method.FullName);
        
        // Estimate request size (serialized)
        var requestSize = EstimateMessageSize(request);
        activity?.SetTag("request.uncompressed_size", requestSize);
        
        // Add compression headers
        var headers = context.Options.Headers ?? new Metadata();
        headers.Add("grpc-encoding", "gzip");
        headers.Add("grpc-accept-encoding", "gzip,br");
        
        var options = context.Options.WithHeaders(headers);
        var newContext = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method,
            context.Host,
            options);
        
        var response = continuation(request, newContext);
        
        // Track response
        var responseCall = new AsyncUnaryCall<TResponse>(
            HandleResponseAsync(response.ResponseAsync, activity, requestSize),
            response.ResponseHeadersAsync,
            response.GetStatus,
            response.GetTrailers,
            response.Dispose);
        
        return responseCall;
    }
    
    private async Task<TResponse> HandleResponseAsync<TResponse>(
        Task<TResponse> responseTask,
        Activity? activity,
        long requestSize)
    {
        try
        {
            var response = await responseTask;
            var responseSize = EstimateMessageSize(response);
            
            activity?.SetTag("response.uncompressed_size", responseSize);
            
            // Calculate compression ratio
            var totalUncompressed = requestSize + responseSize;
            activity?.SetTag("compression.total_uncompressed", totalUncompressed);
            
            // Update metrics
            _metrics.RecordRequest(requestSize, responseSize);
            
            _logger.LogTrace("gRPC call completed - Request: {RequestSize} bytes, Response: {ResponseSize} bytes",
                requestSize, responseSize);
            
            return response;
        }
        catch (RpcException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
    
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        using var activity = _activitySource.StartActivity("grpc.compression.server");
        activity?.SetTag("rpc.method", context.Method);
        
        // Check compression headers
        var encoding = context.RequestHeaders.GetValue("grpc-encoding");
        var acceptEncoding = context.RequestHeaders.GetValue("grpc-accept-encoding");
        
        activity?.SetTag("request.encoding", encoding ?? "none");
        activity?.SetTag("request.accept_encoding", acceptEncoding ?? "none");
        
        var requestSize = EstimateMessageSize(request);
        activity?.SetTag("request.uncompressed_size", requestSize);
        
        try
        {
            var response = await continuation(request, context);
            
            var responseSize = EstimateMessageSize(response);
            activity?.SetTag("response.uncompressed_size", responseSize);
            
            // Set response compression
            if (!string.IsNullOrEmpty(acceptEncoding))
            {
                // Use the first supported encoding
                var supportedEncodings = acceptEncoding.Split(',').Select(e => e.Trim());
                var selectedEncoding = supportedEncodings.FirstOrDefault(e => e == "gzip" || e == "br") ?? "gzip";
                
                context.ResponseTrailers.Add("grpc-encoding", selectedEncoding);
                activity?.SetTag("response.encoding", selectedEncoding);
            }
            
            _metrics.RecordRequest(requestSize, responseSize);
            
            return response;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
    
    /// <summary>
    /// Estimate message size for metrics (simplified)
    /// </summary>
    private long EstimateMessageSize<T>(T message)
    {
        if (message == null) return 0;
        
        // This is a simplified estimation
        // In production, you might want to actually serialize to get accurate size
        var json = System.Text.Json.JsonSerializer.Serialize(message);
        return System.Text.Encoding.UTF8.GetByteCount(json);
    }
    
    /// <summary>
    /// Get compression metrics
    /// </summary>
    public CompressionMetrics GetMetrics() => _metrics;
}

/// <summary>
/// Compression metrics tracking
/// </summary>
public class CompressionMetrics
{
    private long _totalRequests;
    private long _totalRequestBytes;
    private long _totalResponseBytes;
    private readonly object _lock = new();
    
    public long TotalRequests => _totalRequests;
    public long TotalRequestBytes => _totalRequestBytes;
    public long TotalResponseBytes => _totalResponseBytes;
    public long TotalBytes => _totalRequestBytes + _totalResponseBytes;
    
    public double AverageRequestSize => _totalRequests > 0 ? (double)_totalRequestBytes / _totalRequests : 0;
    public double AverageResponseSize => _totalRequests > 0 ? (double)_totalResponseBytes / _totalRequests : 0;
    
    public void RecordRequest(long requestBytes, long responseBytes)
    {
        lock (_lock)
        {
            _totalRequests++;
            _totalRequestBytes += requestBytes;
            _totalResponseBytes += responseBytes;
        }
    }
    
    public void Reset()
    {
        lock (_lock)
        {
            _totalRequests = 0;
            _totalRequestBytes = 0;
            _totalResponseBytes = 0;
        }
    }
}