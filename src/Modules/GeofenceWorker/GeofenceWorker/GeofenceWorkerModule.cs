using GeofenceWorker.Data;
using GeofenceWorker.Data.Repository;
using GeofenceWorker.Data.Repository.IRepository;
using GeofenceWorker.Services.RabbitMq;
using GeofenceWorker.Workers;
using GeofenceWorker.Workers.Features.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GeofenceWorker;

public static class GeofenceWorkerModule
{
    public static IServiceCollection AddGeofenceWorkerModule(this IServiceCollection services,
        IConfiguration configuration)
    {

        services.AddHttpClient();
        services.AddHostedService<Worker>();
        services.AddHostedService<AmqpMessage>();
        
        // Binding konfigurasi ke kelas RabbitMqSettings
        services.Configure<RabbitMqSettings>(
            configuration.GetSection("RabbitMq")
        );
        
        services.AddSingleton<RabbitMqSettings>(sp => 
            sp.GetRequiredService<IOptions<RabbitMqSettings>>().Value
        );
    
        var connectionString = configuration.GetConnectionString("Database");
        
        ////services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        ////services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();
        
        services.AddDbContext<GeofenceWorkerDbContext>( (sp,options) =>
        {
            options.AddInterceptors(new DateTimeKindInterceptor());
            ////options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseNpgsql(connectionString)
            .EnableSensitiveDataLogging()
            .LogTo(Console.WriteLine, LogLevel.Warning);
        });
        
        services.AddScoped<IGpsLastPositionRepository>(sp =>
            new GpsLastPositionRepository(
                connectionString!,
                sp.GetRequiredService<ILogger<GpsLastPositionRepository>>()
            ));
        services.AddScoped<IGpsApiLogRepository, GpsApiLogRepository>();
        services.AddScoped<IGpsLastPositionHRepository, GpsLastPositionHRepository>();
        services.AddSingleton<IRabbitMqService, RabbitMqService>();
        

        return services;
    }
    
    
    public static IApplicationBuilder UseGeofenceWorkerModule(this IApplicationBuilder app)
    {
        // Configure the HTTP request pipeline.

        // 1. Use Api Endpoint services

        // 2. Use Application Use Case services

        // 3. Use Data - Infrastructure services  
        ////app.UseMigration<CatalogDbContext>();

        return app;
    }

}