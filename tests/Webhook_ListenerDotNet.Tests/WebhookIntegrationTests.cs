using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Webhook_ListenerDotNet.Models;
using Xunit;

namespace Webhook_ListenerDotNet.Tests;

public class WebhookIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public WebhookIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StatusEndpoint_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/api/webhook/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReceiveWebhook_WithValidJson_ShouldReturnAccepted()
    {
        var jsonContent = new StringContent(
            "{\"id\":\"evt_001\",\"event\":\"user.signup\",\"data\":{\"userId\":\"usr_999\"}}",
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/webhook", jsonContent);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<WebhookResponse>();
        Assert.NotNull(result);
        Assert.Equal("evt_001", result.EventId);
        Assert.Equal("Accepted", result.Status);
    }

    [Fact]
    public async Task ReceiveWebhook_LegacyListenerRoute_ShouldReturnAccepted()
    {
        var jsonContent = new StringContent(
            "{\"id\":\"legacy_01\",\"event\":\"ping\"}",
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/listener", jsonContent);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<WebhookResponse>();
        Assert.NotNull(result);
        Assert.Equal("legacy_01", result.EventId);
    }

    [Fact]
    public async Task ReceiveWebhook_WithMalformedJson_ShouldReturnBadRequest()
    {
        var malformedContent = new StringContent(
            "{ invalid json format: missing quotes }",
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/webhook", malformedContent);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReceiveWebhook_WithEmptyBody_ShouldReturnBadRequest()
    {
        var emptyContent = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/webhook", emptyContent);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

