using System.Threading.RateLimiting;
using ApiGateway;
using Microsoft.AspNetCore.RateLimiting;
using Shared.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

// 0. Distributed Tracing & Metrics (OpenTelemetry)
builder.Services.AddEdclOpenTelemetry(builder.Configuration, "api-gateway");

// 1. Add YARP Reverse Proxy from Configuration
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiGatewayJsonSerializerContext.Default);
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// 2. Health Checks
builder.Services.AddHealthChecks();

// 3. CORS Policy (Restricted to Trusted Corporate & Telematics Origins)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["https://*.toyota.co.id", "https://localhost:3000", "https://localhost:5001"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .SetIsOriginAllowedToAllowWildcardSubdomains()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// 4. Rate Limiting (Fixed Window per Client IP)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 200,
                QueueLimit = 50,
                Window = TimeSpan.FromSeconds(1)
            }));
});

var app = builder.Build();

app.UseCors("DefaultCorsPolicy");
app.UseRateLimiter();

// 5. Distributed Tracing (W3C trace_id) & Correlation ID Middleware
app.UseEdclTraceContext();

// 6. Endpoints
app.MapHealthChecks("/hc");
app.MapEdclPrometheus();

app.MapGet("/", () => Results.Ok(new GatewayStatusDto(
    Service: "EDCL Telematics API Gateway",
    Engine: "YARP Reverse Proxy (.NET 10 Native AOT)",
    Status: "Healthy",
    Version: "2.1.0"
)));

// 7. Map Reverse Proxy
app.MapReverseProxy();

app.Run();

namespace ApiGateway
{
    public sealed record GatewayStatusDto(
        string Service,
        string Engine,
        string Status,
        string Version);

    [System.Text.Json.Serialization.JsonSerializable(typeof(GatewayStatusDto))]
    internal partial class ApiGatewayJsonSerializerContext : System.Text.Json.Serialization.JsonSerializerContext
    {
    }
}
