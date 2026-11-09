using System.Reflection;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Webhook_ListenerDotNet.Configuration;
using Webhook_ListenerDotNet.Queue;
using Webhook_ListenerDotNet.Security;
using Webhook_ListenerDotNet.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Strongly typed configuration
builder.Services.Configure<WebhookSettings>(
    builder.Configuration.GetSection(WebhookSettings.SectionName));

// 2. Core framework services & CORS
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

// Allow all origins in local dev to avoid Swagger UI "Failed to fetch / CORS" issues
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 3. Rate Limiter to guard against flooding
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter(policyName: "webhook-policy", policyOptions =>
    {
        policyOptions.PermitLimit = 100;
        policyOptions.Window = TimeSpan.FromSeconds(10);
        policyOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        policyOptions.QueueLimit = 20;
    });
});

// 4. Webhook Domain Services
builder.Services.AddSingleton<IWebhookQueue, WebhookQueue>();
builder.Services.AddSingleton<IWebhookSecurityValidator, WebhookSecurityValidator>();
builder.Services.AddScoped<IWebhookProcessor, WebhookProcessor>();
builder.Services.AddHostedService<WebhookProcessingWorker>();

// 5. Swagger / OpenAPI Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DotNet Webhook Listener API",
        Version = "v1",
        Description = "Enterprise-grade, high-throughput asynchronous webhook listener built with .NET 10.",
        Contact = new OpenApiContact
        {
            Name = "Webhook Listener Team"
        }
    });

    // Provide a sample JSON schema for the dynamic JsonElement body in Swagger UI
    options.MapType<JsonElement>(() => new OpenApiSchema
    {
        Type = "object",
        Description = "Any valid JSON payload",
        Example = new OpenApiString("{\n  \"id\": \"evt_12345\",\n  \"event\": \"payment.success\",\n  \"data\": {\n    \"amount\": 49.99,\n    \"customer\": \"Mario Rossi\"\n  }\n}")
    });

    // Include XML comments if generated
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// 6. HTTP Pipeline
app.UseExceptionHandler();
app.UseStatusCodePages();

// Enable CORS
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DotNet Webhook Listener API v1");
        c.RoutePrefix = "swagger";
    });
}
else
{
    app.UseHttpsRedirection();
}

app.UseRateLimiter();

app.UseAuthorization();

// Health Check endpoint
app.MapHealthChecks("/health");

// Webhook and API controllers
app.MapControllers().RequireRateLimiting("webhook-policy");

app.Run();

// Expose Program class for WebApplicationFactory in integration tests
public partial class Program { }
