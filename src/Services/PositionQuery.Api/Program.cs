using Carter;
using PositionQuery.Api.Services;
using Shared.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

// 0. Distributed Tracing & Metrics (OpenTelemetry)
builder.Services.AddEdclOpenTelemetry(builder.Configuration, "position-query-svc");

// 1. Carter & Swagger
builder.Services.AddCarter();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2. Redis Distributed Cache
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "";
});

// 3. Application Services
builder.Services.AddScoped<IPositionQueryService, PositionQueryService>();

// 4. Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Database") ?? string.Empty);

var app = builder.Build();

// 5. Middleware Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseEdclTraceContext();

app.MapHealthChecks("/hc");
app.MapEdclPrometheus();
app.MapCarter();

app.MapGet("/", () => Results.Json(new
{
    Service = "EDCL Position Query Service",
    Version = "v1.0",
    Status = "Healthy",
    CacheStrategy = "Redis-First with PostgreSQL Partition Fallback",
    Timestamp = DateTime.UtcNow
}));

app.Run();
