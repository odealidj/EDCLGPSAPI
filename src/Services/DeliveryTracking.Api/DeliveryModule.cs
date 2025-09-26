using DeliveryTracking.Application.Ports;
using DeliveryTracking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace DeliveryTracking;

// modul konfigurasi dependensi layanan delivery tracking (arsitektur hexagonal)
public static class DeliveryModule
{
    public static IServiceCollection AddDeliveryModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Connection string 'Database' tidak ditemukan.");

        // 1. registrasi npgsql data source (ado.net connection pool bawaan .net 10)
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        var dataSource = dataSourceBuilder.Build();
        services.AddSingleton(dataSource);

        // 2. registrasi repository port -> driven adapter ado.net
        services.AddScoped<IDeliveryTrackingRepository, DeliveryTrackingAdoRepository>();

        // 3. registrasi redis distributed cache untuk high-speed position lookup
        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? configuration["Redis"]
            ?? "localhost:6379";
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "";
        });

        // 4. registrasi layanan kueri posisi armada (redis-first + db fallback)
        services.AddScoped<IPositionQueryService, DeliveryTracking.Infrastructure.Services.PositionQueryService>();

        return services;
    }

    public static IApplicationBuilder UseDeliveryModule(this IApplicationBuilder app)
    {
        return app;
    }
}