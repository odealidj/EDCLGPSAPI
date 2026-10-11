using System.Data;
using System.Text.Json;
using GpsCollector.Application.DTOs;
using GpsCollector.Application.Ports;
using GpsCollector.Domain.Models;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Shared.Data.Cache;
using StackExchange.Redis;

namespace GpsCollector.Infrastructure.Adapters.Persistence;

// Repositori konfigurasi vendor dengan strategi Redis-First (Persistent Infinite TTL) & PostgreSQL Fallback (AOT Ready)
public sealed class PostgresVendorConfigRepository : IVendorConfigRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<PostgresVendorConfigRepository> _logger;

    public PostgresVendorConfigRepository(
        NpgsqlDataSource dataSource,
        ILogger<PostgresVendorConfigRepository> logger,
        IConnectionMultiplexer? redis = null)
    {
        _dataSource = dataSource;
        _logger = logger;
        _redis = redis;
    }

    public async Task<List<VendorConfig>> GetActiveVendorsAsync(CancellationToken ct)
    {
        // 1. Redis-First Strategy: Coba ambil dari Redis cache persisten (disimpan selamanya)
        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var db = _redis.GetDatabase();
                var cachedValue = await db.StringGetAsync(VendorCacheConstants.ActiveVendorsCacheKey);

                if (cachedValue.HasValue && !cachedValue.IsNullOrEmpty)
                {
                    var cachedDtos = JsonSerializer.Deserialize<List<ActiveVendorCacheDto>>(
                        cachedValue.ToString(),
                        GpsCollectorJsonContext.Default.ListActiveVendorCacheDto);

                    if (cachedDtos is { Count: > 0 })
                    {
                        _logger.LogInformation("Loaded {Count} active vendor configurations from Redis cache (key: '{Key}').",
                            cachedDtos.Count, VendorCacheConstants.ActiveVendorsCacheKey);

                        return cachedDtos.Select(MapFromCacheDto).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read active vendors from Redis cache. Falling back to PostgreSQL query...");
            }
        }

        // 2. Fallback ke PostgreSQL jika cache di Redis kosong atau belum ada
        _logger.LogInformation("Cache miss for active vendors. Fetching active vendor configurations directly from PostgreSQL...");
        var vendors = await FetchActiveVendorsFromPostgresAsync(ct);

        // 3. Simpan hasil query ke Redis selamanya (Rehydration)
        if (vendors.Count > 0 && _redis != null && _redis.IsConnected)
        {
            try
            {
                var cacheDtos = vendors.Select(MapToCacheDto).ToList();
                var json = JsonSerializer.Serialize(cacheDtos, GpsCollectorJsonContext.Default.ListActiveVendorCacheDto);
                var db = _redis.GetDatabase();

                // Simpan selamanya (expiry: null)
                await db.StringSetAsync(VendorCacheConstants.ActiveVendorsCacheKey, json, expiry: null);
                _logger.LogInformation("Successfully cached {Count} active vendor configurations to Redis indefinitely (Key: '{Key}').",
                    cacheDtos.Count, VendorCacheConstants.ActiveVendorsCacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache active vendors to Redis.");
            }
        }

        return vendors;
    }

    private async Task<List<VendorConfig>> FetchActiveVendorsFromPostgresAsync(CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);

        var vendorDict = new Dictionary<Guid, VendorConfigBuilder>();
        await PopulateVendorsAsync(conn, vendorDict, ct);

        if (vendorDict.Count == 0) return [];

        await PopulateEndpointsAsync(conn, vendorDict, ct);
        await PopulateAuthsAsync(conn, vendorDict, ct);
        await PopulateMappingsAsync(conn, vendorDict, ct);

        return vendorDict.Values.Select(b => b.Build()).ToList();
    }

    private static async Task PopulateVendorsAsync(
        NpgsqlConnection conn,
        Dictionary<Guid, VendorConfigBuilder> vendorDict,
        CancellationToken ct)
    {
        const string sqlVendors = @"
            SELECT v.id, v.vendor_name, v.vendor_code, vl.lpcd,
                   v.processing_strategy, v.processing_strategy_path_key, v.processing_strategy_path_data
            FROM edcl.m_gps_vendor v
            LEFT JOIN edcl.m_gps_vendor_lpcd vl ON v.id = vl.vendor_id
            WHERE v.is_active = true AND v.is_deleted = false;";

        await using var cmd = new NpgsqlCommand(sqlVendors, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            ReadVendorRow(reader, vendorDict);
        }
    }

    private static void ReadVendorRow(NpgsqlDataReader reader, Dictionary<Guid, VendorConfigBuilder> vendorDict)
    {
        var id = reader.GetGuid(0);
        if (!vendorDict.TryGetValue(id, out var builder))
        {
            builder = new VendorConfigBuilder
            {
                Id = id,
                VendorName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                VendorCode = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Lpcd = reader.IsDBNull(3) ? null : reader.GetString(3),
                ProcessingStrategy = reader.IsDBNull(4) ? "individual" : reader.GetString(4),
                ProcessingStrategyPathKey = reader.IsDBNull(5) ? null : reader.GetString(5),
                ProcessingStrategyPathData = reader.IsDBNull(6) ? "data" : reader.GetString(6),
            };
            vendorDict[id] = builder;
            return;
        }

        if (string.IsNullOrEmpty(builder.Lpcd) && !reader.IsDBNull(3))
        {
            builder.Lpcd = reader.GetString(3);
        }
    }

    private static async Task PopulateEndpointsAsync(
        NpgsqlConnection conn,
        Dictionary<Guid, VendorConfigBuilder> vendorDict,
        CancellationToken ct)
    {
        const string sqlEndpoints = @"
            SELECT id, vendor_id, endpoint_type, base_url, method, 
                   headers::text, params::text, bodies::text, var_params::text, max_path
            FROM edcl.m_gps_vendor_endpoint;";

        await using var cmd = new NpgsqlCommand(sqlEndpoints, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            ReadEndpointRow(reader, vendorDict);
        }
    }

    private static void ReadEndpointRow(NpgsqlDataReader reader, Dictionary<Guid, VendorConfigBuilder> vendorDict)
    {
        var vendorId = reader.GetGuid(1);
        if (!vendorDict.TryGetValue(vendorId, out var builder)) return;

        builder.Endpoints.Add(new VendorEndpointConfig
        {
            Id = reader.GetGuid(0),
            GpsVendorId = vendorId,
            EndpointName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            BaseUrl = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            Method = reader.IsDBNull(4) ? "GET" : reader.GetString(4),
            HeadersJson = reader.IsDBNull(5) ? null : reader.GetString(5),
            ParamsJson = reader.IsDBNull(6) ? null : reader.GetString(6),
            BodiesJson = reader.IsDBNull(7) ? null : reader.GetString(7),
            VarParamsJson = reader.IsDBNull(8) ? null : reader.GetString(8),
            MaxPath = reader.IsDBNull(9) ? null : reader.GetString(9),
        });
    }

    private static async Task PopulateAuthsAsync(
        NpgsqlConnection conn,
        Dictionary<Guid, VendorConfigBuilder> vendorDict,
        CancellationToken ct)
    {
        const string sqlAuth = @"
            SELECT id, vendor_id, base_url, method, auth_type, content_type, 
                   username, password, headers::text, params::text, bodies::text, token_path
            FROM edcl.m_gps_vendor_auth;";

        await using var cmd = new NpgsqlCommand(sqlAuth, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            ReadAuthRow(reader, vendorDict);
        }
    }

    private static void ReadAuthRow(NpgsqlDataReader reader, Dictionary<Guid, VendorConfigBuilder> vendorDict)
    {
        var vendorId = reader.GetGuid(1);
        if (!vendorDict.TryGetValue(vendorId, out var builder)) return;

        var auth = new VendorAuthConfig
        {
            Id = reader.GetGuid(0),
            GpsVendorId = vendorId,
            BaseUrl = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            Method = reader.IsDBNull(3) ? "POST" : reader.GetString(3),
            AuthType = reader.IsDBNull(4) ? "Bearer" : reader.GetString(4),
            ContentType = reader.IsDBNull(5) ? "application/json" : reader.GetString(5),
            Username = reader.IsDBNull(6) ? null : reader.GetString(6),
            Password = reader.IsDBNull(7) ? null : reader.GetString(7),
            HeadersJson = reader.IsDBNull(8) ? null : reader.GetString(8),
            ParamsJson = reader.IsDBNull(9) ? null : reader.GetString(9),
            BodiesJson = reader.IsDBNull(10) ? null : reader.GetString(10),
            TokenPath = reader.IsDBNull(11) ? null : reader.GetString(11),
        };

        builder.Auth = auth;
        builder.RequiredAuth = true;
        builder.AuthType = auth.AuthType;
        builder.Username = auth.Username;
        builder.Password = auth.Password;
    }

    private static async Task PopulateMappingsAsync(
        NpgsqlConnection conn,
        Dictionary<Guid, VendorConfigBuilder> vendorDict,
        CancellationToken ct)
    {
        const string sqlMappings = @"
            SELECT vendor_id, response_field, mapped_field
            FROM edcl.m_gps_vendor_mapping;";

        await using var cmd = new NpgsqlCommand(sqlMappings, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            ReadMappingRow(reader, vendorDict);
        }
    }

    private static void ReadMappingRow(NpgsqlDataReader reader, Dictionary<Guid, VendorConfigBuilder> vendorDict)
    {
        var vendorId = reader.GetGuid(0);
        if (!vendorDict.TryGetValue(vendorId, out var builder)) return;

        builder.Mappings.Add(new VendorFieldMapping
        {
            GpsVendorId = vendorId,
            ResponseField = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            MappedField = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
        });
    }

    public async Task<VendorConfig?> GetVendorByIdAsync(Guid vendorId, CancellationToken ct)
    {
        var vendors = await GetActiveVendorsAsync(ct);
        return vendors.FirstOrDefault(v => v.Id == vendorId);
    }

    public async Task<int> GetPollingDelayMinutesAsync(CancellationToken ct)
    {
        const string sql = @"
            SELECT sys_value FROM edcl.m_system
            WHERE (sys_cat = 'Delay' AND sys_sub_cat = 'EDCL_GPS_WORKER_SERVICE' AND sys_cd = '1')
               OR (sys_cat = 'WORKER' AND sys_sub_cat = 'INTERVAL' AND sys_cd = 'POLLING_SEC')
            ORDER BY created_at DESC
            LIMIT 1;";

        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            var result = await cmd.ExecuteScalarAsync(ct);

            if (result != null && int.TryParse(result.ToString(), out var val) && val > 0)
            {
                // Jika nilai dalam detik (> 5), konversi ke menit (minimal 1 menit)
                return val >= 60 ? (int)Math.Ceiling(val / 60.0) : 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read delay config from edcl.m_system, falling back to 1 minute");
        }

        return 1;
    }

    public async Task UpdateVarParamsMaxIdAsync(Guid endpointId, string propertyName, int maxId, CancellationToken ct)
    {
        const string sql = @"
            UPDATE edcl.m_gps_vendor_endpoint
            SET var_params = jsonb_set(COALESCE(var_params, '{}'::jsonb), @path, @val::jsonb),
                last_modified = @modifiedAt,
                last_modified_by = 'System'
            WHERE id = @endpointId;";

        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);

            cmd.Parameters.Add(new NpgsqlParameter("endpointId", NpgsqlDbType.Uuid) { Value = endpointId });
            cmd.Parameters.AddWithValue("path", new[] { propertyName });
            cmd.Parameters.Add(new NpgsqlParameter("val", NpgsqlDbType.Jsonb) { Value = maxId.ToString() });
            cmd.Parameters.Add(new NpgsqlParameter("modifiedAt", NpgsqlDbType.Timestamp) { Value = DateTime.UtcNow });

            await cmd.ExecuteNonQueryAsync(ct);

            // Invalidate Redis cache agar polling berikutnya mengambil var_params terbaru
            if (_redis != null && _redis.IsConnected)
            {
                var db = _redis.GetDatabase();
                await db.KeyDeleteAsync(VendorCacheConstants.ActiveVendorsCacheKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update VarParams for endpoint {EndpointId}", endpointId);
        }
    }

    private static VendorConfig MapFromCacheDto(ActiveVendorCacheDto dto) => new()
    {
        Id = dto.Id,
        VendorCode = dto.VendorCode,
        VendorName = dto.VendorName,
        Lpcd = dto.Lpcd,
        RequiredAuth = dto.RequiredAuth,
        AuthType = dto.AuthType,
        Username = dto.Username,
        Password = dto.Password,
        ProcessingStrategy = dto.ProcessingStrategy,
        ProcessingStrategyPathKey = dto.ProcessingStrategyPathKey,
        ProcessingStrategyPathData = dto.ProcessingStrategyPathData,
        Auth = dto.Auth == null ? null : new VendorAuthConfig
        {
            Id = dto.Auth.Id,
            GpsVendorId = dto.Auth.GpsVendorId,
            BaseUrl = dto.Auth.BaseUrl,
            Method = dto.Auth.Method,
            AuthType = dto.Auth.AuthType,
            ContentType = dto.Auth.ContentType,
            Username = dto.Auth.Username,
            Password = dto.Auth.Password,
            HeadersJson = dto.Auth.HeadersJson,
            ParamsJson = dto.Auth.ParamsJson,
            BodiesJson = dto.Auth.BodiesJson,
            TokenPath = dto.Auth.TokenPath
        },
        Endpoints = dto.Endpoints.Select(e => new VendorEndpointConfig
        {
            Id = e.Id,
            GpsVendorId = e.GpsVendorId,
            EndpointName = e.EndpointName,
            BaseUrl = e.BaseUrl,
            Method = e.Method,
            HeadersJson = e.HeadersJson,
            ParamsJson = e.ParamsJson,
            BodiesJson = e.BodiesJson,
            VarParamsJson = e.VarParamsJson,
            MaxPath = e.MaxPath
        }).ToList(),
        Mappings = dto.Mappings.Select(m => new VendorFieldMapping
        {
            GpsVendorId = m.GpsVendorId,
            ResponseField = m.ResponseField,
            MappedField = m.MappedField
        }).ToList()
    };

    private static ActiveVendorCacheDto MapToCacheDto(VendorConfig v) => new()
    {
        Id = v.Id,
        VendorCode = v.VendorCode,
        VendorName = v.VendorName,
        Lpcd = v.Lpcd,
        RequiredAuth = v.RequiredAuth,
        AuthType = v.AuthType,
        Username = v.Username,
        Password = v.Password,
        ProcessingStrategy = v.ProcessingStrategy,
        ProcessingStrategyPathKey = v.ProcessingStrategyPathKey,
        ProcessingStrategyPathData = v.ProcessingStrategyPathData,
        Auth = v.Auth == null ? null : new ActiveVendorAuthCacheDto
        {
            Id = v.Auth.Id,
            GpsVendorId = v.Auth.GpsVendorId,
            BaseUrl = v.Auth.BaseUrl,
            Method = v.Auth.Method,
            AuthType = v.Auth.AuthType,
            ContentType = v.Auth.ContentType,
            Username = v.Auth.Username,
            Password = v.Auth.Password,
            HeadersJson = v.Auth.HeadersJson,
            ParamsJson = v.Auth.ParamsJson,
            BodiesJson = v.Auth.BodiesJson,
            TokenPath = v.Auth.TokenPath
        },
        Endpoints = v.Endpoints.Select(e => new ActiveVendorEndpointCacheDto
        {
            Id = e.Id,
            GpsVendorId = e.GpsVendorId,
            EndpointName = e.EndpointName,
            BaseUrl = e.BaseUrl,
            Method = e.Method,
            HeadersJson = e.HeadersJson,
            ParamsJson = e.ParamsJson,
            BodiesJson = e.BodiesJson,
            VarParamsJson = e.VarParamsJson,
            MaxPath = e.MaxPath
        }).ToList(),
        Mappings = v.Mappings.Select(m => new ActiveVendorMappingCacheDto
        {
            GpsVendorId = m.GpsVendorId,
            ResponseField = m.ResponseField,
            MappedField = m.MappedField
        }).ToList()
    };

    private sealed class VendorConfigBuilder
    {
        public Guid Id { get; set; }
        public string VendorCode { get; set; } = string.Empty;
        public string VendorName { get; set; } = string.Empty;
        public string? Lpcd { get; set; }
        public bool RequiredAuth { get; set; }
        public string? AuthType { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string ProcessingStrategy { get; set; } = "individual";
        public string? ProcessingStrategyPathKey { get; set; }
        public string ProcessingStrategyPathData { get; set; } = "data";
        public VendorAuthConfig? Auth { get; set; }
        public List<VendorEndpointConfig> Endpoints { get; } = [];
        public List<VendorFieldMapping> Mappings { get; } = [];

        public VendorConfig Build() => new()
        {
            Id = Id,
            VendorCode = VendorCode,
            VendorName = VendorName,
            Lpcd = Lpcd,
            RequiredAuth = RequiredAuth,
            AuthType = AuthType,
            Username = Username,
            Password = Password,
            ProcessingStrategy = ProcessingStrategy,
            ProcessingStrategyPathKey = ProcessingStrategyPathKey,
            ProcessingStrategyPathData = ProcessingStrategyPathData,
            Auth = Auth,
            Endpoints = Endpoints,
            Mappings = Mappings
        };
    }
}
