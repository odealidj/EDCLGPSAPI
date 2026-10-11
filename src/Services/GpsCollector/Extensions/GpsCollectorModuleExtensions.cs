using GpsCollector.Application.Ports;
using GpsCollector.Application.Services;
using GpsCollector.Infrastructure.Adapters.Alerting;
using GpsCollector.Infrastructure.Adapters.Archive;
using GpsCollector.Infrastructure.Adapters.Cache;
using GpsCollector.Infrastructure.Adapters.Health;
using GpsCollector.Infrastructure.Adapters.Http;
using GpsCollector.Infrastructure.Adapters.Messaging;
using GpsCollector.Infrastructure.Adapters.Normalization;
using GpsCollector.Infrastructure.Adapters.Persistence;
using GpsCollector.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Shared.Data.Mongo;
using Shared.Infrastructure.LeaderElection;
using StackExchange.Redis;

namespace GpsCollector.Extensions;

public static class GpsCollectorModuleExtensions
{
    public static IServiceCollection AddGpsCollectorModule(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. HttpClient Factory dengan Anti-Bot User-Agent & Headers agar tidak di-blacklist
        services.AddHttpClient("VendorHttpClient", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(50);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("id-ID,id;q=0.9,en-US;q=0.8,en;q=0.7");
            client.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua", "\"Chromium\";v=\"128\", \"Not;A=Brand\";v=\"24\", \"Google Chrome\";v=\"128\"");
            client.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
            client.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
        });

        services.AddHttpClient("VendorAuthClient", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("id-ID,id;q=0.9,en-US;q=0.8,en;q=0.7");
        });

        // 2. PostgreSQL NpgsqlDataSource (Pure ADO.NET)
        var connectionString = configuration.GetConnectionString("Database")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? string.Empty;

        services.AddSingleton<NpgsqlDataSource>(sp =>
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            return dataSourceBuilder.Build();
        });

        // 3. Redis Caching & Leader Election
        var redisConn = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConn))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConn;
                options.InstanceName = "edcl:";
            });

            try
            {
                var multiplexer = ConnectionMultiplexer.Connect(redisConn);
                services.AddSingleton<IConnectionMultiplexer>(multiplexer);
            }
            catch
            {
                // Fallback jika Redis offline saat startup
            }
        }

        services.AddSingleton<ILeaderElection>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RedisLeaderElection>>();
            var redis = sp.GetService<IConnectionMultiplexer>();
            return new RedisLeaderElection(logger, redis);
        });

        // 4. MongoDB Logs (Raw Telemetry Archive)
        services.AddMongoDbLogs(configuration);

        // 5. Ports & Adapters Registration (Hexagonal Architecture)
        services.AddScoped<IVendorConfigRepository, PostgresVendorConfigRepository>();
        services.AddScoped<IDeliverySessionRepository, PostgresDeliverySessionRepository>();
        services.AddScoped<ITelemetryIngestionRepository, PostgresTelemetryIngestionRepository>();
        services.AddScoped<IGpsApiLogRepository, PostgresGpsApiLogRepository>();

        services.AddSingleton<ITelemetryNormalizer, VendorTelemetryNormalizer>();
        services.AddSingleton<IVendorHttpFetcher, VendorHttpFetcher>();

        services.AddScoped<ITelemetryCache, RedisTelemetryCache>();
        services.AddScoped<ITelemetryArchiver, MongoTelemetryArchiver>();
        services.AddScoped<ITelemetryEventPublisher, RabbitMqTelemetryPublisher>();

        services.AddScoped<IVendorAlertNotifier, DatabaseVendorAlertNotifier>();

        var alertThreshold = int.TryParse(configuration["Alerts:MaxConsecutiveFailures"], out var threshold) ? threshold : 3;
        services.AddSingleton<IVendorHealthTracker>(sp =>
            new VendorHealthTracker(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<ILogger<VendorHealthTracker>>(),
                alertThreshold
            ));

        services.AddScoped<IGpsCollectorOrchestrator, GpsCollectorOrchestrator>();

        // 6. Background Worker Service
        var isWorkerEnabled = !bool.TryParse(configuration["Worker:Enabled"], out var enabled) || enabled;
        if (isWorkerEnabled)
        {
            services.AddHostedService<GpsCollectorWorker>();
        }

        return services;
    }
}
