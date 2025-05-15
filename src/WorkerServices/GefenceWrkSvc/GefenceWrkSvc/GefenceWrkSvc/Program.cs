using GefenceWrkSvc;
using GeofenceWorker;

var builder = Host.CreateApplicationBuilder(args);

//var geofenceWorkerAssembly = typeof(GeofenceWorkerModule).Assembly;

//builder.Services.AddHostedService<Worker>();

builder.Services    
    .AddGeofenceWorkerModule(builder.Configuration);

var host = builder.Build();
host.Run();