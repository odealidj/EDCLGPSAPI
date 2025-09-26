using GpsCollector.Application.Ports;
using GpsCollector.Domain.Models;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace GpsCollector.Infrastructure.Adapters.Persistence;

// Repositori query sesi delivery aktif armada (tb_r_delivery_progress) via pure ADO.NET
public sealed class PostgresDeliverySessionRepository : IDeliverySessionRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PostgresDeliverySessionRepository> _logger;

    public PostgresDeliverySessionRepository(NpgsqlDataSource dataSource, ILogger<PostgresDeliverySessionRepository> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
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

        const string sql = @"
            SELECT DISTINCT ON (""PlatNo"") ""PlatNo"", ""DeliveryNo"", ""Lpcd"", ""NoKtp""
            FROM edcl.tb_r_delivery_progress
            WHERE UPPER(""PlatNo"") = ANY(@platNos)
            ORDER BY ""PlatNo"", ""CreatedAt"" DESC;";

        var result = new Dictionary<string, DeliveryProgressSession>(platArray.Length, StringComparer.OrdinalIgnoreCase);

        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);

            cmd.Parameters.Add(new NpgsqlParameter("platNos", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = platArray });

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var platNo = reader.IsDBNull(0) ? string.Empty : reader.GetString(0).Trim().ToUpperInvariant();
                if (string.IsNullOrEmpty(platNo)) continue;

                result[platNo] = new DeliveryProgressSession
                {
                    PlatNo = platNo,
                    DeliveryNo = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Lpcd = reader.IsDBNull(2) ? null : reader.GetString(2),
                    NoKtp = reader.IsDBNull(3) ? null : reader.GetString(3)
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query active delivery progress sessions for plates: {PlatNos}", string.Join(", ", platArray));
        }

        return result;
    }
}
