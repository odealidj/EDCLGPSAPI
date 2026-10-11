using System.Data.Common;
using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Domain.Entities;
using Npgsql;

namespace DeliveryTracking.Infrastructure.Persistence;

// helper ekstensi pembaca data reader npgsql agar aman dari null dan bebas refleksi (aot-ready)
public static class DataReaderExtensions
{
    public static string GetSafeString(this DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    }

    public static string? GetNullableString(this DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    public static Guid GetSafeGuid(this DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? Guid.Empty : reader.GetGuid(ordinal);
    }

    public static Guid? GetNullableGuid(this DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
    }

    public static DateTime? GetNullableDateTime(this DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    public static decimal? GetNullableDecimal(this DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }

    public static int GetSafeInt32(this DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
    }

    public static bool HasColumn(this DbDataReader reader, string columnName)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // pemetaan statis koordinat gps dari data reader stored procedure
    public static TrackDeliveryPointDto MapToTrackDeliveryPoint(this NpgsqlDataReader reader)
    {
        return new TrackDeliveryPointDto
        {
            PositionId = reader.HasColumn("positionid") ? reader.GetSafeInt32("positionid") : 0,
            DeliveryNo = reader.HasColumn("deliveryno") ? reader.GetSafeString("deliveryno") : string.Empty,
            NoKtp = reader.HasColumn("noktp") ? reader.GetSafeString("noktp") 
                  : (reader.HasColumn("driverktp") ? reader.GetSafeString("driverktp") : string.Empty),
            Lpcd = reader.HasColumn("lpcd") ? reader.GetSafeString("lpcd") : string.Empty,
            GpsVendor = reader.HasColumn("gpsvendor") ? reader.GetSafeString("gpsvendor") 
                      : (reader.HasColumn("vendorname") ? reader.GetSafeString("vendorname") : string.Empty),
            FlagGps = reader.HasColumn("flaggps") ? reader.GetSafeString("flaggps") : string.Empty,
            PlatNo = reader.HasColumn("platno") ? reader.GetSafeString("platno") 
                   : (reader.HasColumn("platenumber") ? reader.GetSafeString("platenumber") : string.Empty),
            DeviceId = reader.HasColumn("deviceid") ? reader.GetSafeString("deviceid") : string.Empty,
            Datetime = reader.HasColumn("datetime") ? reader.GetNullableDateTime("datetime") : null,
            X = reader.HasColumn("x") ? reader.GetNullableDecimal("x") 
              : (reader.HasColumn("longitude") ? reader.GetNullableDecimal("longitude") : null),
            Y = reader.HasColumn("y") ? reader.GetNullableDecimal("y") 
              : (reader.HasColumn("latitude") ? reader.GetNullableDecimal("latitude") : null),
            Speed = reader.HasColumn("speed") ? reader.GetNullableDecimal("speed") : null,
            Course = reader.HasColumn("course") ? reader.GetNullableDecimal("course") : null,
            StreetName = reader.HasColumn("streetname") ? reader.GetSafeString("streetname") : string.Empty
        };
    }

    // pemetaan statis progres delivery dari tabel tb_r_delivery_progress
    public static DeliveryProgress MapToDeliveryProgress(this NpgsqlDataReader reader)
    {
        var progress = new DeliveryProgress
        {
            Id = reader.GetSafeGuid("id"),
            DeliveryNo = reader.GetSafeString("delivery_no"),
            PlatNo = reader.GetSafeString("plat_no"),
            NoKtp = reader.GetSafeString("no_ktp"),
            VendorName = reader.GetNullableString("vendor_name"),
            Lpcd = reader.GetNullableString("lpcd")
        };
        return progress;
    }

    // pemetaan statis posisi armada dari data reader postgresql (aot-ready tanpa refleksi)
    public static LastPositionDto MapToLastPosition(this NpgsqlDataReader reader)
    {
        return new LastPositionDto
        {
            Id = reader.HasColumn("id") ? reader.GetSafeGuid("id") : Guid.NewGuid(),
            GpsVendorId = reader.HasColumn("gps_vendor_id") ? reader.GetSafeGuid("gps_vendor_id") 
                        : (reader.HasColumn("gpsvendorid") ? reader.GetSafeGuid("gpsvendorid") : Guid.Empty),
            VendorName = reader.HasColumn("vendor_name") ? reader.GetSafeString("vendor_name") 
                        : (reader.HasColumn("vendorname") ? reader.GetSafeString("vendorname") : string.Empty),
            PlatNo = (reader.HasColumn("plat_no") ? reader.GetNullableString("plat_no") 
                    : (reader.HasColumn("platno") ? reader.GetNullableString("platno") : null))?.Trim().ToUpperInvariant(),
            DeviceId = reader.HasColumn("device_id") ? reader.GetNullableString("device_id") 
                      : (reader.HasColumn("deviceid") ? reader.GetNullableString("deviceid") : null),
            Datetime = reader.HasColumn("datetime") ? (reader.GetNullableDateTime("datetime") ?? DateTime.UtcNow) : DateTime.UtcNow,
            X = reader.HasColumn("x") ? reader.GetNullableDecimal("x") 
                : (reader.HasColumn("longitude") ? reader.GetNullableDecimal("longitude") : null),
            Y = reader.HasColumn("y") ? reader.GetNullableDecimal("y") 
                : (reader.HasColumn("latitude") ? reader.GetNullableDecimal("latitude") : null),
            Speed = reader.HasColumn("speed") ? reader.GetNullableDecimal("speed") : null,
            Course = reader.HasColumn("course") ? reader.GetNullableDecimal("course") : null,
            StreetName = reader.HasColumn("street_name") ? reader.GetNullableString("street_name") 
                        : (reader.HasColumn("streetname") ? reader.GetNullableString("streetname") : null),
            CreatedAt = reader.HasColumn("created_at") ? (reader.GetNullableDateTime("created_at") ?? DateTime.UtcNow) : DateTime.UtcNow,
            DataSource = "PostgreSQL"
        };
    }

    // pemetaan manifes sesi delivery dari data reader postgresql
    public static DeliveryTrackingSession MapToDeliveryTrackingSession(this DbDataReader reader)
    {
        return new DeliveryTrackingSession
        {
            Id = reader.GetSafeGuid("id"),
            DeliveryNo = reader.GetSafeString("delivery_no"),
            Lpcd = reader.GetSafeString("lpcd"),
            VendorId = reader.GetNullableGuid("vendor_id"),
            MatchKey = reader.HasColumn("match_key") ? reader.GetSafeString("match_key") : "PlatNo",
            MatchValue = reader.HasColumn("match_value") ? reader.GetSafeString("match_value") : string.Empty,
            CleanMatchValue = reader.HasColumn("clean_match_value") ? reader.GetSafeString("clean_match_value") : string.Empty,
            DriverKtp = reader.HasColumn("driver_ktp") ? reader.GetNullableString("driver_ktp") : null,
            Status = reader.HasColumn("status") ? reader.GetSafeString("status") : "STARTED",
            StartedAt = reader.HasColumn("started_at") ? (reader.GetNullableDateTime("started_at") ?? DateTime.UtcNow) : DateTime.UtcNow,
            ArrivedAt = reader.HasColumn("arrived_at") ? reader.GetNullableDateTime("arrived_at") : null,
            DestinationGeofenceId = reader.HasColumn("destination_geofence_id") ? reader.GetNullableGuid("destination_geofence_id") : null,
            CompletedAt = reader.HasColumn("completed_at") ? reader.GetNullableDateTime("completed_at") : null,
            CompletedBy = reader.HasColumn("completed_by") ? reader.GetNullableString("completed_by") : null,
            CompletionSource = reader.HasColumn("completion_source") ? reader.GetNullableString("completion_source") : null,
            IsDeleted = reader.HasColumn("is_deleted") && reader.GetBoolean(reader.GetOrdinal("is_deleted"))
        };
    }
}

