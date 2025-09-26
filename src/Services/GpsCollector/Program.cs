using GpsCollector.Application.DTOs;
using GpsCollector.Application.Ports;
using GpsCollector.Extensions;
using GpsCollector.Metrics;
using Shared.Data.Mongo.DTOs;
using Shared.Data.Mongo.Repositories;
using Shared.Infrastructure.Telemetry;
using Shared.Messaging.RabbitMqClient.Extension;
using Shared.Pagination;

var builder = WebApplication.CreateBuilder(args);

// Configure System.Text.Json Source Generator for AOT readiness
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, GpsCollectorJsonContext.Default);
});

// 0. Distributed Tracing & Metrics (OpenTelemetry & Prometheus)
builder.Services.AddSingleton<ICollectorMetrics, CollectorMetrics>();
builder.Services.AddEdclOpenTelemetry(builder.Configuration, "gps-collector-svc");

// 1. Register GpsCollector Hexagonal Module & RabbitMQ
builder.Services.AddGpsCollectorModule(builder.Configuration);
builder.Services.AddRabbitMqModule(builder.Configuration);

// 2. Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Database") ?? string.Empty);

var app = builder.Build();

app.UseEdclTraceContext();

app.MapHealthChecks("/hc");
app.MapEdclPrometheus();

// Health & Info Endpoint
app.MapGet("/", () => Results.Ok(new CollectorStatusDto(
    Service: "EDCL GPS Collector Service",
    Role: "Telemetry Ingestion & Vendor Polling Worker",
    Status: "Running",
    Architecture: "Hexagonal",
    AotReady: true,
    Timestamp: DateTime.UtcNow
)));

// Manual Trigger Endpoints untuk integrasi testing dan ad-hoc polling
app.MapPost("/api/v1/collector/trigger", async (IGpsCollectorOrchestrator orchestrator, CancellationToken ct) =>
{
    var results = await orchestrator.CollectAllVendorsAsync(ct);
    return Results.Ok(results);
});

app.MapPost("/api/v1/collector/trigger/{vendorId:guid}", async (Guid vendorId, IGpsCollectorOrchestrator orchestrator, CancellationToken ct) =>
{
    var result = await orchestrator.CollectVendorAsync(vendorId, ct);
    return Results.Ok(result);
});

// Arsip Data Raw Response Asli di MongoDB (dengan filter & pagination)
app.MapGet("/api/v1/collector/raw-responses", async (
    string? vendorId,
    string? vendorName,
    DateTime? fromDate,
    DateTime? toDate,
    string? search,
    int? pageIndex,
    int? pageSize,
    IGpsRawResponseRepository rawRepo,
    CancellationToken ct) =>
{
    var filter = new RawResponseFilterRequest
    {
        VendorId = vendorId,
        VendorName = vendorName,
        FromDate = fromDate,
        ToDate = toDate,
        Search = search,
        PageIndex = pageIndex ?? 1,
        PageSize = pageSize ?? 20
    };
    var result = await rawRepo.QueryRawResponsesAsync(filter, ct);
    return Results.Ok(result);
})
.WithName("QueryCollectorRawResponses")
.WithSummary("Lihat arsip data raw response asli dari vendor di MongoDB dengan filter dan paginasi");

app.MapGet("/api/v1/collector/raw-responses/{id}", async (
    string id,
    IGpsRawResponseRepository rawRepo,
    CancellationToken ct) =>
{
    var result = await rawRepo.GetRawResponseByIdAsync(id, ct);
    return result != null
        ? Results.Ok(result)
        : Results.NotFound(new { Message = $"Raw response dengan ID {id} tidak ditemukan di MongoDB." });
})
.WithName("GetCollectorRawResponseById")
.WithSummary("Ambil detail satu dokumen data raw response asli berdasarkan ID MongoDB");

app.Run();

// Marker untuk integration testing
public partial class Program { }
