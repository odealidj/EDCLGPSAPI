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

        // 1. Ambil semua vendor
        var vendorDict = new Dictionary<Guid, VendorConfigBuilder>();

        const string sqlVendors = @"
            SELECT ""Id"", ""VendorName"", ""RequiredAuth"", ""AuthType"", ""Username"", ""Password"", 
                   ""ProcessingStrategy"", ""ProcessingStrategyPathKey"", ""ProcessingStrategyPathData""
            FROM edcl.tb_m_gps_vendor;";

        await using (var cmd = new NpgsqlCommand(sqlVendors, conn))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var id = reader.GetGuid(0);
                vendorDict[id] = new VendorConfigBuilder
                {
                    Id = id,
                    VendorName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    RequiredAuth = !reader.IsDBNull(2) && reader.GetBoolean(2),
                    AuthType = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Username = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Password = reader.IsDBNull(5) ? null : reader.GetString(5),
                    ProcessingStrategy = reader.IsDBNull(6) ? "individual" : reader.GetString(6),
                    ProcessingStrategyPathKey = reader.IsDBNull(7) ? null : reader.GetString(7),
                    ProcessingStrategyPathData = reader.IsDBNull(8) ? "data" : reader.GetString(8),
                };
            }
        }

        if (vendorDict.Count == 0) return [];

        // 2. Ambil endpoints
        const string sqlEndpoints = @"
            SELECT ""Id"", ""GpsVendorId"", ""EndpointName"", ""BaseUrl"", ""Method"", 
                   ""Headers""::text, ""Params""::text, ""Bodies""::text, ""VarParams""::text, ""MaxPath""
            FROM edcl.tb_m_gps_vendor_endpoint;";

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
            SELECT ""Id"", ""GpsVendorId"", ""BaseUrl"", ""Method"", ""Authtype"", ""ContentType"", 
                   ""Username"", ""Password"", ""Headers""::text, ""Params""::text, ""Bodies""::text, ""TokenPath""
            FROM edcl.tb_m_gps_vendor_auth;";

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
                }
            }
        }

        // 4. Ambil mappings
        const string sqlMappings = @"
            SELECT ""GpsVendorId"", ""ResponseField"", ""MappedField""
            FROM edcl.tb_m_mapping;";

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
            SELECT ""SysValue"" FROM edcl.tb_m_system
            WHERE ""SysCat"" = 'Delay' AND ""SysSubCat"" = 'EDCL_GPS_WORKER_SERVICE' AND ""SysCd"" = '1'
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
            _logger.LogWarning(ex, "Failed to read delay config from tb_m_system, falling back to 1 minute");
        }

        return 1;
    }

    public async Task UpdateVarParamsMaxIdAsync(Guid endpointId, string propertyName, int maxId, CancellationToken ct)
    {
        const string sql = @"
            UPDATE edcl.tb_m_gps_vendor_endpoint
            SET ""VarParams"" = jsonb_set(COALESCE(""VarParams"", '{}'::jsonb), @path, @val::jsonb),
                ""LastModified"" = @modifiedAt,
                ""LastModifiedBy"" = 'System'
            WHERE ""Id"" = @endpointId;";

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
        public string VendorName { get; set; } = string.Empty;
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
            VendorName = VendorName,
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
