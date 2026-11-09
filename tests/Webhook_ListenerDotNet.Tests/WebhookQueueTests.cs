using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Webhook_ListenerDotNet.Configuration;
using Webhook_ListenerDotNet.Models;
using Webhook_ListenerDotNet.Queue;
using Xunit;

namespace Webhook_ListenerDotNet.Tests;

public class WebhookQueueTests
{
    [Fact]
    public async Task EnqueueAndRead_ShouldReturnMatchingEvent()
    {
        var settings = Options.Create(new WebhookSettings { QueueCapacity = 50 });
        var queue = new WebhookQueue(settings, NullLogger<WebhookQueue>.Instance);

        using var jsonDoc = JsonDocument.Parse("{\"event\":\"test\",\"data\":123}");
        var webhookEvent = new WebhookEvent(
            Id: "evt_12345",
            EventType: "test",
            ReceivedAtUtc: DateTimeOffset.UtcNow,
            SourceIp: "127.0.0.1",
            Payload: jsonDoc.RootElement.Clone(),
            Headers: new Dictionary<string, string>());

        await queue.EnqueueAsync(webhookEvent);

        Assert.Equal(1, queue.CurrentCount);

        var dequeued = await queue.Reader.ReadAsync();

        Assert.Equal("evt_12345", dequeued.Id);
        Assert.Equal("test", dequeued.EventType);
        Assert.Equal(0, queue.CurrentCount);
    }
}

