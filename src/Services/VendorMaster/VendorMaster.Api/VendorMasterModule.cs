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
