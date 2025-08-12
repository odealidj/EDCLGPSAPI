using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// 1. Add YARP Reverse Proxy from Configuration
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// 2. Health Checks
builder.Services.AddHealthChecks();

// 3. CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        policy.AllowAnyOrigin()
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

// 5. Distributed Tracing & Correlation ID Middleware
app.Use(async (context, next) =>
{
    const string correlationHeader = "X-Correlation-Id";
    if (!context.Request.Headers.TryGetValue(correlationHeader, out var correlationId) ||
        string.IsNullOrWhiteSpace(correlationId))
    {
        correlationId = Guid.NewGuid().ToString("N");
        context.Request.Headers[correlationHeader] = correlationId;
    }
    context.Response.Headers[correlationHeader] = correlationId;
    await next();
});

// 6. Endpoints
app.MapHealthChecks("/hc");

app.MapGet("/", () => Results.Json(new
{
    Service = "EDCL Telematics API Gateway",
    Engine = "YARP (.NET 8)",
    Status = "Healthy",
    Version = "2.1.0"
}));

// 7. Map Reverse Proxy
app.MapReverseProxy();

app.Run();
