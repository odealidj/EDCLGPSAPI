using Carter;
using GeofenceMaster;
using Shared.Exceptions.Handler;
using Shared.Extensions;
using Shared.Infrastructure.Idempotency;
using Shared.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

// 0. Distributed Tracing, Metrics & Idempotency
builder.Services.AddEdclOpenTelemetry(builder.Configuration, "vendor-master-svc");
builder.Services.AddEdclIdempotency(builder.Configuration);

var geofenceMasterAssembly = typeof(GeofenceMasterModule).Assembly;

// 1. Carter & MediatR assembly scanning
builder.Services.AddCarterWithAssemblies(geofenceMasterAssembly);
builder.Services.AddMediatRWithAssemblies(geofenceMasterAssembly);

// 2. Register GeofenceMaster Module
builder.Services.AddGeofenceMasterModule(builder.Configuration);

// 3. Exception Handling & OpenAPI/Swagger
builder.Services.AddExceptionHandler<CustomExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 4. Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Database") ?? string.Empty);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler(options => { });
app.UseEdclTraceContext();

app.UseGeofenceMasterModule();
app.MapCarter();
app.MapHealthChecks("/hc");
app.MapEdclPrometheus();

app.MapGet("/", () => Results.Json(new
{
    Service = "EDCL Vendor & Geofence Master Service",
    Version = "v1.0",
    Status = "Healthy",
    Timestamp = DateTime.UtcNow
}));

app.Run();
