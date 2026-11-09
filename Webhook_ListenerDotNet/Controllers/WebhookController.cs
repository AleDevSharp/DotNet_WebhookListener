using System.IO.Pipelines;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Webhook_ListenerDotNet.Configuration;
using Webhook_ListenerDotNet.Models;
using Webhook_ListenerDotNet.Queue;
using Webhook_ListenerDotNet.Security;
using Webhook_ListenerDotNet.Services;

namespace Webhook_ListenerDotNet.Controllers;

/// <summary>
/// Webhook reception controller providing high-performance, secure and resilient endpoints.
/// </summary>
[ApiController]
[Route("api/webhook")]
[Produces("application/json")]
public sealed class WebhookController : ControllerBase
{
    private readonly IWebhookProcessor _processor;
    private readonly IWebhookQueue _queue;
    private readonly IWebhookSecurityValidator _securityValidator;
    private readonly WebhookSettings _settings;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        IWebhookProcessor processor,
        IWebhookQueue queue,
        IWebhookSecurityValidator securityValidator,
        IOptions<WebhookSettings> settings,
        ILogger<WebhookController> logger)
    {
        _processor = processor;
        _queue = queue;
        _securityValidator = securityValidator;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Receives an incoming webhook event in JSON format.
    /// </summary>
    /// <param name="payload">The arbitrary JSON payload of the webhook event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The webhook was received and processed synchronously.</response>
    /// <response code="202">The webhook was accepted and enqueued for asynchronous background processing.</response>
    /// <response code="400">The request body contains invalid or malformed JSON.</response>
    /// <response code="401">Security signature or secret token validation failed.</response>
    /// <response code="413">Payload exceeds configured maximum allowed size.</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(WebhookResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(WebhookResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> ReceiveWebhook([FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        // 1. Validate payload length against configured threshold
        if (Request.ContentLength.HasValue && Request.ContentLength.Value > _settings.MaxPayloadSizeBytes)
        {
            _logger.LogWarning("Rejected webhook with content length {Length} exceeding max allowed {Max}",
                Request.ContentLength.Value, _settings.MaxPayloadSizeBytes);

            return StatusCode(StatusCodes.Status413PayloadTooLarge, new ProblemDetails
            {
                Status = StatusCodes.Status413PayloadTooLarge,
                Title = "Payload Too Large",
                Detail = $"Request payload exceeds maximum allowed size of {_settings.MaxPayloadSizeBytes} bytes."
            });
        }

        // 2. Validate Security (HMAC / Secret Token)
        byte[] rawBodyBytes = System.Text.Encoding.UTF8.GetBytes(payload.GetRawText());
        var securityResult = _securityValidator.Validate(Request.Headers, rawBodyBytes);
        if (!securityResult.IsValid)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized Webhook",
                Detail = securityResult.FailureReason ?? "Security verification failed."
            });
        }

        // 3. Extract metadata
        string eventId = ExtractEventId(Request.Headers, payload);
        string? eventType = ExtractEventType(Request.Headers, payload);
        string sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        var headerDictionary = Request.Headers.ToDictionary(
            h => h.Key,
            h => h.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

        var webhookEvent = new WebhookEvent(
            Id: eventId,
            EventType: eventType,
            ReceivedAtUtc: DateTimeOffset.UtcNow,
            SourceIp: sourceIp,
            Payload: payload.Clone(),
            Headers: headerDictionary);

        // 4. Enqueue or process synchronously
        if (_settings.EnableBackgroundProcessing)
        {
            await _queue.EnqueueAsync(webhookEvent, cancellationToken);
            _logger.LogInformation("Enqueued webhook event {EventId} for background processing", eventId);

            var acceptedResponse = new WebhookResponse(
                EventId: eventId,
                Status: "Accepted",
                Message: "Webhook event received and enqueued for asynchronous processing.",
                Timestamp: DateTimeOffset.UtcNow);

            return Accepted(acceptedResponse);
        }
        else
        {
            await _processor.ProcessAsync(webhookEvent, cancellationToken);
            _logger.LogInformation("Synchronously processed webhook event {EventId}", eventId);

            var okResponse = new WebhookResponse(
                EventId: eventId,
                Status: "Processed",
                Message: "Webhook event received and processed successfully.",
                Timestamp: DateTimeOffset.UtcNow);

            return Ok(okResponse);
        }
    }

    /// <summary>
    /// Legacy route for backward compatibility with existing webhook providers calling /api/listener.
    /// Hidden from Swagger to keep documentation clean.
    /// </summary>
    [HttpPost("/api/listener")]
    [HttpPost("/api/v1/webhook")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public Task<IActionResult> LegacyReceiveWebhook([FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        return ReceiveWebhook(payload, cancellationToken);
    }

    /// <summary>
    /// Gets the current status of the webhook listener service and processing queue.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            status = "Healthy",
            backgroundProcessingEnabled = _settings.EnableBackgroundProcessing,
            securityValidationEnabled = _settings.EnableSecurityValidation,
            queuePendingItems = _queue.CurrentCount,
            serverUtcTime = DateTimeOffset.UtcNow
        });
    }

    private static string ExtractEventId(IHeaderDictionary headers, JsonElement root)
    {
        if (headers.TryGetValue("X-Event-Id", out var headerId) && !string.IsNullOrWhiteSpace(headerId))
        {
            return headerId.ToString();
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("id", out var idProp) ||
                root.TryGetProperty("eventId", out idProp) ||
                root.TryGetProperty("event_id", out idProp))
            {
                var val = idProp.GetString();
                if (!string.IsNullOrWhiteSpace(val)) return val;
            }
        }

        return Guid.NewGuid().ToString("N");
    }

    private static string? ExtractEventType(IHeaderDictionary headers, JsonElement root)
    {
        if (headers.TryGetValue("X-Event-Type", out var headerType) && !string.IsNullOrWhiteSpace(headerType))
        {
            return headerType.ToString();
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("event", out var eventProp) ||
                root.TryGetProperty("type", out eventProp) ||
                root.TryGetProperty("event_type", out eventProp) ||
                root.TryGetProperty("action", out eventProp))
            {
                return eventProp.GetString();
            }
        }

        return null;
    }
}

