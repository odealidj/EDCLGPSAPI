
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

// Add services to the container.

//common services: carter, mediatr, fluentvalidation
////var catalogAssembly = typeof(CatalogModule).Assembly;
////var basketAssembly = typeof(BasketModule).Assembly;
var geofenceMasterAssembly = typeof(GeofenceMasterModule).Assembly;

builder.Services
    .AddCarterWithAssemblies(geofenceMasterAssembly);

builder.Services
    .AddMediatRWithAssemblies(geofenceMasterAssembly);

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

/*
builder.Services
    .AddMassTransitWithAssemblies(builder.Configuration, catalogAssembly, basketAssembly);
*/
//module services: catalog, basket, ordering
builder.Services
    .AddGeofenceMasterModule(builder.Configuration);


builder.Services
    .AddExceptionHandler<CustomExceptionHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.MapCarter();
app.UseSerilogRequestLogging();
app.UseExceptionHandler(options => { });

app
    .UseGeofenceMasterModule();

app.Run();