using System.Text.Json;
using GpsCollector.Application.DTOs;
using GpsCollector.Application.Ports;
using GpsCollector.Domain.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace GpsCollector.Infrastructure.Adapters.Cache;

// Adapter cache snapshot posisi terkini armada ke Redis (AOT Ready System.Text.Json)
public sealed class RedisTelemetryCache : ITelemetryCache
{
    private readonly IDistributedCache? _cache;
    private readonly ILogger<RedisTelemetryCache> _logger;
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
    };

    public RedisTelemetryCache(ILogger<RedisTelemetryCache> logger, IDistributedCache? cache = null)
    {
        _logger = logger;
        _cache = cache;
    }

    public async Task SetLastPositionsAsync(string vendorName, IReadOnlyList<TelemetryPoint> points, CancellationToken ct)
    {
        if (_cache == null || points.Count == 0) return;

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
                await _cache.SetStringAsync(cacheKey, json, CacheOptions, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache position in Redis for plate {PlatNo}", normalizedPlat);
            }
        }
    }
}
