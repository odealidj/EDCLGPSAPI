using GeofenceWorker.Data;
using GeofenceWorker.Data.Repository;
using GeofenceWorker.Data.Repository.IRepository;
using GeofenceWorker.Services.RabbitMq;
using GeofenceWorker.Services.RabbitMqClient;
using GeofenceWorker.Services.RabbitMqClient.factory;
using GeofenceWorker.Services.RabbitMqClient.Services;
using GeofenceWorker.Workers;
using GeofenceWorker.Workers.Features.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Data.Mongo;
using Shared.Messaging.RabbitMqClient.Provider;
using RabbitMqConnectionProvider = GeofenceWorker.Services.RabbitMqClient.RabbitMqConnectionProvider;

namespace GeofenceWorker;

public static class GeofenceWorkerModule
{
    public static IServiceCollection AddGeofenceWorkerModule(this IServiceCollection services,
        IConfiguration configuration)
    {

        services.AddHttpClient();
        
        var isWorkerEnabled = configuration.GetValue<bool>("Worker:Enabled", true);
        if (isWorkerEnabled)
        {
            services.AddHostedService<Worker>();
        }
        
        // Binding konfigurasi ke kelas RabbitMqSettings
        services.Configure<RabbitMqSettings>(
            configuration.GetSection("RabbitMq")
        );
        
        services.AddSingleton<RabbitMqSettings>(sp => 
            sp.GetRequiredService<IOptions<RabbitMqSettings>>().Value
        );

        // Register MongoDB and Redis caching for telemetry logs & fast query
        services.AddMongoDbLogs(configuration);
        var redisConn = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConn))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConn;
                options.InstanceName = "edcl:";
            });
        }
    
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
        ////services.AddSingleton<IRabbitMqService, RabbitMqService>();
        
        services.AddSingleton<IRabbitMqConnectionProvider, RabbitMqConnectionProviderSsl>();
        services.AddSingleton<IRabbitMqConnectionProvider, RabbitMqConnectionProvider>();
        services.AddSingleton<IRabbitMqConnectionProviderFactory, RabbitMqConnectionProviderFactory>();
        services.AddSingleton<IGpsPublisherService, GpsPublisherService>();
        


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