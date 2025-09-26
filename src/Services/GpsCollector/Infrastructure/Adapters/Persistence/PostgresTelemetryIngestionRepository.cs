using GpsCollector.Application.Ports;
using GpsCollector.Domain.Models;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace GpsCollector.Infrastructure.Adapters.Persistence;

// Bulk Ingestion Telemetri GPS hanya untuk armada dengan Delivery Job Aktif ke edcl.t_delivery_latest_position & edcl.t_delivery_position_history
public sealed class PostgresTelemetryIngestionRepository : ITelemetryIngestionRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PostgresTelemetryIngestionRepository> _logger;

    public PostgresTelemetryIngestionRepository(NpgsqlDataSource dataSource, ILogger<PostgresTelemetryIngestionRepository> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task<int> IngestDeliveryPositionsAsync(
        Guid vendorId,
        string vendorName,
        IReadOnlyList<(TelemetryPoint Point, DeliveryProgressSession Session)> matchedDeliveries,
        CancellationToken ct)
    {
        if (matchedDeliveries.Count == 0) return 0;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            var now = DateTime.UtcNow;

            // 1. Upsert snapshot ke edcl.t_delivery_latest_position
            const string upsertLatestSql = @"
                INSERT INTO edcl.t_delivery_latest_position (
                    delivery_no, session_id, vendor_id, plate_number, vehicle_id,
                    datetime, longitude, latitude, speed, course, street_name, updated_at
                ) VALUES (
                    @deliveryNo, @sessionId, @vendorId, @plateNumber, @vehicleId,
                    @datetime, @longitude, @latitude, @speed, @course, @streetName, @updatedAt
                )
                ON CONFLICT (delivery_no) DO UPDATE SET
                    session_id = EXCLUDED.session_id,
                    vendor_id = EXCLUDED.vendor_id,
                    plate_number = EXCLUDED.plate_number,
                    vehicle_id = EXCLUDED.vehicle_id,
                    datetime = EXCLUDED.datetime,
                    longitude = EXCLUDED.longitude,
                    latitude = EXCLUDED.latitude,
                    speed = EXCLUDED.speed,
                    course = EXCLUDED.course,
                    street_name = EXCLUDED.street_name,
                    updated_at = EXCLUDED.updated_at;";

            foreach (var (point, session) in matchedDeliveries)
            {
                await using var cmdLatest = new NpgsqlCommand(upsertLatestSql, conn, tx);
                cmdLatest.Parameters.Add(new NpgsqlParameter("deliveryNo", NpgsqlDbType.Varchar) { Value = session.DeliveryNo });
                cmdLatest.Parameters.Add(new NpgsqlParameter("sessionId", NpgsqlDbType.Uuid) { Value = session.SessionId });
                cmdLatest.Parameters.Add(new NpgsqlParameter("vendorId", NpgsqlDbType.Uuid) { Value = (object?)session.VendorId ?? vendorId });
                cmdLatest.Parameters.Add(new NpgsqlParameter("plateNumber", NpgsqlDbType.Varchar) { Value = (object?)point.PlatNo ?? session.MatchValue });
                cmdLatest.Parameters.Add(new NpgsqlParameter("vehicleId", NpgsqlDbType.Varchar) { Value = (object?)point.DeviceId ?? DBNull.Value });
                cmdLatest.Parameters.Add(new NpgsqlParameter("datetime", NpgsqlDbType.Timestamp) { Value = point.Datetime == default ? now : point.Datetime });
                cmdLatest.Parameters.Add(new NpgsqlParameter("longitude", NpgsqlDbType.Numeric) { Value = point.X });
                cmdLatest.Parameters.Add(new NpgsqlParameter("latitude", NpgsqlDbType.Numeric) { Value = point.Y });
                cmdLatest.Parameters.Add(new NpgsqlParameter("speed", NpgsqlDbType.Numeric) { Value = point.Speed });
                cmdLatest.Parameters.Add(new NpgsqlParameter("course", NpgsqlDbType.Numeric) { Value = point.Course });
                cmdLatest.Parameters.Add(new NpgsqlParameter("streetName", NpgsqlDbType.Varchar) { Value = (object?)point.StreetName ?? DBNull.Value });
                cmdLatest.Parameters.Add(new NpgsqlParameter("updatedAt", NpgsqlDbType.Timestamp) { Value = now });

                await cmdLatest.ExecuteNonQueryAsync(ct);
            }

            // 2. Insert breadcrumbs jejak rute ke edcl.t_delivery_position_history
            const string insertHistorySql = @"
                INSERT INTO edcl.t_delivery_position_history (
                    id, delivery_no, session_id, datetime, longitude, latitude, speed, course, street_name, created_at
                ) VALUES (
                    gen_random_uuid(), @deliveryNo, @sessionId, @datetime, @longitude, @latitude, @speed, @course, @streetName, @createdAt
                );";

            foreach (var (point, session) in matchedDeliveries)
            {
                await using var cmdHist = new NpgsqlCommand(insertHistorySql, conn, tx);
                cmdHist.Parameters.Add(new NpgsqlParameter("deliveryNo", NpgsqlDbType.Varchar) { Value = session.DeliveryNo });
                cmdHist.Parameters.Add(new NpgsqlParameter("sessionId", NpgsqlDbType.Uuid) { Value = session.SessionId });
                cmdHist.Parameters.Add(new NpgsqlParameter("datetime", NpgsqlDbType.Timestamp) { Value = point.Datetime == default ? now : point.Datetime });
                cmdHist.Parameters.Add(new NpgsqlParameter("longitude", NpgsqlDbType.Numeric) { Value = point.X });
                cmdHist.Parameters.Add(new NpgsqlParameter("latitude", NpgsqlDbType.Numeric) { Value = point.Y });
                cmdHist.Parameters.Add(new NpgsqlParameter("speed", NpgsqlDbType.Numeric) { Value = point.Speed });
                cmdHist.Parameters.Add(new NpgsqlParameter("course", NpgsqlDbType.Numeric) { Value = point.Course });
                cmdHist.Parameters.Add(new NpgsqlParameter("streetName", NpgsqlDbType.Varchar) { Value = (object?)point.StreetName ?? DBNull.Value });
                cmdHist.Parameters.Add(new NpgsqlParameter("createdAt", NpgsqlDbType.Timestamp) { Value = now });

                await cmdHist.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);

            _logger.LogInformation(
                "Ingested {Count} delivery position points for vendor {VendorName} into active delivery tables",
                matchedDeliveries.Count, vendorName);

            return matchedDeliveries.Count;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to ingest delivery positions for vendor {VendorName}: {ErrorMessage}", vendorName, ex.Message);
            throw;
        }
    }

    public async Task<(int TotalInserted, int DeliveryInserted)> IngestTelemetryBatchAsync(
        Guid vendorId,
        string vendorName,
        Guid headerId,
        IReadOnlyList<TelemetryPoint> points,
        IReadOnlyDictionary<string, DeliveryProgressSession> activeSessions,
        CancellationToken ct)
    {
        var matched = new List<(TelemetryPoint Point, DeliveryProgressSession Session)>();
        foreach (var p in points)
        {
            if (!string.IsNullOrWhiteSpace(p.PlatNo) && activeSessions.TryGetValue(p.PlatNo, out var s))
            {
                matched.Add((p, s));
            }
        }

        var count = await IngestDeliveryPositionsAsync(vendorId, vendorName, matched, ct);
        return (0, count);
    }
}
