using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;

namespace ACS.Alerting.Channels;

/// <summary>
/// Email notification channel implementation
/// </summary>
public class EmailNotificationChannel : INotificationChannel
{
    private readonly ILogger<EmailNotificationChannel> _logger;
    private readonly IConfiguration _configuration;
    private readonly SmtpClient _smtpClient;

    public NotificationChannel ChannelType => NotificationChannel.Email;

    public EmailNotificationChannel(ILogger<EmailNotificationChannel> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _smtpClient = CreateSmtpClient();
    }

    public async Task<NotificationDelivery> SendAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var fromEmail = _configuration.GetValue<string>("Alerting:Channels:Email:FromAddress") ?? "alerts@company.com";
            var fromName = _configuration.GetValue<string>("Alerting:Channels:Email:FromName") ?? "ACS Alert System";

            using var message = new MailMessage();
            message.From = new MailAddress(fromEmail, fromName);
            message.To.Add(new MailAddress(request.Target));
            message.Subject = request.Subject;
            message.Body = FormatEmailBody(request);
            message.IsBodyHtml = true;
            message.Priority = MapPriorityToMailPriority(request.Priority);

            // Add custom headers
            message.Headers.Add("X-Alert-ID", request.Id);
            message.Headers.Add("X-Alert-Priority", request.Priority.ToString());
            message.Headers.Add("X-Alert-Channel", "Email");

            await _smtpClient.SendMailAsync(message, cancellationToken);
            
            stopwatch.Stop();
            
            _logger.LogDebug("Email notification sent successfully to {Target} in {ElapsedMs}ms", 
                request.Target, stopwatch.ElapsedMilliseconds);

            return new NotificationDelivery
            {
                Channel = NotificationChannel.Email,
                Target = request.Target,
                SentAt = DateTime.UtcNow,
                Success = true,
                DeliveryTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex, "Failed to send email notification to {Target} in {ElapsedMs}ms", 
                request.Target, stopwatch.ElapsedMilliseconds);

            return new NotificationDelivery
            {
                Channel = NotificationChannel.Email,
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
            // Test SMTP connection
            await _smtpClient.SendMailAsync(new MailMessage(), cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private SmtpClient CreateSmtpClient()
    {
        var host = _configuration.GetValue<string>("Alerting:Channels:Email:SmtpHost") ?? "localhost";
        var port = _configuration.GetValue<int>("Alerting:Channels:Email:SmtpPort", 587);
        var enableSsl = _configuration.GetValue<bool>("Alerting:Channels:Email:EnableSsl", true);
        var username = _configuration.GetValue<string>("Alerting:Channels:Email:Username");
        var password = _configuration.GetValue<string>("Alerting:Channels:Email:Password");

        var smtpClient = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            smtpClient.Credentials = new NetworkCredential(username, password);
        }

        return smtpClient;
    }

    private static string FormatEmailBody(NotificationRequest request)
    {
        var priorityColor = request.Priority switch
        {
            NotificationPriority.Emergency => "#ff0000",
            NotificationPriority.High => "#ff6600",
            NotificationPriority.Medium => "#ffcc00",
            NotificationPriority.Low => "#0066cc",
            _ => "#666666"
        };

        var metadataHtml = request.Metadata.Any() ? 
            $@"<div class=""metadata"">
                <strong>Additional Information:</strong><br>
                {string.Join("<br>", request.Metadata.Select(kvp => $"<strong>{System.Net.WebUtility.HtmlEncode(kvp.Key)}:</strong> {System.Net.WebUtility.HtmlEncode(kvp.Value?.ToString() ?? "")}"))}
            </div>" : "";

        var body = $$"""
            <html>
            <head>
                <style>
                    body { font-family: Arial, sans-serif; line-height: 1.6; margin: 0; padding: 20px; }
                    .header { background-color: {{priorityColor}}; color: white; padding: 15px; border-radius: 5px 5px 0 0; }
                    .content { background-color: #f9f9f9; padding: 20px; border-radius: 0 0 5px 5px; border: 1px solid #ddd; }
                    .priority { font-weight: bold; color: {{priorityColor}}; }
                    .metadata { background-color: #e9e9e9; padding: 10px; margin-top: 15px; border-radius: 3px; }
                    .timestamp { color: #666; font-size: 0.9em; }
                    pre { background-color: #f0f0f0; padding: 10px; border-radius: 3px; overflow-x: auto; }
                </style>
            </head>
            <body>
                <div class="header">
                    <h2>🚨 ACS Alert Notification</h2>
                </div>
                <div class="content">
                    <p><strong>Priority:</strong> <span class="priority">{{request.Priority.ToString().ToUpper()}}</span></p>
                    
                    <div style="white-space: pre-wrap; margin: 15px 0;">{{System.Net.WebUtility.HtmlEncode(request.Message)}}</div>
                    
                    {{metadataHtml}}
                    
                    <p class="timestamp">
                        <strong>Alert ID:</strong> {{request.Id}}<br>
                        <strong>Sent:</strong> {{request.Timestamp:yyyy-MM-dd HH:mm:ss UTC}}
                    </p>
                </div>
            </body>
            </html>
            """;

        return body;
    }

    private static MailPriority MapPriorityToMailPriority(NotificationPriority priority)
    {
        return priority switch
        {
            NotificationPriority.Emergency => MailPriority.High,
            NotificationPriority.High => MailPriority.High,
            NotificationPriority.Medium => MailPriority.Normal,
            NotificationPriority.Low => MailPriority.Low,
            _ => MailPriority.Normal
        };
    }

    public void Dispose()
    {
        _smtpClient?.Dispose();
    }
}