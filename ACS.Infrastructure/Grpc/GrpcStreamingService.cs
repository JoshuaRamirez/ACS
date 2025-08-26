using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace ACS.Infrastructure.Grpc;

/// <summary>
/// Service for handling gRPC streaming operations
/// </summary>
public interface IGrpcStreamingService
{
    /// <summary>
    /// Server streaming - send multiple responses for a single request
    /// </summary>
    IAsyncEnumerable<T> ServerStreamAsync<T>(
        Func<CancellationToken, IAsyncEnumerable<T>> producer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Client streaming - receive multiple requests and send single response
    /// </summary>
    Task<TResponse> ClientStreamAsync<TRequest, TResponse>(
        IAsyncEnumerable<TRequest> requests,
        Func<IEnumerable<TRequest>, Task<TResponse>> processor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bidirectional streaming - stream both requests and responses
    /// </summary>
    IAsyncEnumerable<TResponse> BidirectionalStreamAsync<TRequest, TResponse>(
        IAsyncEnumerable<TRequest> requests,
        Func<TRequest, CancellationToken, Task<TResponse>> processor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch processing with streaming
    /// </summary>
    IAsyncEnumerable<BatchResult<T>> BatchProcessAsync<T>(
        IEnumerable<T> items,
        Func<T, CancellationToken, Task> processor,
        int batchSize = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of gRPC streaming service
/// </summary>
public class GrpcStreamingService : IGrpcStreamingService
{
    private readonly ILogger<GrpcStreamingService> _logger;
    private readonly StreamingOptions _options;

    public GrpcStreamingService(
        ILogger<GrpcStreamingService> logger,
        StreamingOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new StreamingOptions();
    }

    public async IAsyncEnumerable<T> ServerStreamAsync<T>(
        Func<CancellationToken, IAsyncEnumerable<T>> producer,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting server streaming for type {Type}", typeof(T).Name);
        var itemCount = 0;

        IAsyncEnumerable<T> stream;
        try
        {
            stream = producer(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating producer stream for type {Type}", typeof(T).Name);
            throw;
        }

        await foreach (var item in stream.WithCancellation(cancellationToken))
        {
            itemCount++;
            _logger.LogTrace("Streaming item {Count} of type {Type}", itemCount, typeof(T).Name);
            
            yield return item;

            // Apply rate limiting if configured
            if (_options.RateLimitDelayMs > 0)
            {
                try
                {
                    await Task.Delay(_options.RateLimitDelayMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Server streaming cancelled after {Count} items", itemCount);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during rate limiting delay after {Count} items", itemCount);
                    throw;
                }
            }
        }

        _logger.LogDebug("Server streaming completed. Streamed {Count} items", itemCount);
    }

    public async Task<TResponse> ClientStreamAsync<TRequest, TResponse>(
        IAsyncEnumerable<TRequest> requests,
        Func<IEnumerable<TRequest>, Task<TResponse>> processor,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting client streaming for request type {RequestType}", typeof(TRequest).Name);
        var collectedRequests = new List<TRequest>();

        try
        {
            await foreach (var request in requests.WithCancellation(cancellationToken))
            {
                collectedRequests.Add(request);
                _logger.LogTrace("Received request {Count} of type {Type}", 
                    collectedRequests.Count, typeof(TRequest).Name);

                // Check buffer limit
                if (_options.MaxBufferSize > 0 && collectedRequests.Count >= _options.MaxBufferSize)
                {
                    _logger.LogWarning("Client stream buffer limit reached: {MaxSize}", _options.MaxBufferSize);
                    break;
                }
            }

            _logger.LogDebug("Processing {Count} collected requests", collectedRequests.Count);
            var response = await processor(collectedRequests);
            
            _logger.LogDebug("Client streaming completed successfully");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during client streaming after collecting {Count} requests", 
                collectedRequests.Count);
            throw;
        }
    }

    public async IAsyncEnumerable<TResponse> BidirectionalStreamAsync<TRequest, TResponse>(
        IAsyncEnumerable<TRequest> requests,
        Func<TRequest, CancellationToken, Task<TResponse>> processor,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting bidirectional streaming");
        var requestCount = 0;
        var responseCount = 0;

        // Use Channel for concurrent processing
        var channel = Channel.CreateUnbounded<TResponse>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        // Start processing requests
        var processingTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var request in requests.WithCancellation(cancellationToken))
                {
                    requestCount++;
                    _logger.LogTrace("Processing request {Count}", requestCount);

                    // Process request asynchronously
                    _ = ProcessRequestAsync(request, channel.Writer, processor, cancellationToken);

                    // Apply concurrency limit
                    if (_options.MaxConcurrentProcessing > 0 && 
                        requestCount % _options.MaxConcurrentProcessing == 0)
                    {
                        await Task.Delay(10, cancellationToken); // Small delay to prevent overwhelming
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing requests in bidirectional stream");
                channel.Writer.TryComplete(ex);
            }
            finally
            {
                // Wait a bit for pending operations to complete
                await Task.Delay(100, CancellationToken.None);
                channel.Writer.TryComplete();
            }
        }, cancellationToken);

        // Stream responses
        try
        {
            await foreach (var response in channel.Reader.ReadAllAsync(cancellationToken))
            {
                responseCount++;
                _logger.LogTrace("Streaming response {Count}", responseCount);
                yield return response;
            }
        }
        finally
        {
            await processingTask;
            _logger.LogDebug("Bidirectional streaming completed. Requests: {Requests}, Responses: {Responses}",
                requestCount, responseCount);
        }
    }

    public async IAsyncEnumerable<BatchResult<T>> BatchProcessAsync<T>(
        IEnumerable<T> items,
        Func<T, CancellationToken, Task> processor,
        int batchSize = 10,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
            throw new ArgumentException("Batch size must be positive", nameof(batchSize));

        _logger.LogDebug("Starting batch processing with batch size {BatchSize}", batchSize);
        
        var batches = items.Chunk(batchSize);
        var batchNumber = 0;

        foreach (var batch in batches)
        {
            batchNumber++;
            var batchResult = new BatchResult<T>
            {
                BatchNumber = batchNumber,
                Items = batch.ToList(),
                StartTime = DateTime.UtcNow
            };

            _logger.LogDebug("Processing batch {BatchNumber} with {Count} items", 
                batchNumber, batchResult.Items.Count);

            // Process items in parallel within batch
            var tasks = batchResult.Items.Select(async item =>
            {
                try
                {
                    await processor(item, cancellationToken);
                    return new ProcessingResult<T> { Item = item, Success = true };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing item in batch {BatchNumber}", batchNumber);
                    return new ProcessingResult<T> 
                    { 
                        Item = item, 
                        Success = false, 
                        Error = ex.Message 
                    };
                }
            });

            var results = await Task.WhenAll(tasks);
            
            batchResult.EndTime = DateTime.UtcNow;
            batchResult.SuccessCount = results.Count(r => r.Success);
            batchResult.FailureCount = results.Count(r => !r.Success);
            batchResult.Results = results.ToList();

            _logger.LogDebug("Batch {BatchNumber} completed. Success: {Success}, Failed: {Failed}",
                batchNumber, batchResult.SuccessCount, batchResult.FailureCount);

            yield return batchResult;

            // Apply delay between batches if configured
            if (_options.BatchDelayMs > 0 && batchNumber < items.Count() / batchSize)
            {
                await Task.Delay(_options.BatchDelayMs, cancellationToken);
            }
        }

        _logger.LogDebug("Batch processing completed. Total batches: {BatchCount}", batchNumber);
    }

    private async Task ProcessRequestAsync<TRequest, TResponse>(
        TRequest request,
        ChannelWriter<TResponse> writer,
        Func<TRequest, CancellationToken, Task<TResponse>> processor,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await processor(request, cancellationToken);
            await writer.WriteAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request in bidirectional stream");
            // Don't propagate - let other requests continue
        }
    }
}

/// <summary>
/// Options for streaming operations
/// </summary>
public class StreamingOptions
{
    /// <summary>
    /// Maximum buffer size for client streaming
    /// </summary>
    public int MaxBufferSize { get; set; } = 1000;

    /// <summary>
    /// Rate limit delay in milliseconds for server streaming
    /// </summary>
    public int RateLimitDelayMs { get; set; } = 0;

    /// <summary>
    /// Maximum concurrent processing for bidirectional streaming
    /// </summary>
    public int MaxConcurrentProcessing { get; set; } = 10;

    /// <summary>
    /// Delay between batches in milliseconds
    /// </summary>
    public int BatchDelayMs { get; set; } = 0;
}

/// <summary>
/// Result of batch processing
/// </summary>
public class BatchResult<T>
{
    public int BatchNumber { get; set; }
    public List<T> Items { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<ProcessingResult<T>> Results { get; set; } = new();
    public TimeSpan Duration => EndTime - StartTime;
}

/// <summary>
/// Result of processing a single item
/// </summary>
public class ProcessingResult<T>
{
    public T Item { get; set; } = default!;
    public bool Success { get; set; }
    public string? Error { get; set; }
}