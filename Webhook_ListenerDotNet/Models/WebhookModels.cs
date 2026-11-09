using System.Text.Json;

namespace Webhook_ListenerDotNet.Models;

/// <summary>
/// Represents an incoming webhook event normalized with its metadata.
/// </summary>
/// <param name="Id">Unique identifier generated or extracted for this event.</param>
/// <param name="EventType">Type or category of event (if provided by header or payload).</param>
/// <param name="ReceivedAtUtc">Timestamp when the webhook was received.</param>
/// <param name="SourceIp">IP address of the sender.</param>
/// <param name="Payload">Parsed JSON element payload.</param>
/// <param name="Headers">Relevant request headers.</param>
public sealed record WebhookEvent(
    string Id,
    string? EventType,
    DateTimeOffset ReceivedAtUtc,
    string SourceIp,
    JsonElement Payload,
    IReadOnlyDictionary<string, string> Headers
);

/// <summary>
/// Standard response returned to webhook callers.
/// </summary>
public sealed record WebhookResponse(
    string EventId,
    string Status,
    string Message,
    DateTimeOffset Timestamp
);

/// <summary>
/// Result of webhook security verification.
/// </summary>
public sealed record SecurityValidationResult(
    bool IsValid,
    string? FailureReason = null
)
{
    public static SecurityValidationResult Success() => new(true);
    public static SecurityValidationResult Failure(string reason) => new(false, reason);
}

