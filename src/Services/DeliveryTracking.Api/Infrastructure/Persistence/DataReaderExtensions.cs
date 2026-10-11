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

    public static string GetFirstString(this DbDataReader reader, string col1, string col2, string defaultValue = "")
    {
        if (reader.HasColumn(col1)) return reader.GetSafeString(col1);
        if (reader.HasColumn(col2)) return reader.GetSafeString(col2);
        return defaultValue;
    }

    public static string? GetFirstNullableString(this DbDataReader reader, string col1, string col2)
    {
        if (reader.HasColumn(col1)) return reader.GetNullableString(col1);
        if (reader.HasColumn(col2)) return reader.GetNullableString(col2);
        return null;
    }

    public static Guid GetFirstGuid(this DbDataReader reader, string col1, string col2)
    {
        if (reader.HasColumn(col1)) return reader.GetSafeGuid(col1);
        if (reader.HasColumn(col2)) return reader.GetSafeGuid(col2);
        return Guid.Empty;
    }

    public static decimal? GetFirstNullableDecimal(this DbDataReader reader, string col1, string col2)
    {
        if (reader.HasColumn(col1)) return reader.GetNullableDecimal(col1);
        if (reader.HasColumn(col2)) return reader.GetNullableDecimal(col2);
        return null;
    }

    public static string GetSafeStringOrDefault(this DbDataReader reader, string columnName, string defaultValue = "") =>
        reader.HasColumn(columnName) ? reader.GetSafeString(columnName) : defaultValue;

    public static string? GetNullableStringOrDefault(this DbDataReader reader, string columnName, string? defaultValue = null) =>
        reader.HasColumn(columnName) ? reader.GetNullableString(columnName) : defaultValue;

    public static int GetSafeInt32OrDefault(this DbDataReader reader, string columnName, int defaultValue = 0) =>
        reader.HasColumn(columnName) ? reader.GetSafeInt32(columnName) : defaultValue;

    public static decimal? GetNullableDecimalOrDefault(this DbDataReader reader, string columnName, decimal? defaultValue = null) =>
        reader.HasColumn(columnName) ? reader.GetNullableDecimal(columnName) : defaultValue;

    public static DateTime? GetNullableDateTimeOrDefault(this DbDataReader reader, string columnName, DateTime? defaultValue = null) =>
        reader.HasColumn(columnName) ? reader.GetNullableDateTime(columnName) : defaultValue;

    public static DateTime GetSafeDateTimeOrDefault(this DbDataReader reader, string columnName, DateTime defaultValue) =>
        (reader.HasColumn(columnName) ? reader.GetNullableDateTime(columnName) : null) ?? defaultValue;

    public static Guid GetSafeGuidOrDefault(this DbDataReader reader, string columnName, Guid defaultValue) =>
        reader.HasColumn(columnName) ? reader.GetSafeGuid(columnName) : defaultValue;

    public static Guid? GetNullableGuidOrDefault(this DbDataReader reader, string columnName, Guid? defaultValue = null) =>
        reader.HasColumn(columnName) ? reader.GetNullableGuid(columnName) : defaultValue;

    public static bool GetBooleanOrDefault(this DbDataReader reader, string columnName, bool defaultValue = false)
    {
        if (!reader.HasColumn(columnName)) return defaultValue;
        var ordinal = reader.GetOrdinal(columnName);
        return !reader.IsDBNull(ordinal) && reader.GetBoolean(ordinal);
    }

    // pemetaan statis koordinat gps dari data reader stored procedure
    public static TrackDeliveryPointDto MapToTrackDeliveryPoint(this NpgsqlDataReader reader)
    {
        return new TrackDeliveryPointDto
        {
            PositionId = reader.GetSafeInt32OrDefault("positionid"),
            DeliveryNo = reader.GetSafeStringOrDefault("deliveryno"),
            NoKtp = reader.GetFirstString("noktp", "driverktp"),
            Lpcd = reader.GetSafeStringOrDefault("lpcd"),
            GpsVendor = reader.GetFirstString("gpsvendor", "vendorname"),
            FlagGps = reader.GetSafeStringOrDefault("flaggps"),
            PlatNo = reader.GetFirstString("platno", "platenumber"),
            DeviceId = reader.GetSafeStringOrDefault("deviceid"),
            Datetime = reader.GetNullableDateTimeOrDefault("datetime"),
            X = reader.GetFirstNullableDecimal("x", "longitude"),
            Y = reader.GetFirstNullableDecimal("y", "latitude"),
            Speed = reader.GetNullableDecimalOrDefault("speed"),
            Course = reader.GetNullableDecimalOrDefault("course"),
            StreetName = reader.GetSafeStringOrDefault("streetname")
        };
    }

    // pemetaan statis progres delivery dari tabel tb_r_delivery_progress
    public static DeliveryProgress MapToDeliveryProgress(this NpgsqlDataReader reader)
    {
        return new DeliveryProgress
        {
            Id = reader.GetSafeGuid("id"),
            DeliveryNo = reader.GetSafeString("delivery_no"),
            PlatNo = reader.GetSafeString("plat_no"),
            NoKtp = reader.GetSafeString("no_ktp"),
            VendorName = reader.GetNullableString("vendor_name"),
            Lpcd = reader.GetNullableString("lpcd")
        };
    }

    // pemetaan statis posisi armada dari data reader postgresql (aot-ready tanpa refleksi)
    public static LastPositionDto MapToLastPosition(this NpgsqlDataReader reader)
    {
        var rawPlatNo = reader.GetFirstNullableString("plat_no", "platno");
        return new LastPositionDto
        {
            Id = reader.GetSafeGuidOrDefault("id", Guid.NewGuid()),
            GpsVendorId = reader.GetFirstGuid("gps_vendor_id", "gpsvendorid"),
            VendorName = reader.GetFirstString("vendor_name", "vendorname"),
            PlatNo = rawPlatNo?.Trim().ToUpperInvariant(),
            DeviceId = reader.GetFirstNullableString("device_id", "deviceid"),
            Datetime = reader.GetSafeDateTimeOrDefault("datetime", DateTime.UtcNow),
            X = reader.GetFirstNullableDecimal("x", "longitude"),
            Y = reader.GetFirstNullableDecimal("y", "latitude"),
            Speed = reader.GetNullableDecimalOrDefault("speed"),
            Course = reader.GetNullableDecimalOrDefault("course"),
            StreetName = reader.GetFirstNullableString("street_name", "streetname"),
            CreatedAt = reader.GetSafeDateTimeOrDefault("created_at", DateTime.UtcNow),
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
            MatchKey = reader.GetSafeStringOrDefault("match_key", "PlatNo"),
            MatchValue = reader.GetSafeStringOrDefault("match_value"),
            CleanMatchValue = reader.GetSafeStringOrDefault("clean_match_value"),
            DriverKtp = reader.GetNullableStringOrDefault("driver_ktp"),
            Status = reader.GetSafeStringOrDefault("status", "STARTED"),
            StartedAt = reader.GetSafeDateTimeOrDefault("started_at", DateTime.UtcNow),
            ArrivedAt = reader.GetNullableDateTimeOrDefault("arrived_at"),
            DestinationGeofenceId = reader.GetNullableGuidOrDefault("destination_geofence_id"),
            CompletedAt = reader.GetNullableDateTimeOrDefault("completed_at"),
            CompletedBy = reader.GetNullableStringOrDefault("completed_by"),
            CompletionSource = reader.GetNullableStringOrDefault("completion_source"),
            IsDeleted = reader.GetBooleanOrDefault("is_deleted")
        };
    }
}
