using GpsCollector.Application.Ports;
using GpsCollector.Domain.Models;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace GpsCollector.Infrastructure.Adapters.Persistence;

// Repositori query sesi delivery aktif armada dari edcl.t_delivery_tracking_session via pure ADO.NET
public sealed class PostgresDeliverySessionRepository : IDeliverySessionRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PostgresDeliverySessionRepository> _logger;

    public PostgresDeliverySessionRepository(NpgsqlDataSource dataSource, ILogger<PostgresDeliverySessionRepository> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task<List<DeliveryProgressSession>> GetActiveSessionsForVendorAsync(
        Guid vendorId,
        string? lpcd,
        CancellationToken ct)
    {
        var result = new List<DeliveryProgressSession>();

        const string sql = @"
            SELECT id, delivery_no, lpcd, vendor_id, match_key, match_value, clean_match_value, driver_ktp
            FROM edcl.t_delivery_tracking_session
            WHERE status IN ('STARTED', 'IN_TRANSIT')
              AND is_deleted = false
              AND (vendor_id = @vendorId OR (@lpcd IS NOT NULL AND UPPER(lpcd) = UPPER(@lpcd)));";

        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);

            cmd.Parameters.Add(new NpgsqlParameter("vendorId", NpgsqlDbType.Uuid) { Value = vendorId });
            cmd.Parameters.Add(new NpgsqlParameter("lpcd", NpgsqlDbType.Varchar) { Value = (object?)lpcd ?? DBNull.Value });

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                result.Add(new DeliveryProgressSession
                {
                    SessionId = reader.GetGuid(0),
                    DeliveryNo = reader.GetString(1),
                    Lpcd = reader.GetString(2),
                    VendorId = reader.IsDBNull(3) ? null : reader.GetGuid(3),
                    MatchKey = reader.IsDBNull(4) ? "PlatNo" : reader.GetString(4),
                    MatchValue = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    CleanMatchValue = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    DriverKtp = reader.IsDBNull(7) ? null : reader.GetString(7)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query active delivery tracking sessions for vendor {VendorId}, LPCD {Lpcd}", vendorId, lpcd);
        }

        return result;
    }

    public async Task<Dictionary<string, DeliveryProgressSession>> GetActiveSessionsByPlatNosAsync(
        IEnumerable<string> platNos,
        CancellationToken ct)
    {
        var platArray = platNos
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim().ToUpperInvariant())
            .Distinct()
            .ToArray();

        if (platArray.Length == 0)
        {
            return new Dictionary<string, DeliveryProgressSession>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, DeliveryProgressSession>(platArray.Length, StringComparer.OrdinalIgnoreCase);

        const string sql = @"
            SELECT id, delivery_no, lpcd, vendor_id, match_key, match_value, clean_match_value, driver_ktp
            FROM edcl.t_delivery_tracking_session
            WHERE status IN ('STARTED', 'IN_TRANSIT')
              AND is_deleted = false
              AND clean_match_value = ANY(@platNos);";

        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);

            cmd.Parameters.Add(new NpgsqlParameter("platNos", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = platArray });

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var cleanVal = reader.IsDBNull(6) ? string.Empty : reader.GetString(6);
                if (string.IsNullOrEmpty(cleanVal)) continue;

                result[cleanVal] = new DeliveryProgressSession
                {
                    SessionId = reader.GetGuid(0),
                    DeliveryNo = reader.GetString(1),
                    Lpcd = reader.GetString(2),
                    VendorId = reader.IsDBNull(3) ? null : reader.GetGuid(3),
                    MatchKey = reader.IsDBNull(4) ? "PlatNo" : reader.GetString(4),
                    MatchValue = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    CleanMatchValue = cleanVal,
                    DriverKtp = reader.IsDBNull(7) ? null : reader.GetString(7)
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query active delivery tracking sessions for plates: {PlatNos}", string.Join(", ", platArray));
        }

        return result;
    }
}
