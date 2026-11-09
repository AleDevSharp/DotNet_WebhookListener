using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Webhook_ListenerDotNet.Configuration;
using Webhook_ListenerDotNet.Models;

namespace Webhook_ListenerDotNet.Queue;

public sealed class WebhookQueue : IWebhookQueue
{
    private readonly Channel<WebhookEvent> _channel;
    private readonly ILogger<WebhookQueue> _logger;

    public WebhookQueue(
        IOptions<WebhookSettings> settings,
        ILogger<WebhookQueue> logger)
    {
        _logger = logger;
        int capacity = Math.Max(100, settings.Value.QueueCapacity);

        var channelOptions = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };

        _channel = Channel.CreateBounded<WebhookEvent>(channelOptions);
        _logger.LogInformation("Initialized bounded webhook queue with capacity {Capacity}", capacity);
    }

    public ValueTask EnqueueAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(webhookEvent, cancellationToken);
    }

    public ChannelReader<WebhookEvent> Reader => _channel.Reader;

    public int CurrentCount => _channel.Reader.Count;
}

