using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Webhook_ListenerDotNet.Configuration;
using Webhook_ListenerDotNet.Security;
using Xunit;

namespace Webhook_ListenerDotNet.Tests;

public class WebhookSecurityValidatorTests
{
    [Fact]
    public void Validate_WhenSecurityDisabled_ShouldAlwaysReturnSuccess()
    {
        var settings = Options.Create(new WebhookSettings
        {
            EnableSecurityValidation = false
        });

        var validator = new WebhookSecurityValidator(settings, NullLogger<WebhookSecurityValidator>.Instance);
        var headers = new HeaderDictionary();
        var body = Encoding.UTF8.GetBytes("{\"test\":\"data\"}");

        var result = validator.Validate(headers, body);

        Assert.True(result.IsValid);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public void Validate_WhenSecretTokenMatches_ShouldReturnSuccess()
    {
        const string expectedToken = "secret-12345";
        var settings = Options.Create(new WebhookSettings
        {
            EnableSecurityValidation = true,
            SecretToken = expectedToken,
            SecretTokenHeaderName = "X-Webhook-Secret"
        });

        var validator = new WebhookSecurityValidator(settings, NullLogger<WebhookSecurityValidator>.Instance);
        var headers = new HeaderDictionary
        {
            { "X-Webhook-Secret", expectedToken }
        };
        var body = Encoding.UTF8.GetBytes("{\"test\": true}");

        var result = validator.Validate(headers, body);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenSecretTokenMissing_ShouldReturnFailure()
    {
        var settings = Options.Create(new WebhookSettings
        {
            EnableSecurityValidation = true,
            SecretToken = "secret-12345",
            SecretTokenHeaderName = "X-Webhook-Secret"
        });

        var validator = new WebhookSecurityValidator(settings, NullLogger<WebhookSecurityValidator>.Instance);
        var headers = new HeaderDictionary();
        var body = Encoding.UTF8.GetBytes("{\"test\": true}");

        var result = validator.Validate(headers, body);

        Assert.False(result.IsValid);
        Assert.Contains("Missing required 'X-Webhook-Secret'", result.FailureReason);
    }

    [Fact]
    public void Validate_WhenSecretTokenInvalid_ShouldReturnFailure()
    {
        var settings = Options.Create(new WebhookSettings
        {
            EnableSecurityValidation = true,
            SecretToken = "expected-token",
            SecretTokenHeaderName = "X-Webhook-Secret"
        });

        var validator = new WebhookSecurityValidator(settings, NullLogger<WebhookSecurityValidator>.Instance);
        var headers = new HeaderDictionary
        {
            { "X-Webhook-Secret", "wrong-token" }
        };
        var body = Encoding.UTF8.GetBytes("{\"test\": true}");

        var result = validator.Validate(headers, body);

        Assert.False(result.IsValid);
        Assert.Equal("Invalid secret token.", result.FailureReason);
    }

    [Fact]
    public void Validate_WhenHmacSignatureMatches_ShouldReturnSuccess()
    {
        const string hmacSecret = "super-secret-signing-key";
        var body = Encoding.UTF8.GetBytes("{\"action\":\"payment.succeeded\",\"amount\":100}");

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(hmacSecret));
        string validHex = Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();

        var settings = Options.Create(new WebhookSettings
        {
            EnableSecurityValidation = true,
            HmacSecret = hmacSecret,
            HmacHeaderName = "X-Hub-Signature-256"
        });

        var validator = new WebhookSecurityValidator(settings, NullLogger<WebhookSecurityValidator>.Instance);
        var headers = new HeaderDictionary
        {
            { "X-Hub-Signature-256", $"sha256={validHex}" }
        };

        var result = validator.Validate(headers, body);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenHmacSignatureInvalid_ShouldReturnFailure()
    {
        const string hmacSecret = "super-secret-signing-key";
        var body = Encoding.UTF8.GetBytes("{\"action\":\"test\"}");

        var settings = Options.Create(new WebhookSettings
        {
            EnableSecurityValidation = true,
            HmacSecret = hmacSecret,
            HmacHeaderName = "X-Hub-Signature-256"
        });

        var validator = new WebhookSecurityValidator(settings, NullLogger<WebhookSecurityValidator>.Instance);
        var headers = new HeaderDictionary
        {
            { "X-Hub-Signature-256", "sha256=00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff" }
        };

        var result = validator.Validate(headers, body);

        Assert.False(result.IsValid);
        Assert.Equal("HMAC signature verification failed.", result.FailureReason);
    }
}

