using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TrackDelivery;

public static class TrackDeliveryModule
{
    public static IServiceCollection AddTrackDeliveryModule(this IServiceCollection services,
        IConfiguration configuration)
    {
        return services;
    }

    public static IApplicationBuilder UseTrackDeliveryModule(this IApplicationBuilder app)
    {
        // Configure the HTTP request pipeline.

        // 1. Use Api Endpoint services

        // 2. Use Application Use Case services

        // 3. Use Data - Infrastructure services  
        ////app.UseMigration<CatalogDbContext>();

        return app;
    }
    
}