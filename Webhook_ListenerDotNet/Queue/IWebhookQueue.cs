using System.Threading.Channels;
using Webhook_ListenerDotNet.Models;

namespace Webhook_ListenerDotNet.Queue;

public interface IWebhookQueue
{
    /// <summary>
    /// Enqueues a webhook event asynchronously.
    /// </summary>
    ValueTask EnqueueAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Channel reader stream for consuming enqueued webhook events.
    /// </summary>
    ChannelReader<WebhookEvent> Reader { get; }

    /// <summary>
    /// Current count of items waiting in the queue.
    /// </summary>
    int CurrentCount { get; }
}

