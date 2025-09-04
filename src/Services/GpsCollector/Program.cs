using GeofenceWorker;
using GpsCollector.Metrics;
using Shared.Infrastructure.Telemetry;
using Shared.Messaging.RabbitMqClient.Extension;

var builder = WebApplication.CreateBuilder(args);

// 0. Distributed Tracing & Metrics (OpenTelemetry & Prometheus)
builder.Services.AddSingleton<ICollectorMetrics, CollectorMetrics>();
builder.Services.AddEdclOpenTelemetry(builder.Configuration, "gps-collector-svc");

// 1. Register Geofence Worker Module (MongoDB, Redis, PostgreSQL, Polly, RabbitMQ)
builder.Services.AddGeofenceWorkerModule(builder.Configuration);
builder.Services.AddRabbitMqModule(builder.Configuration);

// 2. Health Checks for Kubernetes Liveness & Readiness Probes
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Database") ?? string.Empty);

var app = builder.Build();

app.UseGeofenceWorkerModule();
app.UseEdclTraceContext();

app.MapHealthChecks("/hc");
app.MapEdclPrometheus();

app.MapGet("/", () => Results.Json(new
{
    Service = "EDCL GPS Collector Service",
    Role = "Telemetry Ingestion & Vendor Polling Worker",
    Status = "Running",
    Timestamp = DateTime.UtcNow
}));

app.Run();
