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
        
        services.AddScoped<IDeliveryDapperRepository>(sp =>
            new DeliveryDapperRepository(
                connectionString!,
                sp.GetRequiredService<ILogger<DeliveryDapperRepository>>()
            ));
        
        /*
        services.AddScoped<Func<IDeliveryRepository>>(serviceProvider => () =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<DeliveryRepository>>();
            var dbContext = serviceProvider.GetRequiredService<DeliveryDbContext>();
            return new DeliveryRepository(connectionString!, dbContext, logger);
        });
        */
        
        // Daftarkan repository dengan ftory pattern
        /*
        services.AddScoped<IDeliveryRepository>(sp =>
        {
            // Resolve DbContext secara otomatis dari DI container
            var dbContext = sp.GetService<DeliveryDbContext>();

            // Resolve ILogger secara otomatis dari DI container
            var logger = sp.GetRequiredService<ILogger<DeliveryRepository>>();

            // Buat instance repository dengan connectionString, dbContext, dan logger
            return new DeliveryRepository(connectionString, dbContext, logger);
        });
        */
        
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