namespace VendorMaster.Api;

// module extension buat registrasi dependency injection vendor master
public static class VendorMasterModule
{
    public static IServiceCollection AddVendorMasterModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");

        // interceptor audit trail & domain events
        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        // registrasi dbcontext postgresql
        services.AddDbContext<VendorMasterDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseNpgsql(connectionString)
                .EnableSensitiveDataLogging(false)
                .LogTo(Console.WriteLine, LogLevel.Warning);
        }, ServiceLifetime.Scoped);

        // registrasi port repository
        services.AddScoped<IVendorMasterRepository, VendorMasterRepository>();

        // registrasi redis caching untuk sinkronisasi vendor aktif
        var redisConn = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        try
        {
            var multiplexer = StackExchange.Redis.ConnectionMultiplexer.Connect(redisConn);
            services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(multiplexer);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VendorMasterModule] Warning: Redis connection failed ({ex.Message}). Cache sync will run gracefully in offline fallback mode.");
        }

        services.AddScoped<IVendorCacheSyncService, VendorMaster.Infrastructure.Services.VendorCacheSyncService>();

        return services;
    }

    public static IApplicationBuilder UseVendorMasterModule(this IApplicationBuilder app)
    {
        return app;
    }

    // alias buat jaga kompatibilitas nama lama
    public static IServiceCollection AddGeofenceMasterModule(this IServiceCollection services, IConfiguration configuration)
        => services.AddVendorMasterModule(configuration);

    public static IApplicationBuilder UseGeofenceMasterModule(this IApplicationBuilder app)
        => app.UseVendorMasterModule();
}
