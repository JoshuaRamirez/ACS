using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ACS.Alerting.Channels;

/// <summary>
/// Slack notification channel implementation using webhooks
/// </summary>
public class SlackNotificationChannel : INotificationChannel
{
    private readonly ILogger<SlackNotificationChannel> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public NotificationChannel ChannelType => NotificationChannel.Slack;

    public SlackNotificationChannel(ILogger<SlackNotificationChannel> logger, IConfiguration configuration, HttpClient httpClient)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClient;
    }

    public async Task<NotificationDelivery> SendAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var webhookUrl = GetWebhookUrl(request.Target);
            if (string.IsNullOrEmpty(webhookUrl))
            {
                var errorMessage = $"Slack webhook URL not configured for target: {request.Target}";
                _logger.LogWarning(errorMessage);
                
                return new NotificationDelivery
                {
                    Channel = NotificationChannel.Slack,
                    Target = request.Target,
                    SentAt = DateTime.UtcNow,
                    Success = false,
                    Error = errorMessage,
                    DeliveryTime = stopwatch.Elapsed
                };
            }

            var slackMessage = CreateSlackMessage(request);
            var jsonContent = JsonSerializer.Serialize(slackMessage);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(webhookUrl, httpContent, cancellationToken);
            
            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Slack notification sent successfully to {Target} in {ElapsedMs}ms", 
                    request.Target, stopwatch.ElapsedMilliseconds);

                return new NotificationDelivery
                {
                    Channel = NotificationChannel.Slack,
                    Target = request.Target,
                    SentAt = DateTime.UtcNow,
                    Success = true,
                    DeliveryTime = stopwatch.Elapsed
                };
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var errorMessage2 = $"Slack API error: {response.StatusCode} - {responseContent}";
            
            _logger.LogError("Failed to send Slack notification to {Target}: {Error}", request.Target, errorMessage2);

            return new NotificationDelivery
            {
                Channel = NotificationChannel.Slack,
                Target = request.Target,
                SentAt = DateTime.UtcNow,
                Success = false,
                Error = errorMessage2,
                DeliveryTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex, "Failed to send Slack notification to {Target} in {ElapsedMs}ms", 
                request.Target, stopwatch.ElapsedMilliseconds);

            return new NotificationDelivery
            {
                Channel = NotificationChannel.Slack,
                Target = request.Target,
                SentAt = DateTime.UtcNow,
                Success = false,
                Error = ex.Message,
                DeliveryTime = stopwatch.Elapsed
            };
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Test with a minimal ping message to the default webhook
            var defaultWebhookUrl = _configuration.GetValue<string>("Alerting:Channels:Slack:DefaultWebhookUrl");
            if (string.IsNullOrEmpty(defaultWebhookUrl))
                return false;

            var testMessage = new
            {
                text = "ACS Alert System Health Check",
                username = "ACS-HealthCheck",
                icon_emoji = ":white_check_mark:"
            };

            var jsonContent = JsonSerializer.Serialize(testMessage);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _httpClient.PostAsync(defaultWebhookUrl, httpContent, cts.Token);
            
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private string? GetWebhookUrl(string target)
    {
        // If target looks like a webhook URL, use it directly
        if (target.StartsWith("https://hooks.slack.com/"))
        {
            return target;
        }

        // Otherwise, look up webhook URL by channel name or use default
        var webhookUrl = _configuration.GetValue<string>($"Alerting:Channels:Slack:Webhooks:{target}");
        if (!string.IsNullOrEmpty(webhookUrl))
        {
            return webhookUrl;
        }

        // Fall back to default webhook
        return _configuration.GetValue<string>("Alerting:Channels:Slack:DefaultWebhookUrl");
    }

    private static object CreateSlackMessage(NotificationRequest request)
    {
        var color = request.Priority switch
        {
            NotificationPriority.Emergency => "danger",
            NotificationPriority.High => "warning",
            NotificationPriority.Medium => "#ffcc00",
            NotificationPriority.Low => "good",
            _ => "#cccccc"
        };

        var icon = request.Priority switch
        {
            NotificationPriority.Emergency => ":rotating_light:",
            NotificationPriority.High => ":exclamation:",
            NotificationPriority.Medium => ":warning:",
            NotificationPriority.Low => ":information_source:",
            _ => ":bell:"
        };

        var fields = new List<object>
        {
            new
            {
                title = "Priority",
                value = request.Priority.ToString().ToUpper(),
                @short = true
            },
            new
            {
                title = "Alert ID",
                value = request.Id,
                @short = true
            },
            new
            {
                title = "Timestamp",
                value = request.Timestamp.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                @short = true
            }
        };

        // Add metadata as fields
        foreach (var kvp in request.Metadata.Take(5)) // Limit to 5 to avoid too long messages
        {
            fields.Add(new
            {
                title = kvp.Key,
                value = kvp.Value?.ToString() ?? "null",
                @short = true
            });
        }

        var attachment = new
        {
            color = color,
            title = request.Subject,
            text = request.Message,
            fields = fields.ToArray(),
            ts = ((DateTimeOffset)request.Timestamp).ToUnixTimeSeconds(),
            footer = "ACS Alert System",
            footer_icon = "https://via.placeholder.com/16x16.png?text=ACS"
        };

        return new
        {
            username = "ACS Alerts",
            icon_emoji = icon,
            channel = request.Target.StartsWith("#") ? request.Target : $"#{request.Target}",
            attachments = new[] { attachment }
        };
    }
}