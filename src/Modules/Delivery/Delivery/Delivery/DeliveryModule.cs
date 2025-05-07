using Delivery.Data;
using Delivery.Data.Repositories;
using Delivery.Data.Repositories.IRepositories;

namespace Delivery;

public static class DeliveryModule
{
    public static IServiceCollection AddDeliveryModule(this IServiceCollection services,
        IConfiguration configuration)
    {
        
        var connectionString = configuration.GetConnectionString("Database");
        
        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();
        
        services.AddDbContext<DeliveryDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseNpgsql(connectionString)
                .EnableSensitiveDataLogging()
                .LogTo(Console.WriteLine, LogLevel.Warning);
        },ServiceLifetime.Scoped);
        
        services.AddScoped<IDeliveryRepository, DeliveryRepository>();
        
        return services;
    }

    public static IApplicationBuilder UseDeliveryModule(this IApplicationBuilder app)
    {
        // Configure the HTTP request pipeline.

        // 1. Use Api Endpoint services

        // 2. Use Application Use Case services

        // 3. Use Data - Infrastructure services  
        ////app.UseMigration<CatalogDbContext>();

        return app;
    }
}