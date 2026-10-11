using Carter;
using DeliveryTracking;
using Microsoft.OpenApi.Models;
using Shared.Exceptions.Handler;
using Shared.Extensions;
using Shared.Infrastructure.Idempotency;
using Shared.Infrastructure.Telemetry;
// aktifkan fleksibilitas timestamp npgsql agar kompatibel dengan tabel lama postgres
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// 0. distributed tracing, metrics & filter idempotensi
builder.Services.AddEdclOpenTelemetry(builder.Configuration, "delivery-tracking-svc");
builder.Services.AddEdclIdempotency(builder.Configuration);

var deliveryAssembly = typeof(DeliveryModule).Assembly;

// 1. scan endpoint carter & pipeline mediatR
builder.Services.AddCarterWithAssemblies(deliveryAssembly);
builder.Services.AddMediatRWithAssemblies(deliveryAssembly);

// 2. registrasi modul delivery tracking & npgsql data source (ado.net)
builder.Services.AddDeliveryModule(builder.Configuration);

// 3. exception handler global & dokumentasi openapi swagger
builder.Services.AddExceptionHandler<CustomExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "EDCL Delivery Tracking API",
        Version = "v1",
        Description = "Layanan Pelacakan Pengiriman, Kueri Posisi Armada (Redis-first) & Titik Telemetri GPS Toyota TMMIN (.NET 10 - ADO.NET Npgsql Binary COPY)."
    });
});

// 4. health checks postgresql
var healthDbConn = builder.Configuration.GetConnectionString("Database")
    ?? builder.Configuration["Database"]
    ?? string.Empty;
builder.Services.AddHealthChecks()
    .AddNpgSql(healthDbConn);

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("EnableSwagger", true))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "EDCL Delivery Tracking API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseExceptionHandler(options => { });
app.UseEdclTraceContext();

app.UseDeliveryModule();
app.MapCarter();
app.MapHealthChecks("/hc");
app.MapEdclPrometheus();

app.MapGet("/", () => Results.Json(new
{
    Service = "EDCL Delivery Tracking Service",
    Version = "v1.0",
    Status = "Healthy",
    Runtime = ".NET 10 (ReadyToRun)",
    Persistence = "ADO.NET Npgsql (Binary COPY Bulk)",
    PositionQuery = "Redis-First with PostgreSQL ADO.NET Fallback",
    Timestamp = DateTime.UtcNow
}));

await app.RunAsync();

public partial class Program
{
    protected Program() { }
}
