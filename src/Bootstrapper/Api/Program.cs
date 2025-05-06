
using GeofenceWorker;
using TrackDelivery;


var builder = WebApplication.CreateBuilder(args);

////builder.Host.UseSerilog((context, config) =>
    ////config.ReadFrom.Configuration(context.Configuration));

// Add services to the container.

//common services: carter, mediatr, fluentvalidation
////var catalogAssembly = typeof(CatalogModule).Assembly;
////var basketAssembly = typeof(BasketModule).Assembly;
var geofenceMasterAssembly = typeof(GeofenceMasterModule).Assembly;
////var geofenceWorkerModuleAssembly = typeof(GeofenceWorkerModule).Assembly;

builder.Services
    .AddCarterWithAssemblies(geofenceMasterAssembly);

builder.Services
    .AddMediatRWithAssemblies(geofenceMasterAssembly);

////builder.Services
   ////.AddMassTransitWithAssemblies(builder.Configuration, geofenceWorkerModuleAssembly);

/*
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});
*/

/*
builder.Services
    .AddMassTransitWithAssemblies(builder.Configuration, catalogAssembly, basketAssembly);
*/
//module services: catalog, basket, ordering
   builder.Services
       .AddGeofenceMasterModule(builder.Configuration)
       .AddTrackDeliveryModule(builder.Configuration)
       .AddGeofenceWorkerModule(builder.Configuration);



builder.Services
    .AddExceptionHandler<CustomExceptionHandler>();

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Database") ?? string.Empty);



// Registrasi layanan Health Checks
/*
builder.Services.AddHealthChecks()
    .AddCheck("Sample_HealthCheck", () => HealthCheckResult.Healthy("OK"), tags: new[] { "sample" });
*/


var app = builder.Build();

// Configure the HTTP request pipeline.

app.MapCarter();
////app.UseSerilogRequestLogging();
app.UseExceptionHandler(options => { });

app
    .UseGeofenceMasterModule()
    .UseTrackDeliveryModule();


app.UseHttpsRedirection();

// Endpoint default untuk health checks
app.MapHealthChecks("/hc", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => true, // Memeriksa semua health checks
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
        {
            Status = report.Status.ToString(),
            Checks = report.Entries.Select(e => new
            {
                Key = e.Key,
                Value = e.Value.Status.ToString(),
                Description = e.Value.Description
            }),
            TotalDuration = report.TotalDuration.TotalSeconds
        }));
    }
});


// Endpoint default
app.MapGet("/", () => Results.Json(new
{
    ApiName = "Edcl GPS Web API",
    Version = "1.0.1"
}));

app.Run();