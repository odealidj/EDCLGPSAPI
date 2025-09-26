using DeliveryTracking.Application.Ports;
using DeliveryTracking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shared.Data.Mongo;

namespace DeliveryTracking;

// modul konfigurasi dependensi layanan delivery tracking (arsitektur hexagonal)
public static class DeliveryModule
{
    public static IServiceCollection AddDeliveryModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? configuration["Database"]
            ?? "Server=localhost;Port=5436;Database=AE031_EDCL_GPS_DB;User Id=postgres;Password=postgres;Include Error Detail=true;Timezone=Asia/Jakarta;";

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

        // 3.1 registrasi multiplexer redis untuk in-memory O(1) active delivery hash lookup
        services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(_ =>
        {
            var config = StackExchange.Redis.ConfigurationOptions.Parse(redisConnectionString);
            config.AbortOnConnectFail = false;
            config.ConnectRetry = 3;
            config.ConnectTimeout = 5000;
            return StackExchange.Redis.ConnectionMultiplexer.Connect(config);
        });

        // 4. registrasi layanan kueri posisi armada (redis-first + db fallback)
        services.AddScoped<IPositionQueryService, DeliveryTracking.Infrastructure.Services.PositionQueryService>();

        // 5. registrasi mongodb logging & raw response repository
        services.AddMongoDbLogs(configuration);

        return services;
    }

    public static IApplicationBuilder UseDeliveryModule(this IApplicationBuilder app)
    {
        return app;
    }
}