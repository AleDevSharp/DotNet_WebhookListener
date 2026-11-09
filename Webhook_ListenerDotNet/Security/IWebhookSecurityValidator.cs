using Webhook_ListenerDotNet.Models;

namespace Webhook_ListenerDotNet.Security;

public interface IWebhookSecurityValidator
{
    /// <summary>
    /// Validates the authenticity of the incoming request based on configured secret token or HMAC signature.
    /// </summary>
    /// <param name="headers">HTTP request headers.</param>
    /// <param name="rawBody">Raw request body bytes for signature calculation.</param>
    /// <returns>Validation result indicating success or failure reason.</returns>
    SecurityValidationResult Validate(IHeaderDictionary headers, byte[] rawBody);
}

