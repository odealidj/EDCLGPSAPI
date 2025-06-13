using Microsoft.Extensions.Logging;
using Ping.Data;

namespace Ping;

public static class PingModule
{
    public static IServiceCollection AddPingModule(this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");

        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        services.AddDbContext<PingDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseNpgsql(connectionString)
                .EnableSensitiveDataLogging()
                .LogTo(Console.WriteLine, LogLevel.Warning);
        },ServiceLifetime.Scoped);
        return services;
    }

    public static IApplicationBuilder UsePingModule(this IApplicationBuilder app)
    {
        return app;
    }
}