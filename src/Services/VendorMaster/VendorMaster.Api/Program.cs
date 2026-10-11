// aktifkan fleksibilitas timestamp npgsql agar kompatibel dengan tabel postgres
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// 0. open telemetry, metrics & idempotency filter
builder.Services.AddEdclOpenTelemetry(builder.Configuration, "vendor-master-svc");
builder.Services.AddEdclIdempotency(builder.Configuration);

var apiAssembly = typeof(Program).Assembly;
var applicationAssembly = typeof(IVendorMasterRepository).Assembly;

// 1. scan carter endpoints & mediatR handlers + validators & application services (AutoMapper)
builder.Services.AddCarterWithAssemblies(apiAssembly);
builder.Services.AddMediatRWithAssemblies(applicationAssembly, apiAssembly);
builder.Services.AddApplicationServices();

// 2. registrasi module & ef core postgres
builder.Services.AddVendorMasterModule(builder.Configuration);

// 3. exception handler global & swagger
builder.Services.AddExceptionHandler<CustomExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "EDCL Vendor & Geofence Master API",
        Version = "v1",
        Description = "Layanan Master Konfigurasi Vendor GPS Telematika dan Destinasi Geofence Pabrik Toyota (TMMIN). Menggunakan arsitektur Hexagonal murni di .NET 10."
    });
});

// 4. health checks postgresql
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Database") ?? string.Empty);

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("EnableSwagger", true))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "EDCL Vendor & Geofence Master API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseExceptionHandler(options => { });
app.UseEdclTraceContext();

app.UseVendorMasterModule();
app.MapCarter();
app.MapHealthChecks("/hc");
app.MapEdclPrometheus();

app.MapGet("/", () => Results.Json(new
{
    Service = "EDCL Vendor & Geofence Master Service",
    Version = "v2.0",
    Status = "Healthy",
    Architecture = "Hexagonal",
    Timestamp = DateTime.UtcNow
}));

app.Run();

public partial class Program
{
    protected Program() { }
}
