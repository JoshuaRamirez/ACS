using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using ACS.WebApi.DTOs;

namespace ACS.WebApi.Services;

/// <summary>
/// Service for mapping gRPC errors to appropriate HTTP status codes and responses
/// </summary>
public class GrpcErrorMappingService
{
    private readonly ILogger<GrpcErrorMappingService> _logger;

    public GrpcErrorMappingService(ILogger<GrpcErrorMappingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Maps gRPC RpcException to appropriate HTTP status code
    /// </summary>
    public int MapGrpcStatusToHttpStatus(StatusCode grpcStatus)
    {
        return grpcStatus switch
        {
            StatusCode.OK => 200,
            StatusCode.InvalidArgument => 400,
            StatusCode.Unauthenticated => 401,
            StatusCode.PermissionDenied => 403,
            StatusCode.NotFound => 404,
            StatusCode.AlreadyExists => 409,
            StatusCode.ResourceExhausted => 429,
            StatusCode.FailedPrecondition => 412,
            StatusCode.Aborted => 409,
            StatusCode.OutOfRange => 400,
            StatusCode.Unimplemented => 501,
            StatusCode.Internal => 500,
            StatusCode.Unavailable => 503,
            StatusCode.DataLoss => 500,
            StatusCode.Cancelled => 499, // Client closed request
            StatusCode.DeadlineExceeded => 504,
            StatusCode.Unknown => 500,
            _ => 500
        };
    }

    /// <summary>
    /// Maps gRPC RpcException to user-friendly error message
    /// </summary>
    public string MapGrpcStatusToMessage(StatusCode grpcStatus)
    {
        return grpcStatus switch
        {
            StatusCode.OK => "Success",
            StatusCode.InvalidArgument => "Invalid request parameters",
            StatusCode.Unauthenticated => "Authentication required",
            StatusCode.PermissionDenied => "Permission denied",
            StatusCode.NotFound => "Resource not found",
            StatusCode.AlreadyExists => "Resource already exists",
            StatusCode.ResourceExhausted => "Rate limit exceeded",
            StatusCode.FailedPrecondition => "Precondition failed",
            StatusCode.Aborted => "Operation aborted",
            StatusCode.OutOfRange => "Request parameter out of range",
            StatusCode.Unimplemented => "Operation not implemented",
            StatusCode.Internal => "Internal server error",
            StatusCode.Unavailable => "Service temporarily unavailable",
            StatusCode.DataLoss => "Data loss detected",
            StatusCode.Cancelled => "Request cancelled",
            StatusCode.DeadlineExceeded => "Request timeout",
            StatusCode.Unknown => "Unknown error occurred",
            _ => "Unexpected error occurred"
        };
    }

    /// <summary>
    /// Creates an appropriate HTTP ActionResult from an RpcException
    /// </summary>
    public ActionResult<ApiResponse<T>> CreateErrorResponse<T>(RpcException rpcException, string? customMessage = null)
    {
        var httpStatus = MapGrpcStatusToHttpStatus(rpcException.Status.StatusCode);
        var message = customMessage ?? MapGrpcStatusToMessage(rpcException.Status.StatusCode);
        
        var errors = new List<string>();
        if (!string.IsNullOrEmpty(rpcException.Status.Detail))
        {
            errors.Add(rpcException.Status.Detail);
        }

        var apiResponse = new ApiResponse<T>(false, default, message, errors);
        
        _logger.LogWarning(rpcException, "gRPC error mapped to HTTP {HttpStatus}: {Message}", httpStatus, message);

        return new ObjectResult(apiResponse)
        {
            StatusCode = httpStatus
        };
    }

    /// <summary>
    /// Creates an appropriate HTTP ActionResult from an RpcException for non-generic responses
    /// </summary>
    public ActionResult CreateErrorResponse(RpcException rpcException, string? customMessage = null)
    {
        var httpStatus = MapGrpcStatusToHttpStatus(rpcException.Status.StatusCode);
        var message = customMessage ?? MapGrpcStatusToMessage(rpcException.Status.StatusCode);
        
        var errors = new List<string>();
        if (!string.IsNullOrEmpty(rpcException.Status.Detail))
        {
            errors.Add(rpcException.Status.Detail);
        }

        var apiResponse = new ApiResponse<object>(false, null, message, errors);
        
        _logger.LogWarning(rpcException, "gRPC error mapped to HTTP {HttpStatus}: {Message}", httpStatus, message);

        return new ObjectResult(apiResponse)
        {
            StatusCode = httpStatus
        };
    }

    /// <summary>
    /// Creates an HTTP ActionResult for circuit breaker open exceptions
    /// </summary>
    public ActionResult<ApiResponse<T>> CreateCircuitBreakerResponse<T>(CircuitBreakerOpenException exception)
    {
        var apiResponse = new ApiResponse<T>(
            false, 
            default, 
            "Service temporarily unavailable - circuit breaker open",
            new List<string> { exception.Message }
        );

        _logger.LogWarning(exception, "Circuit breaker open response returned");

        return new ObjectResult(apiResponse)
        {
            StatusCode = 503 // Service Unavailable
        };
    }

    /// <summary>
    /// Determines if a gRPC error should be retried
    /// </summary>
    public bool IsRetryableError(StatusCode grpcStatus)
    {
        return grpcStatus switch
        {
            StatusCode.Unavailable => true,
            StatusCode.Internal => true,
            StatusCode.Unknown => true,
            StatusCode.DeadlineExceeded => true,
            StatusCode.ResourceExhausted => true,
            StatusCode.Aborted => true,
            _ => false
        };
    }

    /// <summary>
    /// Determines if a gRPC error indicates a client error (non-retryable)
    /// </summary>
    public bool IsClientError(StatusCode grpcStatus)
    {
        return grpcStatus switch
        {
            StatusCode.InvalidArgument => true,
            StatusCode.NotFound => true,
            StatusCode.AlreadyExists => true,
            StatusCode.PermissionDenied => true,
            StatusCode.Unauthenticated => true,
            StatusCode.FailedPrecondition => true,
            StatusCode.OutOfRange => true,
            StatusCode.Unimplemented => true,
            _ => false
        };
    }

    /// <summary>
    /// Gets retry delay based on gRPC status and attempt number
    /// </summary>
    public TimeSpan GetRetryDelay(StatusCode grpcStatus, int attemptNumber)
    {
        var baseDelay = grpcStatus switch
        {
            StatusCode.ResourceExhausted => TimeSpan.FromSeconds(2), // Rate limiting - longer delay
            StatusCode.Unavailable => TimeSpan.FromMilliseconds(100), // Service unavailable - shorter delay
            StatusCode.DeadlineExceeded => TimeSpan.FromMilliseconds(200), // Timeout - medium delay
            _ => TimeSpan.FromMilliseconds(100)
        };

        // Exponential backoff with jitter
        var exponentialDelay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attemptNumber - 1));
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, (int)(exponentialDelay.TotalMilliseconds * 0.1)));
        
        return exponentialDelay.Add(jitter);
    }
}

/// <summary>
/// Extension methods for controller integration
/// </summary>
public static class GrpcErrorMappingExtensions
{
    /// <summary>
    /// Handles gRPC exceptions and returns appropriate HTTP responses
    /// </summary>
    public static ActionResult<ApiResponse<T>> HandleGrpcException<T>(
        this ControllerBase controller, 
        Exception exception, 
        GrpcErrorMappingService errorMapper,
        string? customMessage = null)
    {
        return exception switch
        {
            RpcException rpcEx => errorMapper.CreateErrorResponse<T>(rpcEx, customMessage),
            CircuitBreakerOpenException cbEx => errorMapper.CreateCircuitBreakerResponse<T>(cbEx),
            TimeoutException timeoutEx => new ObjectResult(new ApiResponse<T>(false, default, "Request timeout", new List<string> { timeoutEx.Message }))
            {
                StatusCode = 504
            },
            _ => new ObjectResult(new ApiResponse<T>(false, default, "Internal server error", new List<string> { exception.Message }))
            {
                StatusCode = 500
            }
        };
    }
}