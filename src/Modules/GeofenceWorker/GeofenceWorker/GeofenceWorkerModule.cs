using GeofenceWorker.Data;
using GeofenceWorker.Data.Repository;
using GeofenceWorker.Data.Repository.IRepository;
using GeofenceWorker.Workers.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.Data.Interceptors;

namespace GeofenceWorker;

public static class GeofenceWorkerModule
{
    public static IServiceCollection AddGeofenceWorkerModule(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpClient();
        services.AddHostedService<Worker>();
        
        var connectionString = configuration.GetConnectionString("Database");
        
        ////services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        ////services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();
        
        services.AddDbContext<GeofenceWorkerDbContext>( (sp,options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseNpgsql(connectionString);
            //.EnableSensitiveDataLogging()
            //.LogTo(Console.WriteLine, LogLevel.Information);
        });
        
        services.AddScoped<IGpsLastPositionHRepository, GpsLastPositionHRepository>();

        return services;
    }

}