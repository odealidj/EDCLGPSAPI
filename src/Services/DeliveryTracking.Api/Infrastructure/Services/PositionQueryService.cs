using System.Text.Json;
using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Application.Ports;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace DeliveryTracking.Infrastructure.Services;

// implementasi layanan kueri posisi armada dengan strategi redis-first + postgresql ado.net fallback
public class PositionQueryService(
    IDistributedCache cache,
    IDeliveryTrackingRepository repository,
    ILogger<PositionQueryService> logger) : IPositionQueryService
{
    public async Task<LastPositionDto?> GetLastPositionByPlatNoAsync(
        string platNo,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(platNo)) return null;

        var normalizedPlatNo = platNo.Trim().ToUpperInvariant();
        var cacheKey = $"gps:last:{normalizedPlatNo}";

        // 1. cek redis cache terlebih dahulu (latensi < 1ms)
        try
        {
            var cachedJson = await cache.GetStringAsync(cacheKey, cancellationToken);
            if (!string.IsNullOrEmpty(cachedJson))
            {
                var cached = JsonSerializer.Deserialize<JsonElement>(cachedJson);
                logger.LogDebug("Cache HIT Redis untuk plat nomor: {PlatNo}", normalizedPlatNo);
                return MapFromJsonElement(cached, "Redis");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis lookup gagal untuk plat nomor: {PlatNo}. Fallback ke PostgreSQL.", normalizedPlatNo);
        }

        // 2. fallback ke database postgresql (ado.net via stored procedure)
        logger.LogDebug("Cache MISS Redis untuk plat nomor: {PlatNo}. Mengueri PostgreSQL.", normalizedPlatNo);
        var position = await repository.GetLastPositionByPlatNoFromDbAsync(normalizedPlatNo, cancellationToken);
        if (position != null)
        {
            position.DataSource = "PostgreSQL";
        }

        return position;
    }

    public async Task<PaginatedPositionResponse> QueryPositionsAsync(
        LastPositionRequest request,
        CancellationToken cancellationToken = default)
    {
        // jika kueri spesifik satu plat nomor tanpa filter tanggal/vendor, cek redis dulu
        if (!string.IsNullOrWhiteSpace(request.PlatNo) &&
            request.Datetime == null &&
            request.CreatedAt == null &&
            request.GpsVendorId == null)
        {
            var redisHit = await GetLastPositionByPlatNoAsync(request.PlatNo, cancellationToken);
            if (redisHit != null)
            {
                return new PaginatedPositionResponse(
                    PageIndex: 1,
                    PageSize: request.PageSize <= 0 ? 100 : request.PageSize,
                    Count: 1,
                    Data: [redisHit]
                );
            }
        }

        return await repository.QueryPositionsFromDbAsync(request, cancellationToken);
    }

    private static LastPositionDto MapFromJsonElement(JsonElement json, string source)
    {
        return new LastPositionDto
        {
            Id = json.TryGetProperty("Id", out var id) && id.TryGetGuid(out var g) ? g : Guid.NewGuid(),
            PlatNo = json.TryGetProperty("PlatNo", out var plat) ? plat.GetString() : null,
            DeviceId = json.TryGetProperty("DeviceId", out var dev) ? dev.GetString() : null,
            Datetime = json.TryGetProperty("Datetime", out var dt) && dt.TryGetDateTime(out var d) ? d : DateTime.UtcNow,
            X = json.TryGetProperty("X", out var x) && x.TryGetDecimal(out var decX) ? decX : null,
            Y = json.TryGetProperty("Y", out var y) && y.TryGetDecimal(out var decY) ? decY : null,
            Speed = json.TryGetProperty("Speed", out var sp) && sp.TryGetDecimal(out var decSp) ? decSp : null,
            Course = json.TryGetProperty("Course", out var cs) && cs.TryGetDecimal(out var decCs) ? decCs : null,
            StreetName = json.TryGetProperty("StreetName", out var st) ? st.GetString() : null,
            CreatedAt = json.TryGetProperty("CachedAt", out var cat) && cat.TryGetDateTime(out var cdt) ? cdt : DateTime.UtcNow,
            DataSource = source
        };
    }
}
