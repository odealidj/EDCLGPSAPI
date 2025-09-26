using GpsCollector.Application.Ports;
using GpsCollector.Domain.Models;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace GpsCollector.Infrastructure.Adapters.Persistence;

// Bulk Ingestion Telemetri GPS berkinerja tinggi menggunakan PostgreSQL Binary COPY Stream (Zero Reflection & AOT Ready)
public sealed class PostgresTelemetryIngestionRepository : ITelemetryIngestionRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PostgresTelemetryIngestionRepository> _logger;

    public PostgresTelemetryIngestionRepository(NpgsqlDataSource dataSource, ILogger<PostgresTelemetryIngestionRepository> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task<(int TotalInserted, int DeliveryInserted)> IngestTelemetryBatchAsync(
        Guid vendorId,
        string vendorName,
        Guid headerId,
        IReadOnlyList<TelemetryPoint> points,
        IReadOnlyDictionary<string, DeliveryProgressSession> activeSessions,
        CancellationToken ct)
    {
        if (points.Count == 0) return (0, 0);

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            var now = DateTime.UtcNow;
            const string createdBy = "System";

            // 1. Simpan Header ke tb_r_gps_last_position_h
            const string insertHeaderSql = @"
                INSERT INTO edcl.tb_r_gps_last_position_h (""Id"", ""GpsVendorId"", ""CreatedAt"", ""CreatedBy"")
                VALUES (@id, @vendorId, @createdAt, @createdBy);";

            await using (var cmd = new NpgsqlCommand(insertHeaderSql, conn, tx))
            {
                cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Uuid) { Value = headerId });
                cmd.Parameters.Add(new NpgsqlParameter("vendorId", NpgsqlDbType.Uuid) { Value = vendorId });
                cmd.Parameters.Add(new NpgsqlParameter("createdAt", NpgsqlDbType.Timestamp) { Value = now });
                cmd.Parameters.Add(new NpgsqlParameter("createdBy", NpgsqlDbType.Text) { Value = createdBy });
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // 2. Bulk Insert Detail ke tb_r_gps_last_position_d via Binary COPY stream
            const string copyDetailSql = @"
                COPY edcl.tb_r_gps_last_position_d (
                    ""Id"", ""GpsLastPositionHId"", ""Lpcd"", ""PlatNo"", ""DeviceId"", 
                    ""Datetime"", ""X"", ""Y"", ""Speed"", ""Course"", ""StreetName"", 
                    ""CreatedAt"", ""CreatedBy""
                ) FROM STDIN (FORMAT BINARY);";

            await using (var writerD = await conn.BeginBinaryImportAsync(copyDetailSql, ct))
            {
                foreach (var p in points)
                {
                    await writerD.StartRowAsync(ct);
                    await writerD.WriteAsync(p.Id, NpgsqlDbType.Uuid, ct);
                    await writerD.WriteAsync(headerId, NpgsqlDbType.Uuid, ct);
                    await writerD.WriteAsync(p.Lpcd, NpgsqlDbType.Text, ct);
                    await writerD.WriteAsync(p.PlatNo, NpgsqlDbType.Text, ct);
                    await writerD.WriteAsync(p.DeviceId, NpgsqlDbType.Text, ct);
                    await writerD.WriteAsync(p.Datetime, NpgsqlDbType.Timestamp, ct);
                    await writerD.WriteAsync(p.X, NpgsqlDbType.Numeric, ct);
                    await writerD.WriteAsync(p.Y, NpgsqlDbType.Numeric, ct);
                    await writerD.WriteAsync(p.Speed, NpgsqlDbType.Numeric, ct);
                    await writerD.WriteAsync(p.Course, NpgsqlDbType.Numeric, ct);
                    await writerD.WriteAsync(p.StreetName, NpgsqlDbType.Text, ct);
                    await writerD.WriteAsync(now, NpgsqlDbType.Timestamp, ct);
                    await writerD.WriteAsync(createdBy, NpgsqlDbType.Text, ct);
                }

                await writerD.CompleteAsync(ct);
            }

            // 3. Filter titik armada yang memiliki sesi delivery aktif di tb_r_delivery_progress
            var deliveryRows = new List<(TelemetryPoint Point, DeliveryProgressSession Session)>();
            foreach (var p in points)
            {
                if (!string.IsNullOrWhiteSpace(p.PlatNo) && activeSessions.TryGetValue(p.PlatNo, out var session))
                {
                    deliveryRows.Add((p, session));
                }
            }

            // 4. Bulk Insert ke tb_r_gps_delivery via Binary COPY stream jika ada armada yang sedang bertugas
            if (deliveryRows.Count > 0)
            {
                const string copyDeliverySql = @"
                    COPY edcl.tb_r_gps_delivery (
                        ""Id"", ""GpsVendorId"", ""GpsVendorName"", ""GpsDeliveryHId"", 
                        ""Lpcd"", ""DeliveryNo"", ""NoKtp"", ""PlatNo"", ""DeviceId"", 
                        ""Datetime"", ""X"", ""Y"", ""Speed"", ""Course"", ""StreetName"", 
                        ""CreatedAt"", ""CreatedBy""
                    ) FROM STDIN (FORMAT BINARY);";

                await using var writerDel = await conn.BeginBinaryImportAsync(copyDeliverySql, ct);
                foreach (var (p, s) in deliveryRows)
                {
                    await writerDel.StartRowAsync(ct);
                    await writerDel.WriteAsync(Guid.NewGuid(), NpgsqlDbType.Uuid, ct);
                    await writerDel.WriteAsync(vendorId, NpgsqlDbType.Uuid, ct);
                    await writerDel.WriteAsync(vendorName, NpgsqlDbType.Text, ct);
                    await writerDel.WriteAsync(headerId, NpgsqlDbType.Uuid, ct);
                    await writerDel.WriteAsync(s.Lpcd ?? p.Lpcd, NpgsqlDbType.Text, ct);
                    await writerDel.WriteAsync(s.DeliveryNo, NpgsqlDbType.Text, ct);
                    await writerDel.WriteAsync(s.NoKtp, NpgsqlDbType.Text, ct);
                    await writerDel.WriteAsync(p.PlatNo, NpgsqlDbType.Text, ct);
                    await writerDel.WriteAsync(p.DeviceId, NpgsqlDbType.Text, ct);
                    await writerDel.WriteAsync(p.Datetime, NpgsqlDbType.Timestamp, ct);
                    await writerDel.WriteAsync(p.X, NpgsqlDbType.Numeric, ct);
                    await writerDel.WriteAsync(p.Y, NpgsqlDbType.Numeric, ct);
                    await writerDel.WriteAsync(p.Speed, NpgsqlDbType.Numeric, ct);
                    await writerDel.WriteAsync(p.Course, NpgsqlDbType.Numeric, ct);
                    await writerDel.WriteAsync(p.StreetName, NpgsqlDbType.Text, ct);
                    await writerDel.WriteAsync(now, NpgsqlDbType.Timestamp, ct);
                    await writerDel.WriteAsync(createdBy, NpgsqlDbType.Text, ct);
                }

                await writerDel.CompleteAsync(ct);
            }

            await tx.CommitAsync(ct);

            _logger.LogInformation(
                "Ingested {PointsCount} raw telemetry points and {DeliveryCount} delivery points for vendor {VendorName}",
                points.Count, deliveryRows.Count, vendorName);

            return (points.Count, deliveryRows.Count);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to ingest telemetry batch for vendor {VendorName}: {ErrorMessage}", vendorName, ex.Message);
            throw;
        }
    }
}
