using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Caching.Distributed;
using Npgsql;
using PositionQuery.Api.Models;

namespace PositionQuery.Api.Services;

public class PositionQueryService(
    IConfiguration configuration,
    IDistributedCache cache,
    ILogger<PositionQueryService> logger) : IPositionQueryService
{
    private readonly string _connectionString = configuration.GetConnectionString("Database")
        ?? throw new InvalidOperationException("ConnectionStrings:Database is not configured.");

    public async Task<LastPositionDto?> GetLastPositionByPlatNoAsync(string platNo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(platNo)) return null;

        var normalizedPlatNo = platNo.Trim().ToUpperInvariant();
        var cacheKey = $"gps:last:{normalizedPlatNo}";

        // 1. Redis-First check (latency < 1ms)
        try
        {
            var cachedJson = await cache.GetStringAsync(cacheKey, cancellationToken);
            if (!string.IsNullOrEmpty(cachedJson))
            {
                var cached = JsonSerializer.Deserialize<JsonElement>(cachedJson);
                logger.LogDebug("Cache HIT for plate number: {PlatNo}", normalizedPlatNo);
                return MapFromJsonElement(cached, "Redis");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis lookup failed for plate number: {PlatNo}. Falling back to PostgreSQL.", normalizedPlatNo);
        }

        // 2. Database Fallback (PostgreSQL Partitioned Tables via Stored Function)
        logger.LogDebug("Cache MISS for plate number: {PlatNo}. Querying PostgreSQL.", normalizedPlatNo);
        var dbResult = await QueryDatabasePositionsAsync(new LastPositionRequest(PlatNo: normalizedPlatNo, PageIndex: 0, PageSize: 1), cancellationToken);
        var position = dbResult.Data.FirstOrDefault();
        if (position != null)
        {
            position.DataSource = "PostgreSQL";
        }
        return position;
    }

    public async Task<PaginatedPositionResponse> QueryPositionsAsync(LastPositionRequest request, CancellationToken cancellationToken = default)
    {
        // If query is specifically looking up a single plate with no temporal filters, try Redis first
        if (!string.IsNullOrWhiteSpace(request.PlatNo) && request.Datetime == null && request.CreatedAt == null && request.GpsVendorId == null)
        {
            var redisHit = await GetLastPositionByPlatNoAsync(request.PlatNo, cancellationToken);
            if (redisHit != null)
            {
                return new PaginatedPositionResponse(
                    PageIndex: 1,
                    PageSize: request.PageSize,
                    Count: 1,
                    Data: [redisHit]
                );
            }
        }

        return await QueryDatabasePositionsAsync(request, cancellationToken);
    }

    private async Task<PaginatedPositionResponse> QueryDatabasePositionsAsync(LastPositionRequest param, CancellationToken cancellationToken)
    {
        try
        {
            var parameters = new DynamicParameters();
            parameters.Add("p_gps_vendor_id", param.GpsVendorId == null || param.GpsVendorId == Guid.Empty ? null : param.GpsVendorId);
            parameters.Add("p_vendor_name", string.IsNullOrEmpty(param.VendorName) ? null : param.VendorName.ToLower());
            parameters.Add("p_plat_no", string.IsNullOrEmpty(param.PlatNo) ? null : param.PlatNo.ToLower());
            parameters.Add("p_device_id", string.IsNullOrEmpty(param.DeviceId) ? null : param.DeviceId.ToLower());
            parameters.Add("p_datetime", param.Datetime);
            parameters.Add("p_created_at", param.CreatedAt?.Date);
            parameters.Add("p_page_index", param.PageIndex <= 0 ? 0 : param.PageIndex - 1);
            parameters.Add("p_page_size", param.PageSize <= 0 ? 100 : param.PageSize);

            var sqlData = "SELECT * FROM edcl.sp_edclgps1_get_last_positions(@p_gps_vendor_id, @p_vendor_name, @p_plat_no, @p_device_id, @p_datetime, @p_created_at, @p_page_index, @p_page_size)";
            var sqlCount = "SELECT * FROM edcl.sp_edclgps1_get_last_positions_count(@p_gps_vendor_id, @p_vendor_name, @p_plat_no, @p_device_id, @p_datetime, @p_created_at)";

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var positions = (await connection.QueryAsync<LastPositionDto>(
                sqlData,
                parameters,
                commandType: CommandType.Text,
                commandTimeout: 60)).ToList();

            foreach (var pos in positions)
            {
                pos.DataSource = "PostgreSQL";
            }

            var totalCount = await connection.ExecuteScalarAsync<long>(
                sqlCount,
                parameters,
                commandType: CommandType.Text,
                commandTimeout: 60);

            return new PaginatedPositionResponse(
                PageIndex: param.PageIndex <= 0 ? 1 : param.PageIndex,
                PageSize: param.PageSize,
                Count: totalCount,
                Data: positions
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to query GPS positions from PostgreSQL");
            throw;
        }
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
