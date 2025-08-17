using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace ACS.Service.Infrastructure;

public class TenantRingBuffer : IDisposable
{
    private readonly Channel<WebRequestCommand> _channel;
    private readonly ILogger<TenantRingBuffer> _logger;

    public TenantRingBuffer(ILogger<TenantRingBuffer> logger)
    {
        _logger = logger;
        
        var options = new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,  // Vertical single processor
            SingleWriter = false, // Multiple API threads
            AllowSynchronousContinuations = false
        };
        
        _channel = Channel.CreateBounded<WebRequestCommand>(options);
        
        _logger.LogInformation("TenantRingBuffer created with capacity {Capacity}", options.Capacity);
    }

    public async ValueTask<bool> TryEnqueueAsync(WebRequestCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            await _channel.Writer.WriteAsync(command, cancellationToken);
            return true;
        }
        catch (InvalidOperationException)
        {
            _logger.LogWarning("Failed to enqueue command - channel is closed");
            return false;
        }
    }

    public IAsyncEnumerable<WebRequestCommand> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    public void CompleteWriter()
    {
        _channel.Writer.Complete();
    }

    public void Dispose()
    {
        CompleteWriter();
    }
}