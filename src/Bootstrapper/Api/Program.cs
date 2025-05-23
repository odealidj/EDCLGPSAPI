
using System.Text.Json;
using Carter;
using Delivery;
using GeofenceMaster;
using GeofenceWorker;
using Microsoft.AspNetCore.Diagnostics;
using Serilog;
using Shared.Exceptions;
using Shared.Exceptions.Handler;
using Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

////builder.Host.UseSerilog((context, config) =>
    ////config.ReadFrom.Configuration(context.Configuration));

// Add services to the container.

var deliveryAssembly = typeof(DeliveryModule).Assembly;
var geofenceMasterAssembly = typeof(GeofenceMasterModule).Assembly;

builder.Services
    .AddCarterWithAssemblies(geofenceMasterAssembly, deliveryAssembly);

builder.Services
    .AddMediatRWithAssemblies(geofenceMasterAssembly, deliveryAssembly);


builder.Services
    .AddGeofenceMasterModule(builder.Configuration)
    .AddDeliveryModule(builder.Configuration);
    //.AddGeofenceWorkerModule(builder.Configuration);

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

////app.UseExceptionHandler(options => { });

app.UseExceptionHandler(options =>
{
    options.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        // Tentukan status code dan respons berdasarkan jenis exception
        if (exception is BadRequestException badRequestException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest ;
            context.Response.ContentType = "application/json";
            
            var result = JsonSerializer.Serialize(new { message = badRequestException.Message, details = badRequestException.Details });

            /*
            var response = new
            {
                title = badRequestException.Title,
                status = badRequestException.Status,
                detail = badRequestException.Detail,
                instance = badRequestException.Instance,
                traceId = context.TraceIdentifier
            }; */

            await context.Response.WriteAsJsonAsync(result);
        }
        else
        {
            // Tangani exception lain (misalnya, internal server error)
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var response = new
            {
                title = "Internal Server Error",
                status = 500,
                detail = exception?.Message ?? "An unexpected error occurred.",
                instance = context.Request.Path,
                traceId = context.TraceIdentifier
            };

            await context.Response.WriteAsJsonAsync(response);
        }
    });
});

app
    .UseGeofenceMasterModule()
    .UseDeliveryModule();


////app.UseHttpsRedirection();

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