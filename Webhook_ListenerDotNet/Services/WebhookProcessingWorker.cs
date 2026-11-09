using Microsoft.Extensions.Options;
using Webhook_ListenerDotNet.Configuration;
using Webhook_ListenerDotNet.Queue;

namespace Webhook_ListenerDotNet.Services;

/// <summary>
/// Background worker hosted service that consumes webhook events from the asynchronous channel queue.
/// </summary>
public sealed class WebhookProcessingWorker : BackgroundService
{
    private readonly IWebhookQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<WebhookSettings> _settings;
    private readonly ILogger<WebhookProcessingWorker> _logger;

    public WebhookProcessingWorker(
        IWebhookQueue queue,
        IServiceProvider serviceProvider,
        IOptions<WebhookSettings> settings,
        ILogger<WebhookProcessingWorker> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Value.EnableBackgroundProcessing)
        {
            _logger.LogInformation("Background webhook processing is disabled in configuration.");
            return;
        }

        _logger.LogInformation("Webhook background processing worker started.");

        try
        {
            await foreach (var webhookEvent in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var processor = scope.ServiceProvider.GetRequiredService<IWebhookProcessor>();
                    await processor.ProcessAsync(webhookEvent, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error processing webhook event {EventId}", webhookEvent.Id);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown requested
        }

        _logger.LogInformation("Webhook background processing worker stopped.");
    }
}

