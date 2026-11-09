using System.Text.Json;
using Webhook_ListenerDotNet.Models;

namespace Webhook_ListenerDotNet.Services;

public sealed class WebhookProcessor : IWebhookProcessor
{
    private readonly ILogger<WebhookProcessor> _logger;
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };

    public WebhookProcessor(ILogger<WebhookProcessor> logger)
    {
        _logger = logger;
    }

    public Task ProcessAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default)
    {
        // Extract pretty printed JSON safely for diagnostic logging
        string prettyPayload = JsonSerializer.Serialize(webhookEvent.Payload, IndentedJsonOptions);

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["EventId"] = webhookEvent.Id,
            ["EventType"] = webhookEvent.EventType ?? "Unknown",
            ["SourceIp"] = webhookEvent.SourceIp
        }))
        {
            _logger.LogInformation("Processing webhook event {EventId} (Type: {EventType}) received from {SourceIp}",
                webhookEvent.Id,
                webhookEvent.EventType ?? "N/A",
                webhookEvent.SourceIp);

            _logger.LogInformation("Webhook Payload Content:\n{Payload}", prettyPayload);

            // Note: Application-specific domain logic, database persistence,
            // or notification dispatch can be injected and invoked here.
        }

        return Task.CompletedTask;
    }
}

