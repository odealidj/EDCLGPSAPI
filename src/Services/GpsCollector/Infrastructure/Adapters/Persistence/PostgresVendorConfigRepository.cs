using System.Data;
using GpsCollector.Application.Ports;
using GpsCollector.Domain.Models;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace GpsCollector.Infrastructure.Adapters.Persistence;

// Repositori konfigurasi vendor dan sistem menggunakan pure ADO.NET (AOT Ready)
public sealed class PostgresVendorConfigRepository : IVendorConfigRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PostgresVendorConfigRepository> _logger;

    public PostgresVendorConfigRepository(NpgsqlDataSource dataSource, ILogger<PostgresVendorConfigRepository> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task<List<VendorConfig>> GetActiveVendorsAsync(CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);

        // 1. Ambil semua vendor aktif beserta LPCD-nya
        var vendorDict = new Dictionary<Guid, VendorConfigBuilder>();

        const string sqlVendors = @"
            SELECT v.id, v.vendor_name, v.vendor_code, vl.lpcd,
                   v.processing_strategy, v.processing_strategy_path_key, v.processing_strategy_path_data
            FROM edcl.m_gps_vendor v
            LEFT JOIN edcl.m_gps_vendor_lpcd vl ON v.id = vl.vendor_id
            WHERE v.is_active = true AND v.is_deleted = false;";

        await using (var cmd = new NpgsqlCommand(sqlVendors, conn))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
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
                }
                else if (string.IsNullOrEmpty(builder.Lpcd) && !reader.IsDBNull(3))
                {
                    builder.Lpcd = reader.GetString(3);
                }
            }
        }

        if (vendorDict.Count == 0) return [];

        // 2. Ambil endpoints
        const string sqlEndpoints = @"
            SELECT id, vendor_id, endpoint_type, base_url, method, 
                   headers::text, params::text, bodies::text, var_params::text, max_path
            FROM edcl.m_gps_vendor_endpoint;";

        await using (var cmd = new NpgsqlCommand(sqlEndpoints, conn))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var vendorId = reader.GetGuid(1);
                if (vendorDict.TryGetValue(vendorId, out var builder))
                {
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
            }
        }

        // 3. Ambil auth
        const string sqlAuth = @"
            SELECT id, vendor_id, base_url, method, auth_type, content_type, 
                   username, password, headers::text, params::text, bodies::text, token_path
            FROM edcl.m_gps_vendor_auth;";

        await using (var cmd = new NpgsqlCommand(sqlAuth, conn))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var vendorId = reader.GetGuid(1);
                if (vendorDict.TryGetValue(vendorId, out var builder))
                {
                    builder.Auth = new VendorAuthConfig
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
                    builder.RequiredAuth = true;
                    builder.AuthType = builder.Auth.AuthType;
                    builder.Username = builder.Auth.Username;
                    builder.Password = builder.Auth.Password;
                }
            }
        }

        // 4. Ambil mappings
        const string sqlMappings = @"
            SELECT vendor_id, response_field, mapped_field
            FROM edcl.m_gps_vendor_mapping;";

        await using (var cmd = new NpgsqlCommand(sqlMappings, conn))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var vendorId = reader.GetGuid(0);
                if (vendorDict.TryGetValue(vendorId, out var builder))
                {
                    builder.Mappings.Add(new VendorFieldMapping
                    {
                        GpsVendorId = vendorId,
                        ResponseField = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        MappedField = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    });
                }
            }
        }

        return vendorDict.Values.Select(b => b.Build()).ToList();
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
            WHERE sys_cat = 'Delay' AND sys_sub_cat = 'EDCL_GPS_WORKER_SERVICE' AND sys_cd = '1'
            LIMIT 1;";

        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            var result = await cmd.ExecuteScalarAsync(ct);

            if (result != null && int.TryParse(result.ToString(), out var minutes) && minutes > 0)
            {
                return minutes;
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
            cmd.Parameters.Add(new NpgsqlParameter("path", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = new[] { propertyName } });
            cmd.Parameters.Add(new NpgsqlParameter("val", NpgsqlDbType.Jsonb) { Value = maxId.ToString() });
            cmd.Parameters.Add(new NpgsqlParameter("modifiedAt", NpgsqlDbType.Timestamp) { Value = DateTime.UtcNow });

            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update VarParams for endpoint {EndpointId}", endpointId);
        }
    }

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
