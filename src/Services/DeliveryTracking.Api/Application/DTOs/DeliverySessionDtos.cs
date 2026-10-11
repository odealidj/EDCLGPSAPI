using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DeliveryTracking.Application.DTOs;

// request payload untuk endpoint Start Job (dipanggil oleh EDCL_API saat supir klik "Start Job")
public class StartDeliveryJobRequestDto
{
    [Required]
    [JsonPropertyName("deliveryNo")]
    public string DeliveryNo { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("lpcd")]
    public string Lpcd { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("match_key")]
    public string? MatchKey { get; set; }

    [JsonPropertyName("val")]
    public string? Val { get; set; }

    // Alias pendukung jika EDCL_API mengirim "value" alih-alih "val"
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("match_value")]
    public string? MatchValue { get; set; }

    [JsonPropertyName("driverKtp")]
    public string? DriverKtp { get; set; }

    [JsonIgnore]
    public string EffectiveKey
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(MatchKey)) return MatchKey;
            if (!string.IsNullOrWhiteSpace(Key)) return Key;
            return "PlatNo";
        }
    }

    [JsonIgnore]
    public string EffectiveValue
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(MatchValue)) return MatchValue;
            if (!string.IsNullOrWhiteSpace(Val)) return Val;
            return Value ?? string.Empty;
        }
    }

    public string GetEffectiveKey() => EffectiveKey;

    public string GetEffectiveValue() => EffectiveValue;
}

// respon hasil eksekusi Start Job
public record StartDeliveryJobResultDto(
    int Status,
    string Message,
    string DeliveryNo,
    string Lpcd,
    string? VendorCode,
    string Key,
    string Val,
    string CleanValue,
    Guid? VendorId,
    string? VendorName,
    DateTime StartedAt
);

// request payload untuk Complete Job
public class CompleteDeliveryJobRequestDto
{
    [Required]
    [JsonPropertyName("deliveryNo")]
    public string DeliveryNo { get; set; } = string.Empty;

    [JsonPropertyName("completedBy")]
    public string? CompletedBy { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; } = "MANUAL_MOBILE";

    [JsonPropertyName("latitude")]
    public decimal? Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public decimal? Longitude { get; set; }
}

// respon hasil eksekusi Complete Job
public record CompleteDeliveryJobResultDto(
    int Status,
    string Message,
    string DeliveryNo,
    DateTime CompletedAt
);

// representasi DTO sesi pelacakan pengiriman aktif
public record DeliveryTrackingSessionDto(
    Guid Id,
    string DeliveryNo,
    string Lpcd,
    Guid? VendorId,
    string? VendorName,
    string Key,
    string Val,
    string CleanValue,
    string? DriverKtp,
    string Status,
    DateTime StartedAt,
    DateTime? ArrivedAt,
    DateTime? CompletedAt
);

// snapshot posisi terkini armada pengiriman dari database edcl.t_delivery_latest_position
public record DeliveryLatestPositionDto(
    string DeliveryNo,
    Guid SessionId,
    Guid? VendorId,
    string PlateNumber,
    string? VehicleId,
    DateTime Datetime,
    decimal Longitude,
    decimal Latitude,
    decimal Speed,
    decimal Course,
    string? StreetName,
    DateTime UpdatedAt
);

// catatan audit log jika terjadi correlation miss / mismatch dari edcl.m_gps_api_log
public record GpsCorrelationMissLogDto(
    string Status,
    string? ErrorMessage,
    string? ErrorResponse,
    DateTime CreatedAt
);

// DTO lengkap status live tracking untuk Web Portal Operation EDCL
public record DeliveryLiveTrackingDto(
    string DeliveryNo,
    string TrackingStatus, // TRACKING_ACTIVE, DATA_MISMATCH, VENDOR_CONNECTION_ERROR, WAITING_TELEMETRY, COMPLETED
    string? StatusMessage,
    string? Lpcd,
    string? VendorCode,
    string? VendorName,
    string? PlatNo,
    string? DeviceId,
    string? MatchKey,
    string? MatchValue,
    decimal? Latitude,
    decimal? Longitude,
    decimal? Speed,
    decimal? Course,
    string? StreetName,
    DateTime? GpsDatetime,
    DateTime? UpdatedAt,
    string DataSource // "Redis" atau "PostgreSQL"
);

// helper utilitas validasi dan pembersihan key matching
public static class DeliveryValidator
{
    private static readonly HashSet<string> WhitelistKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "PlatNo",
        "VehicleId",
        "Imei",
        "DriverKtp",
        "DeliveryNo"
    };

    public static bool IsValidKey(string? key) =>
        string.IsNullOrWhiteSpace(key) || WhitelistKeys.Contains(key.Trim());

    public static string CleanKey(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        return Regex.Replace(input.ToUpperInvariant(), @"[\s\-_]+", "", RegexOptions.None, TimeSpan.FromSeconds(1));
    }
}

// Event DTO yang dipublish ke edcl.delivery (routing key: delivery.status.delivered) saat geofence tiba di tujuan
public record DeliveryDeliveredEventDto(
    string DeliveryNo,
    string Status,
    string? PlatNo,
    string? Lpcd,
    Guid? DestinationGeofenceId,
    string? DestinationName,
    DateTime DeliveredAt,
    decimal? Latitude,
    decimal? Longitude
);
