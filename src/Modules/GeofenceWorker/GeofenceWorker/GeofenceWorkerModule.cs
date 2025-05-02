using GeofenceWorker.Data;
using GeofenceWorker.Data.Repository;
using GeofenceWorker.Data.Repository.IRepository;
using GeofenceWorker.Services.RabbitMq;
using GeofenceWorker.Workers;
using GeofenceWorker.Workers.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GeofenceWorker;

public static class GeofenceWorkerModule
{
    public static IServiceCollection AddGeofenceWorkerModule(this IServiceCollection services,
        IConfiguration configuration)
    {

        //_settings = configuration.GetSection("RabbitMQ").Get<RabbitMQSettings>() ?? new RabbitMQSettings();

        //configuration["DatabaseSettings:DefaultConnection"]??string.Empty;

        services.AddHttpClient();
        services.AddHostedService<Worker>();
        
        // Bind RabbitMqSettings from configuration
        var rmq = configuration.GetSection("RabbitMq");
        var hostName = configuration["RabbitMq:HostName"] ?? string.Empty;
        services.Configure<RabbitMqSettings>(configuration.GetSection("RabbitMq"));
    
        var connectionString = configuration.GetConnectionString("Database");
        
        ////services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        ////services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();
        
        services.AddDbContext<GeofenceWorkerDbContext>( (sp,options) =>
        {
            options.AddInterceptors(new DateTimeKindInterceptor());
            //options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseNpgsql(connectionString)
            .EnableSensitiveDataLogging()
            .LogTo(Console.WriteLine, LogLevel.Debug);
        });
        
        services.AddScoped<IGpsLastPositionHRepository, GpsLastPositionHRepository>();
        
        services.AddSingleton<IRabbitMqService, RabbitMqService>();
        

        return services;
    }

}