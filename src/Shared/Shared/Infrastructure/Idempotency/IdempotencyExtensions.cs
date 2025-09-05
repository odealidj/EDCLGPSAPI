using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Shared.Infrastructure.Idempotency;

public static class IdempotencyExtensions
{
    public static IServiceCollection AddEdclIdempotency(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisConn = configuration.GetConnectionString("Redis") 
                        ?? configuration["Redis"]
                        ?? "localhost:6379";

        services.TryAddSingleton<IConnectionMultiplexer>(sp =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("RedisConnection");
            try
            {
                var multiplexer = ConnectionMultiplexer.Connect(redisConn);
                return multiplexer;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[Idempotency] Could not connect to Redis at {Endpoint}. In-memory fallback will be active.", redisConn);
                return null!;
            }
        });

        services.TryAddSingleton<IIdempotencyStore, RedisIdempotencyStore>();
        return services;
    }

    public static RouteHandlerBuilder RequireIdempotency(
        this RouteHandlerBuilder builder,
        TimeSpan? completedTtl = null)
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            var store = context.HttpContext.RequestServices.GetRequiredService<IIdempotencyStore>();
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<IdempotencyEndpointFilter>>();
            var filter = new IdempotencyEndpointFilter(store, logger, completedTtl);
            return await filter.InvokeAsync(context, next);
        });

        return builder;
    }
}
