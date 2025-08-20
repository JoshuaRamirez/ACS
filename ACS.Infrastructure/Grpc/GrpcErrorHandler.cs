using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace ACS.Infrastructure.Grpc;

/// <summary>
/// Enhanced error handling for gRPC services
/// </summary>
public interface IGrpcErrorHandler
{
    /// <summary>
    /// Handle exception and convert to gRPC status
    /// </summary>
    Status HandleException(Exception exception, string? correlationId = null);

    /// <summary>
    /// Create RpcException with detailed error information
    /// </summary>
    RpcException CreateRpcException(Exception exception, string? correlationId = null);

    /// <summary>
    /// Extract error details from RpcException
    /// </summary>
    ErrorDetails? ExtractErrorDetails(RpcException rpcException);

    /// <summary>
    /// Wrap async operation with error handling
    /// </summary>
    Task<T> WrapAsync<T>(Func<Task<T>> operation, string? correlationId = null);

    /// <summary>
    /// Wrap streaming operation with error handling
    /// </summary>
    IAsyncEnumerable<T> WrapStreamAsync<T>(
        Func<CancellationToken, IAsyncEnumerable<T>> operation,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of gRPC error handler
/// </summary>
public class GrpcErrorHandler : IGrpcErrorHandler
{
    private readonly ILogger<GrpcErrorHandler> _logger;
    private readonly GrpcErrorOptions _options;

    public GrpcErrorHandler(
        ILogger<GrpcErrorHandler> logger,
        GrpcErrorOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new GrpcErrorOptions();
    }

    public Status HandleException(Exception exception, string? correlationId = null)
    {
        // Log the exception
        _logger.LogError(exception, "gRPC error occurred. CorrelationId: {CorrelationId}", correlationId);

        // Map exception to gRPC status
        var (statusCode, message, details) = MapExceptionToStatus(exception);

        // Create metadata with error details
        var metadata = CreateErrorMetadata(exception, correlationId, details);

        // Create status with metadata
        return new Status(statusCode, message);
    }

    public RpcException CreateRpcException(Exception exception, string? correlationId = null)
    {
        var status = HandleException(exception, correlationId);
        var metadata = CreateErrorMetadata(exception, correlationId);

        return new RpcException(status, metadata);
    }

    public ErrorDetails? ExtractErrorDetails(RpcException rpcException)
    {
        try
        {
            var metadata = rpcException.Trailers;
            if (metadata == null)
                return null;

            var errorDetails = new ErrorDetails
            {
                StatusCode = rpcException.Status.StatusCode,
                Message = rpcException.Status.Detail,
                CorrelationId = metadata.GetValue("correlation-id"),
                ErrorCode = metadata.GetValue("error-code"),
                ErrorType = metadata.GetValue("error-type"),
                Timestamp = metadata.GetValue("timestamp"),
                StackTrace = _options.IncludeStackTrace ? metadata.GetValue("stack-trace") : null
            };

            // Try to deserialize additional details
            var additionalDetailsJson = metadata.GetValue("additional-details");
            if (!string.IsNullOrEmpty(additionalDetailsJson))
            {
                try
                {
                    errorDetails.AdditionalDetails = JsonSerializer.Deserialize<Dictionary<string, object>>(additionalDetailsJson);
                }
                catch
                {
                    // Ignore deserialization errors
                }
            }

            return errorDetails;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract error details from RpcException");
            return null;
        }
    }

    public async Task<T> WrapAsync<T>(Func<Task<T>> operation, string? correlationId = null)
    {
        try
        {
            return await operation();
        }
        catch (RpcException)
        {
            // Re-throw RpcExceptions as-is
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation("Operation cancelled. CorrelationId: {CorrelationId}", correlationId);
            throw CreateRpcException(ex, correlationId);
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Operation timed out. CorrelationId: {CorrelationId}", correlationId);
            throw CreateRpcException(ex, correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in gRPC operation. CorrelationId: {CorrelationId}", correlationId);
            throw CreateRpcException(ex, correlationId);
        }
    }

    public async IAsyncEnumerable<T> WrapStreamAsync<T>(
        Func<CancellationToken, IAsyncEnumerable<T>> operation,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString();
        
        IAsyncEnumerator<T>? enumerator = null;
        try
        {
            var stream = operation(cancellationToken);
            enumerator = stream.GetAsyncEnumerator(cancellationToken);

            while (await enumerator.MoveNextAsync())
            {
                yield return enumerator.Current;
            }
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in streaming operation. CorrelationId: {CorrelationId}", correlationId);
            throw CreateRpcException(ex, correlationId);
        }
        finally
        {
            if (enumerator != null)
            {
                await enumerator.DisposeAsync();
            }
        }
    }

    private (StatusCode statusCode, string message, Dictionary<string, string> details) MapExceptionToStatus(Exception exception)
    {
        var details = new Dictionary<string, string>();

        switch (exception)
        {
            case ArgumentNullException argNullEx:
                details["parameter"] = argNullEx.ParamName ?? "unknown";
                return (StatusCode.InvalidArgument, $"Required parameter is null: {argNullEx.ParamName}", details);

            case ArgumentException argEx:
                details["parameter"] = argEx.ParamName ?? "unknown";
                return (StatusCode.InvalidArgument, $"Invalid argument: {argEx.Message}", details);

            case UnauthorizedAccessException:
                return (StatusCode.Unauthenticated, "Authentication required", details);

            case InvalidOperationException invOpEx when invOpEx.Message.Contains("permission", StringComparison.OrdinalIgnoreCase):
                return (StatusCode.PermissionDenied, invOpEx.Message, details);

            case InvalidOperationException invOpEx:
                return (StatusCode.FailedPrecondition, invOpEx.Message, details);

            case NotImplementedException:
                return (StatusCode.Unimplemented, "Operation not implemented", details);

            case KeyNotFoundException keyNotFoundEx:
                details["key"] = keyNotFoundEx.Message;
                return (StatusCode.NotFound, "Resource not found", details);

            case FileNotFoundException fileNotFoundEx:
                details["file"] = fileNotFoundEx.FileName ?? "unknown";
                return (StatusCode.NotFound, $"File not found: {fileNotFoundEx.FileName}", details);

            case TimeoutException:
                return (StatusCode.DeadlineExceeded, "Operation timed out", details);

            case OperationCanceledException:
                return (StatusCode.Cancelled, "Operation was cancelled", details);

            case OutOfMemoryException:
                return (StatusCode.ResourceExhausted, "Out of memory", details);

            case NotSupportedException:
                return (StatusCode.Unimplemented, "Operation not supported", details);

            case AggregateException aggEx:
                var innerEx = aggEx.Flatten().InnerExceptions.FirstOrDefault();
                if (innerEx != null)
                {
                    return MapExceptionToStatus(innerEx);
                }
                return (StatusCode.Internal, "Multiple errors occurred", details);

            default:
                // Check for domain-specific exceptions
                if (exception.GetType().Name.Contains("Validation"))
                {
                    return (StatusCode.InvalidArgument, exception.Message, details);
                }
                if (exception.GetType().Name.Contains("NotFound"))
                {
                    return (StatusCode.NotFound, exception.Message, details);
                }
                if (exception.GetType().Name.Contains("Conflict"))
                {
                    return (StatusCode.AlreadyExists, exception.Message, details);
                }
                if (exception.GetType().Name.Contains("Unauthorized") || 
                    exception.GetType().Name.Contains("Forbidden"))
                {
                    return (StatusCode.PermissionDenied, exception.Message, details);
                }

                // Default to internal error
                return (StatusCode.Internal, 
                    _options.IncludeExceptionDetails ? exception.Message : "Internal server error", 
                    details);
        }
    }

    private Metadata CreateErrorMetadata(Exception exception, string? correlationId, Dictionary<string, string>? additionalDetails = null)
    {
        var metadata = new Metadata();

        // Add correlation ID
        if (!string.IsNullOrEmpty(correlationId))
        {
            metadata.Add("correlation-id", correlationId);
        }

        // Add timestamp
        metadata.Add("timestamp", DateTime.UtcNow.ToString("O"));

        // Add error code
        metadata.Add("error-code", GenerateErrorCode(exception));

        // Add error type
        metadata.Add("error-type", exception.GetType().Name);

        // Add stack trace if configured
        if (_options.IncludeStackTrace && !string.IsNullOrEmpty(exception.StackTrace))
        {
            // Truncate stack trace if too long
            var stackTrace = exception.StackTrace;
            if (stackTrace.Length > _options.MaxStackTraceLength)
            {
                stackTrace = stackTrace.Substring(0, _options.MaxStackTraceLength) + "...";
            }
            metadata.Add("stack-trace", stackTrace);
        }

        // Add inner exception details
        if (_options.IncludeInnerException && exception.InnerException != null)
        {
            metadata.Add("inner-exception-type", exception.InnerException.GetType().Name);
            metadata.Add("inner-exception-message", exception.InnerException.Message);
        }

        // Add additional details
        if (additionalDetails != null && additionalDetails.Any())
        {
            try
            {
                var json = JsonSerializer.Serialize(additionalDetails);
                metadata.Add("additional-details", json);
            }
            catch
            {
                // Ignore serialization errors
            }
        }

        // Add custom error properties if available
        foreach (var property in exception.Data.Keys)
        {
            if (property is string key && exception.Data[key] is string value)
            {
                metadata.Add($"error-data-{key}", value);
            }
        }

        return metadata;
    }

    private string GenerateErrorCode(Exception exception)
    {
        // Generate a unique error code based on exception type and hash
        var typeName = exception.GetType().Name;
        var hash = Math.Abs(exception.GetHashCode()) % 10000;
        return $"{typeName.ToUpper()}_{hash:D4}";
    }
}

/// <summary>
/// Options for gRPC error handling
/// </summary>
public class GrpcErrorOptions
{
    /// <summary>
    /// Include exception details in error messages
    /// </summary>
    public bool IncludeExceptionDetails { get; set; } = true;

    /// <summary>
    /// Include stack trace in error metadata
    /// </summary>
    public bool IncludeStackTrace { get; set; } = false;

    /// <summary>
    /// Include inner exception details
    /// </summary>
    public bool IncludeInnerException { get; set; } = true;

    /// <summary>
    /// Maximum stack trace length
    /// </summary>
    public int MaxStackTraceLength { get; set; } = 2000;
}

/// <summary>
/// Detailed error information extracted from RpcException
/// </summary>
public class ErrorDetails
{
    public StatusCode StatusCode { get; set; }
    public string? Message { get; set; }
    public string? CorrelationId { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorType { get; set; }
    public string? Timestamp { get; set; }
    public string? StackTrace { get; set; }
    public Dictionary<string, object>? AdditionalDetails { get; set; }
}

/// <summary>
/// gRPC interceptor for automatic error handling
/// </summary>
public class ErrorHandlingInterceptor : Interceptor
{
    private readonly IGrpcErrorHandler _errorHandler;
    private readonly ILogger<ErrorHandlingInterceptor> _logger;

    public ErrorHandlingInterceptor(
        IGrpcErrorHandler errorHandler,
        ILogger<ErrorHandlingInterceptor> logger)
    {
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var correlationId = ExtractCorrelationId(context);
        
        try
        {
            return await continuation(request, context);
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw _errorHandler.CreateRpcException(ex, correlationId);
        }
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        var correlationId = ExtractCorrelationId(context);
        
        try
        {
            return await continuation(requestStream, context);
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw _errorHandler.CreateRpcException(ex, correlationId);
        }
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        var correlationId = ExtractCorrelationId(context);
        
        try
        {
            await continuation(request, responseStream, context);
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw _errorHandler.CreateRpcException(ex, correlationId);
        }
    }

    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        var correlationId = ExtractCorrelationId(context);
        
        try
        {
            await continuation(requestStream, responseStream, context);
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw _errorHandler.CreateRpcException(ex, correlationId);
        }
    }

    private string ExtractCorrelationId(ServerCallContext context)
    {
        var correlationId = context.RequestHeaders?.GetValue("correlation-id");
        if (string.IsNullOrEmpty(correlationId))
        {
            correlationId = context.RequestHeaders?.GetValue("x-correlation-id");
        }
        if (string.IsNullOrEmpty(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }
        return correlationId;
    }
}