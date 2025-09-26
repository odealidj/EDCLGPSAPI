using System.Text.Json;
using GpsCollector.Application.DTOs;
using GpsCollector.Application.Ports;
using GpsCollector.Domain.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GpsCollector.Infrastructure.Adapters.Cache;

// Adapter cache snapshot posisi terkini armada ke Redis (AOT Ready System.Text.Json)
public sealed class RedisTelemetryCache : ITelemetryCache
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly IDistributedCache? _cache;
    private readonly ILogger<RedisTelemetryCache> _logger;
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
    };

    public RedisTelemetryCache(
        ILogger<RedisTelemetryCache> logger,
        IConnectionMultiplexer? redis = null,
        IDistributedCache? cache = null)
    {
        _logger = logger;
        _redis = redis;
        _cache = cache;
    }

    public async Task UpdateDeliveryLivePositionAsync(
        string deliveryNo,
        TelemetryPoint point,
        string trackingStatus,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var dto = new DeliveryLivePositionCacheDto(
            DeliveryNo: deliveryNo,
            TrackingStatus: trackingStatus,
            PlateNumber: point.PlatNo,
            DeviceId: point.DeviceId,
            Latitude: point.Y,
            Longitude: point.X,
            Speed: point.Speed,
            Course: point.Course,
            StreetName: point.StreetName,
            Datetime: point.Datetime == default ? now : point.Datetime,
            UpdatedAt: now
        );

        var payload = JsonSerializer.Serialize(dto, GpsCollectorJsonContext.Default.DeliveryLivePositionCacheDto);
        await SetRedisKeyAsync($"delivery:live:{deliveryNo}", payload, ct);
    }

    public async Task UpdateDeliveryLiveStatusAsync(
        string deliveryNo,
        string trackingStatus,
        string? errorMessage,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var dto = new DeliveryLiveStatusCacheDto(
            DeliveryNo: deliveryNo,
            TrackingStatus: trackingStatus,
            ErrorMessage: errorMessage,
            UpdatedAt: now
        );

        var payload = JsonSerializer.Serialize(dto, GpsCollectorJsonContext.Default.DeliveryLiveStatusCacheDto);
        await SetRedisKeyAsync($"delivery:live:{deliveryNo}", payload, ct);
    }

    public async Task SetLastPositionsAsync(string vendorName, IReadOnlyList<TelemetryPoint> points, CancellationToken ct)
    {
        if (points.Count == 0) return;

        var cachedAt = DateTime.UtcNow;

        foreach (var p in points)
        {
            if (string.IsNullOrWhiteSpace(p.PlatNo)) continue;

            var normalizedPlat = p.PlatNo.Trim().ToUpperInvariant();
            var cacheKey = $"gps:last:{normalizedPlat}";

            var dto = new GpsCachePayloadDto(
                Id: p.Id,
                HeaderId: p.HeaderId,
                Lpcd: p.Lpcd,
                PlatNo: normalizedPlat,
                DeviceId: p.DeviceId,
                Datetime: p.Datetime,
                X: p.X,
                Y: p.Y,
                Speed: p.Speed,
                Course: p.Course,
                StreetName: p.StreetName,
                CachedAt: cachedAt
            );

            try
            {
                var json = JsonSerializer.Serialize(dto, GpsCollectorJsonContext.Default.GpsCachePayloadDto);
                await SetRedisKeyAsync(cacheKey, json, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache position in Redis for plate {PlatNo}", normalizedPlat);
            }
        }
    }

    private async Task SetRedisKeyAsync(string key, string value, CancellationToken ct)
    {
        try
        {
            if (_redis != null)
            {
                var db = _redis.GetDatabase();
                await db.StringSetAsync(key, value, TimeSpan.FromHours(24));
            }
            else if (_cache != null)
            {
                await _cache.SetStringAsync(key, value, CacheOptions, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set Redis key {Key}", key);
        }
    }
}
