using MassTransit;
using Shared.Messaging.Extensions;

var builder = Host.CreateApplicationBuilder(args);

var geofenceWorkerModuleAssembly = typeof(GeofenceWorkerModule).Assembly;

//builder.Services.AddHostedService<Worker>();

builder.Services
   .AddMassTransitWithAssemblies(builder.Configuration, geofenceWorkerModuleAssembly);

builder.Services
    .AddGeofenceWorkerModule(builder.Configuration);

var host = builder.Build();

host.Run();