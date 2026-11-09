using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Webhook_ListenerDotNet.Configuration;
using Webhook_ListenerDotNet.Models;

namespace Webhook_ListenerDotNet.Security;

public sealed class WebhookSecurityValidator : IWebhookSecurityValidator
{
    private readonly WebhookSettings _settings;
    private readonly ILogger<WebhookSecurityValidator> _logger;

    public WebhookSecurityValidator(
        IOptions<WebhookSettings> settings,
        ILogger<WebhookSecurityValidator> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public SecurityValidationResult Validate(IHeaderDictionary headers, byte[] rawBody)
    {
        if (!_settings.EnableSecurityValidation)
        {
            return SecurityValidationResult.Success();
        }

        // 1. Check Pre-Shared Secret Token (if configured)
        if (!string.IsNullOrEmpty(_settings.SecretToken))
        {
            if (!headers.TryGetValue(_settings.SecretTokenHeaderName, out var providedTokenValues) ||
                string.IsNullOrEmpty(providedTokenValues))
            {
                _logger.LogWarning("Missing required secret token header '{HeaderName}'", _settings.SecretTokenHeaderName);
                return SecurityValidationResult.Failure($"Missing required '{_settings.SecretTokenHeaderName}' header.");
            }

            var providedTokenBytes = Encoding.UTF8.GetBytes(providedTokenValues.ToString());
            var expectedTokenBytes = Encoding.UTF8.GetBytes(_settings.SecretToken);

            if (!CryptographicOperations.FixedTimeEquals(providedTokenBytes, expectedTokenBytes))
            {
                _logger.LogWarning("Invalid secret token provided in header '{HeaderName}'", _settings.SecretTokenHeaderName);
                return SecurityValidationResult.Failure("Invalid secret token.");
            }
        }

        // 2. Check HMAC-SHA256 signature (if configured)
        if (!string.IsNullOrEmpty(_settings.HmacSecret))
        {
            if (!headers.TryGetValue(_settings.HmacHeaderName, out var providedHmacValues) ||
                string.IsNullOrEmpty(providedHmacValues))
            {
                _logger.LogWarning("Missing required HMAC signature header '{HeaderName}'", _settings.HmacHeaderName);
                return SecurityValidationResult.Failure($"Missing required '{_settings.HmacHeaderName}' header.");
            }

            string signatureHeader = providedHmacValues.ToString().Trim();
            // Supports standard format 'sha256=<hex>' or raw hex
            string expectedHexPrefix = "sha256=";
            string rawProvidedHex = signatureHeader.StartsWith(expectedHexPrefix, StringComparison.OrdinalIgnoreCase)
                ? signatureHeader[expectedHexPrefix.Length..]
                : signatureHeader;

            byte[] providedSignatureBytes;
            try
            {
                providedSignatureBytes = Convert.FromHexString(rawProvidedHex);
            }
            catch (FormatException)
            {
                _logger.LogWarning("Malformed hexadecimal HMAC signature received: '{Signature}'", signatureHeader);
                return SecurityValidationResult.Failure("Malformed HMAC signature format.");
            }

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_settings.HmacSecret));
            byte[] computedHash = hmac.ComputeHash(rawBody);

            if (!CryptographicOperations.FixedTimeEquals(computedHash, providedSignatureBytes))
            {
                _logger.LogWarning("HMAC-SHA256 signature mismatch for header '{HeaderName}'", _settings.HmacHeaderName);
                return SecurityValidationResult.Failure("HMAC signature verification failed.");
            }
        }

        return SecurityValidationResult.Success();
    }
}

