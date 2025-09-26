using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Data.Cache;
using StackExchange.Redis;
using VendorMaster.Application.Ports;
using VendorMaster.Infrastructure.Persistence;

namespace VendorMaster.Infrastructure.Services;

// Service sinkronisasi cache Redis untuk konfigurasi vendor aktif
public class VendorCacheSyncService(
    VendorMasterDbContext dbContext,
    IConnectionMultiplexer? redis,
    ILogger<VendorCacheSyncService> logger) : IVendorCacheSyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task RefreshActiveVendorsCacheAsync(CancellationToken cancellationToken = default)
    {
        if (redis == null || !redis.IsConnected)
        {
            logger.LogWarning("Redis tidak terhubung. Melewati refresh cache vendor aktif ke Redis.");
            return;
        }

        try
        {
            var db = redis.GetDatabase();

            // 1. Hapus kunci cache lama
            await db.KeyDeleteAsync(VendorCacheConstants.ActiveVendorsCacheKey);

            // 2. Ambil seluruh data vendor aktif dari PostgreSQL
            var vendors = await dbContext.GpsVendors
                .AsNoTracking()
                .Where(v => v.IsActive && !v.IsDeleted)
                .Include(v => v.GpsVendorEndpoints)
                .Include(v => v.GpsVendorAuth)
                .Include(v => v.Mappings)
                .Include(v => v.Lpcds)
                .OrderBy(v => v.VendorCode)
                .ToListAsync(cancellationToken);

            var cacheDtos = vendors.Select(v =>
            {
                var auth = v.GpsVendorAuth;
                var primaryLpcd = v.Lpcds.FirstOrDefault()?.Lpcd;

                return new ActiveVendorCacheDto
                {
                    Id = v.Id,
                    VendorCode = v.VendorCode,
                    VendorName = v.VendorName,
                    Lpcd = primaryLpcd,
                    RequiredAuth = auth != null,
                    AuthType = auth?.AuthType,
                    Username = auth?.Username,
                    Password = auth?.Password,
                    ProcessingStrategy = v.ProcessingStrategy,
                    ProcessingStrategyPathKey = v.ProcessingStrategyPathKey,
                    ProcessingStrategyPathData = v.ProcessingStrategyPathData ?? "data",
                    Auth = auth == null ? null : new ActiveVendorAuthCacheDto
                    {
                        Id = auth.Id,
                        GpsVendorId = v.Id,
                        BaseUrl = auth.BaseUrl,
                        Method = auth.Method,
                        AuthType = auth.AuthType,
                        ContentType = auth.ContentType,
                        Username = auth.Username,
                        Password = auth.Password,
                        HeadersJson = auth.Headers?.ToJsonString(),
                        ParamsJson = auth.Params?.ToJsonString(),
                        BodiesJson = auth.Bodies?.ToJsonString(),
                        TokenPath = auth.TokenPath
                    },
                    Endpoints = v.GpsVendorEndpoints.Select(e => new ActiveVendorEndpointCacheDto
                    {
                        Id = e.Id,
                        GpsVendorId = v.Id,
                        EndpointName = e.EndpointType,
                        BaseUrl = e.BaseUrl,
                        Method = e.Method,
                        HeadersJson = e.Headers?.ToJsonString(),
                        ParamsJson = e.Params?.ToJsonString(),
                        BodiesJson = e.Bodies?.ToJsonString(),
                        VarParamsJson = e.VarParams?.ToJsonString(),
                        MaxPath = e.MaxPath
                    }).ToList(),
                    Mappings = v.Mappings.Select(m => new ActiveVendorMappingCacheDto
                    {
                        GpsVendorId = v.Id,
                        ResponseField = m.ResponseField,
                        MappedField = m.MappedField
                    }).ToList()
                };
            }).ToList();

            // 3. Simpan ke Redis selamanya (tanpa expiry / infinite TTL)
            var json = JsonSerializer.Serialize(cacheDtos, JsonOptions);
            await db.StringSetAsync(VendorCacheConstants.ActiveVendorsCacheKey, json, expiry: null);

            logger.LogInformation("Berhasil menyinkronkan {Count} vendor aktif ke Redis cache (Key: '{Key}') selamanya.", 
                cacheDtos.Count, VendorCacheConstants.ActiveVendorsCacheKey);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gagal menyinkronkan data vendor aktif ke Redis cache.");
        }
    }

    public async Task InvalidateActiveVendorsCacheAsync(CancellationToken cancellationToken = default)
    {
        if (redis == null || !redis.IsConnected)
        {
            return;
        }

        try
        {
            var db = redis.GetDatabase();
            await db.KeyDeleteAsync(VendorCacheConstants.ActiveVendorsCacheKey);
            logger.LogInformation("Kunci cache '{Key}' berhasil dihapus dari Redis.", VendorCacheConstants.ActiveVendorsCacheKey);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gagal menghapus kunci cache '{Key}' dari Redis.", VendorCacheConstants.ActiveVendorsCacheKey);
        }
    }
}
