using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ACS.Alerting;

/// <summary>
/// Routes notifications to appropriate channels with delivery tracking
/// </summary>
public class NotificationRouter : INotificationRouter
{
    private readonly ILogger<NotificationRouter> _logger;
    private readonly IConfiguration _configuration;
    private readonly Dictionary<NotificationChannel, INotificationChannel> _channels;

    public NotificationRouter(
        ILogger<NotificationRouter> logger,
        IConfiguration configuration,
        IEnumerable<INotificationChannel> channels)
    {
        _logger = logger;
        _configuration = configuration;
        _channels = channels.ToDictionary(c => c.ChannelType, c => c);
    }

    public async Task<NotificationDelivery> SendNotificationAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        _logger.LogDebug("Sending notification via {Channel} to {Target}", 
            request.Channel, MaskTarget(request.Target));

        try
        {
            if (!_channels.TryGetValue(request.Channel, out var channel))
            {
                var errorMessage = $"Notification channel {request.Channel} is not available";
                _logger.LogWarning(errorMessage);
                
                return new NotificationDelivery
                {
                    Channel = request.Channel,
                    Target = request.Target,
                    SentAt = DateTime.UtcNow,
                    Success = false,
                    Error = errorMessage,
                    DeliveryTime = stopwatch.Elapsed
                };
            }

            // Check if channel is enabled
            var channelEnabled = _configuration.GetValue<bool>($"Alerting:Channels:{request.Channel}:Enabled", true);
            if (!channelEnabled)
            {
                _logger.LogDebug("Channel {Channel} is disabled", request.Channel);
                
                return new NotificationDelivery
                {
                    Channel = request.Channel,
                    Target = request.Target,
                    SentAt = DateTime.UtcNow,
                    Success = false,
                    Error = "Channel is disabled",
                    DeliveryTime = stopwatch.Elapsed
                };
            }

            // Apply delivery timeout
            var timeout = request.DeliveryTimeout ?? TimeSpan.FromSeconds(30);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            // Send notification with retries
            var maxRetries = Math.Max(1, request.MaxRetries);
            Exception? lastException = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var deliveryResult = await channel.SendAsync(request, cts.Token);
                    
                    stopwatch.Stop();
                    
                    if (deliveryResult.Success)
                    {
                        _logger.LogDebug("Notification sent successfully via {Channel} to {Target} in {ElapsedMs}ms (attempt {Attempt})", 
                            request.Channel, MaskTarget(request.Target), stopwatch.ElapsedMilliseconds, attempt);
                        
                        return deliveryResult;
                    }

                    _logger.LogWarning("Notification delivery failed via {Channel} to {Target} (attempt {Attempt}/{MaxRetries}): {Error}", 
                        request.Channel, MaskTarget(request.Target), attempt, maxRetries, deliveryResult.Error);

                    lastException = new Exception(deliveryResult.Error ?? "Unknown delivery error");

                    // Wait before retry (exponential backoff)
                    if (attempt < maxRetries)
                    {
                        var delayMs = Math.Min(1000 * (int)Math.Pow(2, attempt - 1), 10000);
                        await Task.Delay(delayMs, cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    _logger.LogWarning("Notification delivery timed out via {Channel} to {Target} (attempt {Attempt})", 
                        request.Channel, MaskTarget(request.Target), attempt);
                    
                    lastException = new TimeoutException($"Notification delivery timed out after {timeout.TotalSeconds} seconds");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Notification delivery error via {Channel} to {Target} (attempt {Attempt}/{MaxRetries})", 
                        request.Channel, MaskTarget(request.Target), attempt, maxRetries);
                    
                    lastException = ex;

                    if (attempt < maxRetries)
                    {
                        var delayMs = Math.Min(1000 * (int)Math.Pow(2, attempt - 1), 10000);
                        await Task.Delay(delayMs, cancellationToken);
                    }
                }
            }

            stopwatch.Stop();
            
            return new NotificationDelivery
            {
                Channel = request.Channel,
                Target = request.Target,
                SentAt = DateTime.UtcNow,
                Success = false,
                Error = lastException?.Message ?? "All delivery attempts failed",
                DeliveryTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error sending notification via {Channel} to {Target}", 
                request.Channel, MaskTarget(request.Target));

            return new NotificationDelivery
            {
                Channel = request.Channel,
                Target = request.Target,
                SentAt = DateTime.UtcNow,
                Success = false,
                Error = ex.Message,
                DeliveryTime = stopwatch.Elapsed
            };
        }
    }

    public async Task<bool> TestChannelAsync(NotificationChannel channel, string target, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_channels.TryGetValue(channel, out var channelImpl))
            {
                _logger.LogWarning("Test failed: Channel {Channel} is not available", channel);
                return false;
            }

            var testRequest = new NotificationRequest
            {
                Channel = channel,
                Target = target,
                Subject = "ACS Alert System Test",
                Message = $"This is a test notification from the ACS Alert System sent at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}",
                Priority = NotificationPriority.Low,
                DeliveryTimeout = TimeSpan.FromSeconds(10)
            };

            var result = await channelImpl.SendAsync(testRequest, cancellationToken);
            
            _logger.LogInformation("Channel test for {Channel} to {Target}: {Success}", 
                channel, MaskTarget(target), result.Success ? "PASSED" : "FAILED");

            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Channel test failed for {Channel} to {Target}", channel, MaskTarget(target));
            return false;
        }
    }

    public async Task<IEnumerable<NotificationChannel>> GetAvailableChannelsAsync(CancellationToken cancellationToken = default)
    {
        var availableChannels = new List<NotificationChannel>();

        foreach (var (channelType, channel) in _channels)
        {
            try
            {
                var isEnabled = _configuration.GetValue<bool>($"Alerting:Channels:{channelType}:Enabled", true);
                var isHealthy = await channel.IsHealthyAsync(cancellationToken);
                
                if (isEnabled && isHealthy)
                {
                    availableChannels.Add(channelType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check availability of channel {Channel}", channelType);
            }
        }

        return availableChannels;
    }

    private static string MaskTarget(string target)
    {
        if (string.IsNullOrEmpty(target))
            return "unknown";

        // Mask email addresses
        if (target.Contains("@"))
        {
            var parts = target.Split('@');
            if (parts.Length == 2)
            {
                var localPart = parts[0].Length > 2 ? $"{parts[0][0]}***{parts[0][^1]}" : "***";
                return $"{localPart}@{parts[1]}";
            }
        }

        // Mask phone numbers
        if (target.Length > 4 && (target.All(char.IsDigit) || target.StartsWith("+") || target.Contains("-")))
        {
            return $"***-***-{target[^4..]}";
        }

        // Mask URLs
        if (target.StartsWith("http"))
        {
            try
            {
                var uri = new Uri(target);
                return $"{uri.Scheme}://{uri.Host}/***";
            }
            catch
            {
                return "https://***";
            }
        }

        // For other types, show first and last characters
        if (target.Length > 6)
        {
            return $"{target[..2]}***{target[^2..]}";
        }

        return "***";
    }
}

/// <summary>
/// Interface for individual notification channel implementations
/// </summary>
public interface INotificationChannel
{
    NotificationChannel ChannelType { get; }
    Task<NotificationDelivery> SendAsync(NotificationRequest request, CancellationToken cancellationToken);
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken);
}