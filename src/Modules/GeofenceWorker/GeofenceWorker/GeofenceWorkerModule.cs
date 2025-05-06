using GeofenceWorker.Data;
using GeofenceWorker.Data.Repository;
using GeofenceWorker.Data.Repository.IRepository;
using GeofenceWorker.Services.RabbitMq;
using GeofenceWorker.Workers;
using GeofenceWorker.Workers.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Data.Interceptors;

namespace GeofenceWorker;

public static class GeofenceWorkerModule
{
    public static IServiceCollection AddGeofenceWorkerModule(this IServiceCollection services,
        IConfiguration configuration)
    {

        services.AddHttpClient();
        services.AddHostedService<Worker>();
        
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
        
        services.AddScoped<IGpsLastPositionHRepository, GpsLastPositionHRepository>();
        
        services.AddSingleton<IRabbitMqService, RabbitMqService>();
        

        return services;
    }

}