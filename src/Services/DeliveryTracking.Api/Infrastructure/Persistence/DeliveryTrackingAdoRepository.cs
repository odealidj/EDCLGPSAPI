using System.Data;
using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Application.Ports;
using DeliveryTracking.Domain.Entities;
using DeliveryTracking.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace DeliveryTracking.Infrastructure.Persistence;

// implementasi repository delivery tracking berbasis ado.net murni (npgsql)
public class DeliveryTrackingAdoRepository(
    NpgsqlDataSource dataSource,
    ILogger<DeliveryTrackingAdoRepository> logger) : IDeliveryTrackingRepository
{
    // 1. ambil jejak koordinat gps via stored procedure (dengan fallback sp1 -> sp2)
    public async Task<IReadOnlyList<TrackDeliveryPointDto>> GetTrackDeliveryAsync(
        string deliveryNo,
        int density = 1,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var points = new List<TrackDeliveryPointDto>();

            // coba jalankan stored procedure 1
            await using (var cmd = dataSource.CreateCommand("SELECT * FROM edcl.sp_edclgps1_get_track_delivery($1, $2);"))
            {
                cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, deliveryNo);
                cmd.Parameters.AddWithValue(NpgsqlDbType.Integer, density);
                cmd.CommandTimeout = 60;

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    points.Add(reader.MapToTrackDeliveryPoint());
                }
            }

            if (points.Count > 0)
                return points;

            // fallback ke stored procedure 2 jika sp 1 kosong
            await using (var cmd2 = dataSource.CreateCommand("SELECT * FROM edcl.sp_edclgps2_get_track_delivery($1, $2);"))
            {
                cmd2.Parameters.AddWithValue(NpgsqlDbType.Varchar, deliveryNo);
                cmd2.Parameters.AddWithValue(NpgsqlDbType.Integer, density);
                cmd2.CommandTimeout = 60;

                await using var reader2 = await cmd2.ExecuteReaderAsync(cancellationToken);
                while (await reader2.ReadAsync(cancellationToken))
                {
                    points.Add(reader2.MapToTrackDeliveryPoint());
                }
            }

            return points;
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Gagal mengeksekusi stored procedure get_track_delivery untuk DO {DeliveryNo}", deliveryNo);
            throw new DeliveryProgressBadRequestException($"Gagal memproses tracking delivery: {ex.MessageText}");
        }
    }

    // 2. ambil status progres delivery by nomor do
    public async Task<DeliveryProgress?> GetDeliveryProgressByDeliveryNoAsync(
        string deliveryNo,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT id, delivery_no, plat_no, no_ktp, vendor_name, lpcd, created_at, created_by, last_modified, last_modified_by
            FROM edcl.tb_r_delivery_progress
            WHERE delivery_no = $1
            LIMIT 1;";

        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, deliveryNo.Trim().ToUpperInvariant());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return reader.MapToDeliveryProgress();
        }

        return null;
    }

    // 3. upsert delivery progress dengan native postgresql on conflict
    public async Task<SaveDeliveryProgressResult> UpsertDeliveryProgressAsync(
        DeliveryProgress progress,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO edcl.tb_r_delivery_progress (
                id, delivery_no, plat_no, no_ktp, vendor_name, lpcd, created_at, created_by, last_modified, last_modified_by
            )
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
            ON CONFLICT (delivery_no) DO UPDATE SET
                plat_no = EXCLUDED.plat_no,
                no_ktp = EXCLUDED.no_ktp,
                vendor_name = EXCLUDED.vendor_name,
                lpcd = EXCLUDED.lpcd,
                last_modified = EXCLUDED.last_modified,
                last_modified_by = EXCLUDED.last_modified_by
            RETURNING id, (xmax = 0) AS is_created;";

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var progressId = progress.Id == Guid.Empty ? Guid.NewGuid() : progress.Id;

        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Uuid, progressId);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, progress.DeliveryNo);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, progress.PlatNo);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, progress.NoKtp);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, (object?)progress.VendorName ?? DBNull.Value);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, (object?)progress.Lpcd ?? DBNull.Value);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Timestamp, now);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, "System");
        cmd.Parameters.AddWithValue(NpgsqlDbType.Timestamp, now);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, "System");

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var savedId = reader.GetSafeGuid("id");
            var isCreated = reader.GetBoolean(reader.GetOrdinal("is_created"));
            return new SaveDeliveryProgressResult(savedId, progress.DeliveryNo, isCreated);
        }

        return new SaveDeliveryProgressResult(progressId, progress.DeliveryNo, false);
    }

    // 4. bulk insert berkecepatan tinggi menggunakan postgresql binary copy (importer)
    public async Task<int> BulkInsertDeliveryProgressAsync(
        IReadOnlyList<DeliveryProgress> items,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0) return 0;

        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        const string copySql = @"
            COPY edcl.tb_r_delivery_progress (
                id, delivery_no, plat_no, no_ktp, vendor_name, lpcd, created_at, created_by, last_modified, last_modified_by
            ) FROM STDIN (FORMAT BINARY);";

        await using var writer = await conn.BeginBinaryImportAsync(copySql, cancellationToken);
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        foreach (var item in items)
        {
            var id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id;
            await writer.StartRowAsync(cancellationToken);
            await writer.WriteAsync(id, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(item.DeliveryNo, NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(item.PlatNo, NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(item.NoKtp, NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(item.VendorName, NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(item.Lpcd, NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(now, NpgsqlDbType.Timestamp, cancellationToken);
            await writer.WriteAsync("System", NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(now, NpgsqlDbType.Timestamp, cancellationToken);
            await writer.WriteAsync("System", NpgsqlDbType.Varchar, cancellationToken);
        }

        var insertedCount = await writer.CompleteAsync(cancellationToken);
        return (int)insertedCount;
    }

    // 5. bulk insert titik koordinat telemetri gps ke tabel tb_r_gps_delivery
    public async Task<int> BulkInsertGpsDeliveriesAsync(
        IReadOnlyList<GpsDelivery> items,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0) return 0;

        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        const string copySql = @"
            COPY edcl.tb_r_gps_delivery (
                id, gps_vendor_id, gps_vendor_name, gps_delivery_h_id, lpcd_id, lpcd,
                delivery_no, no_ktp, plat_no, device_id, datetime, x, y, speed, course,
                street_name, created_at, created_by, last_modified, last_modified_by
            ) FROM STDIN (FORMAT BINARY);";

        await using var writer = await conn.BeginBinaryImportAsync(copySql, cancellationToken);
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        foreach (var item in items)
        {
            var id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id;
            var dt = item.Datetime.HasValue 
                ? DateTime.SpecifyKind(item.Datetime.Value, DateTimeKind.Unspecified) 
                : now;

            await writer.StartRowAsync(cancellationToken);
            await writer.WriteAsync(id, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(item.GpsVendorId, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(item.GpsVendorName, NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(item.GpsDeliveryHId ?? (object)DBNull.Value, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(item.LpcdId ?? (object)DBNull.Value, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(item.Lpcd, NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(item.DeliveryNo, NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(item.NoKtp, NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(item.PlatNo, NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(item.DeviceId, NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(dt, NpgsqlDbType.Timestamp, cancellationToken);
            await writer.WriteAsync(item.X, NpgsqlDbType.Numeric, cancellationToken);
            await writer.WriteAsync(item.Y, NpgsqlDbType.Numeric, cancellationToken);
            await writer.WriteAsync(item.Speed, NpgsqlDbType.Numeric, cancellationToken);
            await writer.WriteAsync(item.Course, NpgsqlDbType.Numeric, cancellationToken);
            await writer.WriteAsync(item.StreetName, NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(now, NpgsqlDbType.Timestamp, cancellationToken);
            await writer.WriteAsync("System", NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(now, NpgsqlDbType.Timestamp, cancellationToken);
            await writer.WriteAsync("System", NpgsqlDbType.Varchar, cancellationToken);
        }

        var insertedCount = await writer.CompleteAsync(cancellationToken);
        return (int)insertedCount;
    }

    // 6. hapus progress delivery
    public async Task<bool> DeleteDeliveryProgressAsync(
        string deliveryNo,
        CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM edcl.tb_r_delivery_progress WHERE delivery_no = $1;";
        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, deliveryNo.Trim().ToUpperInvariant());

        var rowsAffected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return rowsAffected > 0;
    }

    // 7. lookup cepat vendor by lpcd
    public async Task<VendorByLpcdLookupResult?> GetVendorByLpcdAsync(
        string lpcd,
        CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM edcl.get_latest_gps_vendor_by_lpcd($1);";
        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, lpcd.Trim().ToUpperInvariant());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var vendorId = reader.HasColumn("VendorId") ? reader.GetSafeGuid("VendorId") : (reader.HasColumn("gpsvendorid") ? reader.GetSafeGuid("gpsvendorid") : Guid.Empty);
            var vendorName = reader.HasColumn("VendorName") ? reader.GetSafeString("VendorName") : (reader.HasColumn("gpsvendorname") ? reader.GetSafeString("gpsvendorname") : string.Empty);
            var vendorCode = reader.HasColumn("VendorCode") ? reader.GetSafeString("VendorCode") : (reader.HasColumn("gpsvendorcode") ? reader.GetSafeString("gpsvendorcode") : null);
            return new VendorByLpcdLookupResult(vendorId, vendorName, lpcd, vendorCode);
        }

        return null;
    }

    // 8. lookup cepat vendor by plat nomor
    public async Task<VendorByPlatNoLookupResult?> GetVendorByPlatNoAsync(
        string platNo,
        CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM edcl.get_latest_gps_vendor_by_plat_no($1);";
        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, platNo.Trim().ToUpperInvariant());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var vendorId = reader.HasColumn("gpsvendorid") ? reader.GetSafeGuid("gpsvendorid") : Guid.Empty;
            var vendorName = reader.HasColumn("vendorname") ? reader.GetSafeString("vendorname") : string.Empty;
            return new VendorByPlatNoLookupResult(vendorId, vendorName, platNo);
        }

        return null;
    }

    // 9. lookup cepat lpcd by nama vendor
    public async Task<LpcdByVendorNameLookupResult?> GetLpcdByVendorNameAsync(
        string vendorName,
        CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM edcl.get_latest_lpcd_by_gps_vendor_name($1);";
        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, vendorName.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var vendorId = reader.HasColumn("gpsvendorid") ? reader.GetSafeGuid("gpsvendorid") : Guid.Empty;
            var lpcd = reader.HasColumn("lpcd") ? reader.GetSafeString("lpcd") : string.Empty;
            return new LpcdByVendorNameLookupResult(vendorId, vendorName, lpcd);
        }

        return null;
    }

    // 10. kueri multi-filter dan paginasi posisi armada langsung dari postgresql (ado.net murni)
    public async Task<PaginatedPositionResponse> QueryPositionsFromDbAsync(
        LastPositionRequest param,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pageIndex = param.PageIndex <= 0 ? 0 : param.PageIndex - 1;
            var pageSize = param.PageSize <= 0 ? 100 : param.PageSize;

            const string sqlData = @"
                SELECT * FROM edcl.sp_edclgps1_get_last_positions(
                    $1, $2, $3, $4, $5, $6, $7, $8
                );";

            const string sqlCount = @"
                SELECT * FROM edcl.sp_edclgps1_get_last_positions_count(
                    $1, $2, $3, $4, $5, $6
                );";

            var positions = new List<LastPositionDto>();
            await using (var cmd = dataSource.CreateCommand(sqlData))
            {
                cmd.Parameters.AddWithValue(NpgsqlDbType.Uuid, param.GpsVendorId.HasValue && param.GpsVendorId.Value != Guid.Empty ? param.GpsVendorId.Value : DBNull.Value);
                cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, string.IsNullOrEmpty(param.VendorName) ? DBNull.Value : param.VendorName.ToLower());
                cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, string.IsNullOrEmpty(param.PlatNo) ? DBNull.Value : param.PlatNo.ToLower());
                cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, string.IsNullOrEmpty(param.DeviceId) ? DBNull.Value : param.DeviceId.ToLower());
                cmd.Parameters.AddWithValue(NpgsqlDbType.Timestamp, param.Datetime.HasValue ? param.Datetime.Value : DBNull.Value);
                cmd.Parameters.AddWithValue(NpgsqlDbType.Date, param.CreatedAt.HasValue ? param.CreatedAt.Value.Date : DBNull.Value);
                cmd.Parameters.AddWithValue(NpgsqlDbType.Integer, pageIndex);
                cmd.Parameters.AddWithValue(NpgsqlDbType.Integer, pageSize);
                cmd.CommandTimeout = 60;

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    positions.Add(reader.MapToLastPosition());
                }
            }

            long totalCount = 0;
            await using (var cmdCount = dataSource.CreateCommand(sqlCount))
            {
                cmdCount.Parameters.AddWithValue(NpgsqlDbType.Uuid, param.GpsVendorId.HasValue && param.GpsVendorId.Value != Guid.Empty ? param.GpsVendorId.Value : DBNull.Value);
                cmdCount.Parameters.AddWithValue(NpgsqlDbType.Varchar, string.IsNullOrEmpty(param.VendorName) ? DBNull.Value : param.VendorName.ToLower());
                cmdCount.Parameters.AddWithValue(NpgsqlDbType.Varchar, string.IsNullOrEmpty(param.PlatNo) ? DBNull.Value : param.PlatNo.ToLower());
                cmdCount.Parameters.AddWithValue(NpgsqlDbType.Varchar, string.IsNullOrEmpty(param.DeviceId) ? DBNull.Value : param.DeviceId.ToLower());
                cmdCount.Parameters.AddWithValue(NpgsqlDbType.Timestamp, param.Datetime.HasValue ? param.Datetime.Value : DBNull.Value);
                cmdCount.Parameters.AddWithValue(NpgsqlDbType.Date, param.CreatedAt.HasValue ? param.CreatedAt.Value.Date : DBNull.Value);
                cmdCount.CommandTimeout = 60;

                var scalar = await cmdCount.ExecuteScalarAsync(cancellationToken);
                if (scalar != null && scalar != DBNull.Value)
                {
                    totalCount = Convert.ToInt64(scalar);
                }
            }

            return new PaginatedPositionResponse(
                PageIndex: param.PageIndex <= 0 ? 1 : param.PageIndex,
                PageSize: pageSize,
                Count: totalCount,
                Data: positions
            );
        }
        catch (PostgresException ex)
        {
            logger.LogError(ex, "Gagal kueri posisi armada dari PostgreSQL via stored procedure sp_edclgps1_get_last_positions");
            throw new DeliveryProgressBadRequestException($"Gagal mengambil data posisi armada: {ex.MessageText}");
        }
    }

    // 11. ambil posisi armada terakhir berdasarkan plat nomor
    public async Task<LastPositionDto?> GetLastPositionByPlatNoFromDbAsync(
        string platNo,
        CancellationToken cancellationToken = default)
    {
        var result = await QueryPositionsFromDbAsync(
            new LastPositionRequest(PlatNo: platNo, PageIndex: 1, PageSize: 1),
            cancellationToken);

        return result.Data.FirstOrDefault();
    }

    // 12. daftarkan atau perbarui manifes sesi delivery aktif (Start Job)
    public async Task<DeliveryTrackingSession> StartDeliverySessionAsync(
        DeliveryTrackingSession session,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO edcl.t_delivery_tracking_session (
                id, delivery_no, lpcd, vendor_id, match_key, match_value,
                clean_match_value, driver_ktp, status, started_at, is_deleted, created_at, created_by
            ) VALUES (
                $1, $2, $3, $4, $5, $6, $7, $8, 'STARTED', CURRENT_TIMESTAMP, false, CURRENT_TIMESTAMP, 'EDCL_API'
            )
            ON CONFLICT (delivery_no)
            DO UPDATE SET
                lpcd              = EXCLUDED.lpcd,
                vendor_id         = EXCLUDED.vendor_id,
                match_key         = EXCLUDED.match_key,
                match_value       = EXCLUDED.match_value,
                clean_match_value = EXCLUDED.clean_match_value,
                driver_ktp        = COALESCE(EXCLUDED.driver_ktp, edcl.t_delivery_tracking_session.driver_ktp),
                status            = 'STARTED',
                started_at        = CURRENT_TIMESTAMP,
                arrived_at        = NULL,
                completed_at      = NULL,
                is_deleted        = false,
                last_modified     = CURRENT_TIMESTAMP,
                last_modified_by  = 'EDCL_API'
            RETURNING id, started_at;";

        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Uuid, session.Id == Guid.Empty ? Guid.NewGuid() : session.Id);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, session.DeliveryNo.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, session.Lpcd.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue(NpgsqlDbType.Uuid, (object?)session.VendorId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, session.MatchKey);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, session.MatchValue);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, session.CleanMatchValue);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, (object?)session.DriverKtp ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            session.Id = reader.GetGuid(0);
            session.StartedAt = reader.GetDateTime(1);
        }

        return session;
    }

    // 13. ambil manifes sesi delivery berdasarkan nomor delivery order
    public async Task<DeliveryTrackingSession?> GetDeliverySessionByDeliveryNoAsync(
        string deliveryNo,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT id, delivery_no, lpcd, vendor_id, match_key, match_value,
                   clean_match_value, driver_ktp, status, started_at, arrived_at,
                   destination_geofence_id, completed_at, completed_by, completion_source, is_deleted
            FROM edcl.t_delivery_tracking_session
            WHERE delivery_no = $1 AND is_deleted = false
            LIMIT 1;";

        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, deliveryNo.Trim().ToUpperInvariant());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return reader.MapToDeliveryTrackingSession();
        }

        return null;
    }

    // 14. selesaikan sesi delivery (Complete Job)
    public async Task<DeliveryTrackingSession?> CompleteDeliverySessionAsync(
        string deliveryNo,
        string? completedBy = null,
        string? source = null,
        decimal? latitude = null,
        decimal? longitude = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE edcl.t_delivery_tracking_session
            SET status               = 'COMPLETED',
                completed_at         = CURRENT_TIMESTAMP,
                completed_by         = $2,
                completion_source    = $3,
                last_modified        = CURRENT_TIMESTAMP,
                last_modified_by     = $2
            WHERE delivery_no = $1 AND is_deleted = false
            RETURNING id, delivery_no, lpcd, vendor_id, match_key, match_value,
                      clean_match_value, driver_ktp, status, started_at, arrived_at,
                      destination_geofence_id, completed_at, completed_by, completion_source, is_deleted;";

        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, deliveryNo.Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, (object?)completedBy ?? "System");
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, (object?)source ?? "MANUAL_MOBILE");

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return reader.MapToDeliveryTrackingSession();
        }

        return null;
    }

    // 15. ambil daftar sesi delivery aktif
    public async Task<IReadOnlyList<DeliveryTrackingSession>> GetActiveDeliverySessionsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT id, delivery_no, lpcd, vendor_id, match_key, match_value,
                   clean_match_value, driver_ktp, status, started_at, arrived_at,
                   destination_geofence_id, completed_at, completed_by, completion_source, is_deleted
            FROM edcl.t_delivery_tracking_session
            WHERE status IN ('STARTED', 'IN_TRANSIT') AND is_deleted = false
            ORDER BY started_at DESC
            LIMIT $1;";

        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Integer, limit <= 0 ? 100 : limit);

        var sessions = new List<DeliveryTrackingSession>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            sessions.Add(reader.MapToDeliveryTrackingSession());
        }

        return sessions;
    }

    // 16. ambil snapshot posisi terkini armada delivery dari database edcl.t_delivery_latest_position
    public async Task<DeliveryLatestPositionDto?> GetDeliveryLatestPositionAsync(
        string deliveryNo,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT delivery_no, session_id, vendor_id, plate_number, vehicle_id,
                   datetime, longitude, latitude, speed, course, street_name, updated_at
            FROM edcl.t_delivery_latest_position
            WHERE delivery_no = $1;";

        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, deliveryNo.Trim().ToUpperInvariant());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new DeliveryLatestPositionDto(
                DeliveryNo: reader.GetString(reader.GetOrdinal("delivery_no")),
                SessionId: reader.GetGuid(reader.GetOrdinal("session_id")),
                VendorId: reader.IsDBNull(reader.GetOrdinal("vendor_id")) ? null : reader.GetGuid(reader.GetOrdinal("vendor_id")),
                PlateNumber: reader.GetString(reader.GetOrdinal("plate_number")),
                VehicleId: reader.IsDBNull(reader.GetOrdinal("vehicle_id")) ? null : reader.GetString(reader.GetOrdinal("vehicle_id")),
                Datetime: reader.GetDateTime(reader.GetOrdinal("datetime")),
                Longitude: reader.GetDecimal(reader.GetOrdinal("longitude")),
                Latitude: reader.GetDecimal(reader.GetOrdinal("latitude")),
                Speed: reader.GetDecimal(reader.GetOrdinal("speed")),
                Course: reader.GetDecimal(reader.GetOrdinal("course")),
                StreetName: reader.IsDBNull(reader.GetOrdinal("street_name")) ? null : reader.GetString(reader.GetOrdinal("street_name")),
                UpdatedAt: reader.GetDateTime(reader.GetOrdinal("updated_at"))
            );
        }

        return null;
    }

    // 17. ambil riwayat log correlation miss / data mismatch terkini dari edcl.m_gps_api_log
    public async Task<GpsCorrelationMissLogDto?> GetLatestCorrelationMissLogAsync(
        string deliveryNo,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT status, error_message, error_response, created_at
            FROM edcl.m_gps_api_log
            WHERE function_name = 'DELIVERY_CORRELATION_MISS' AND parameter LIKE '%' || $1 || '%'
            ORDER BY created_at DESC
            LIMIT 1;";

        await using var cmd = dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Varchar, deliveryNo.Trim().ToUpperInvariant());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new GpsCorrelationMissLogDto(
                Status: reader.IsDBNull(reader.GetOrdinal("status")) ? "WARNING" : reader.GetString(reader.GetOrdinal("status")),
                ErrorMessage: reader.IsDBNull(reader.GetOrdinal("error_message")) ? null : reader.GetString(reader.GetOrdinal("error_message")),
                ErrorResponse: reader.IsDBNull(reader.GetOrdinal("error_response")) ? null : reader.GetString(reader.GetOrdinal("error_response")),
                CreatedAt: reader.GetDateTime(reader.GetOrdinal("created_at"))
            );
        }

        return null;
    }
}

