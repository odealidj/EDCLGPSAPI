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
    public string Key { get; set; } = "PlatNo";

    [JsonPropertyName("val")]
    public string Val { get; set; } = string.Empty;

    // Alias pendukung jika EDCL_API mengirim "value" alih-alih "val"
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("driverKtp")]
    public string? DriverKtp { get; set; }

    [JsonIgnore]
    public string EffectiveValue => !string.IsNullOrWhiteSpace(Val) ? Val : (Value ?? string.Empty);

    public string GetEffectiveValue() => EffectiveValue;
}

// respon hasil eksekusi Start Job
public record StartDeliveryJobResultDto(
    int Status,
    string Message,
    string DeliveryNo,
    string Lpcd,
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
        return Regex.Replace(input.ToUpperInvariant(), @"[\s\-_]+", "");
    }
}
