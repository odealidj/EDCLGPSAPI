using GeofenceWorker.Data;
using GeofenceWorker.Workers.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GeofenceWorker;

public static class GeofenceWorkerModule
{
    public static IServiceCollection AddGeofenceWorkerModule(this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add services to the container.

        // Api Endpoint services

        // Application Use Case services       
        
        
        // Register HttpClient
        services.AddHttpClient();
        
        // Data - Infrastructure services
        var connectionString = configuration.GetConnectionString("Database");
        
        services.AddDbContext<GeofenceWorkerDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            //.EnableSensitiveDataLogging()
            //.LogTo(Console.WriteLine, LogLevel.Information);
        });
        
        services.AddHostedService<Worker>();

        ////services.AddScoped<IDataSeeder, CatalogDataSeeder>();

        return services;
    }

}