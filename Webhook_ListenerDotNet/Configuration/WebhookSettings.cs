namespace Webhook_ListenerDotNet.Configuration;

/// <summary>
/// Strongly-typed configuration options for webhook listener behavior, security and queuing.
/// </summary>
public sealed class WebhookSettings
{
    public const string SectionName = "WebhookSettings";

    /// <summary>
    /// If true, validates either secret header token or HMAC signature.
    /// </summary>
    public bool EnableSecurityValidation { get; set; } = false;

    /// <summary>
    /// Pre-shared secret header value for simple token authentication (e.g. X-Webhook-Secret).
    /// </summary>
    public string? SecretToken { get; set; }

    /// <summary>
    /// Header name used for pre-shared secret token validation.
    /// </summary>
    public string SecretTokenHeaderName { get; set; } = "X-Webhook-Secret";

    /// <summary>
    /// Cryptographic secret used to compute and verify HMAC-SHA256 signature (e.g. GitHub or Stripe-style).
    /// </summary>
    public string? HmacSecret { get; set; }

    /// <summary>
    /// Header name used for HMAC-SHA256 signature validation (e.g. X-Hub-Signature-256).
    /// </summary>
    public string HmacHeaderName { get; set; } = "X-Hub-Signature-256";

    /// <summary>
    /// Maximum allowed payload size in bytes (default: 10 MB).
    /// </summary>
    public long MaxPayloadSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Maximum capacity for the in-memory processing queue.
    /// </summary>
    public int QueueCapacity { get; set; } = 10_000;

    /// <summary>
    /// If true, payloads are queued and acknowledged immediately with 202 Accepted.
    /// If false, payloads are processed synchronously and returned with 200 OK.
    /// </summary>
    public bool EnableBackgroundProcessing { get; set; } = true;
}

