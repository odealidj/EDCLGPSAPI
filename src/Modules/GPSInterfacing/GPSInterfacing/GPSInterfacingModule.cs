using GPSInterfacing.Data.Repository;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data.Interceptors;


namespace GPSInterfacing;

public static class GPSInterfacingModule
{
    public static IServiceCollection AddGPSInterfacingModule(this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add services to the container.

        // Api Endpoint services

        // Application Use Case services       
        services.AddScoped<IGeofenceMasterRepository, GeofenceMasterRepository>();
        
        // Data - Infrastructure services
        var connectionString = configuration.GetConnectionString("Database");

        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        
        services.AddDbContext<GPSInterfacingDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseNpgsql(connectionString);
        });
        
        
        ////services.AddScoped<IDataSeeder, CatalogDataSeeder>();

        return services;
    }

    public static IApplicationBuilder UseGPSInterfacingModule(this IApplicationBuilder app)
    {
        // Configure the HTTP request pipeline.

        // 1. Use Api Endpoint services

        // 2. Use Application Use Case services

        // 3. Use Data - Infrastructure services  
        ////app.UseMigration<CatalogDbContext>();

        return app;
    }

}