using Webhook_ListenerDotNet.Models;

namespace Webhook_ListenerDotNet.Services;

public interface IWebhookProcessor
{
    /// <summary>
    /// Processes an incoming or dequeued webhook event.
    /// </summary>
    /// <param name="webhookEvent">The normalized webhook event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ProcessAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default);
}

