using GeofenceMaster.Data.Repository;
using GeofenceMaster.Data.Repository.IRepository;
using Microsoft.Extensions.Logging;

namespace GeofenceMaster;

public static class GeofenceMasterModule
{
    public static IServiceCollection AddGeofenceMasterModule(this IServiceCollection services,
        IConfiguration configuration)
    {
        
        // Data - Infrastructure services
        var connectionString = configuration.GetConnectionString("Database");

        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        services.AddDbContext<GeofenceMasterDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseNpgsql(connectionString)
            .EnableSensitiveDataLogging()
            .LogTo(Console.WriteLine, LogLevel.Warning);
        },ServiceLifetime.Scoped);
        
        // Add services to the container.

        // Api Endpoint services

        // Application Use Case services       
        services.AddScoped<IGeofenceMasterRepository, GeofenceMasterRepository>();
        
        // Tambahkan factory method untuk IGeofenceMasterRepository
        /*
        services.AddTransient<Func<IGeofenceMasterRepository>>(serviceProvider => () =>
        {
            var dbContext = serviceProvider.GetRequiredService<GeofenceMasterDbContext>();
            return new GeofenceMasterRepository(dbContext);
        });
        */
        
        

        ////services.AddScoped<IDataSeeder, CatalogDataSeeder>();

        return services;
    }

    public static IApplicationBuilder UseGeofenceMasterModule(this IApplicationBuilder app)
    {
        // Configure the HTTP request pipeline.

        // 1. Use Api Endpoint services

        // 2. Use Application Use Case services

        // 3. Use Data - Infrastructure services  
        ////app.UseMigration<CatalogDbContext>();

        return app;
    }
}