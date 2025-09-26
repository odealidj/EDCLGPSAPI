using System.Text.Json.Serialization;

namespace DeliveryTracking.Application.DTOs;

// dto status progres pengiriman delivery (aot-ready)
public record DeliveryProgressDto
{
    public Guid? Id { get; init; }
    public string DeliveryNo { get; init; } = string.Empty;
    public string PlatNo { get; init; } = string.Empty;
    public string NoKtp { get; init; } = string.Empty;

    [JsonPropertyName("gpsVendor")]
    public string? VendorName { get; init; }

    public string? Lpcd { get; init; }
    public DateTime? CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? LastModified { get; init; }
    public string? LastModifiedBy { get; init; }
}

// dto hasil operasi upsert / delete progress
public record SaveDeliveryProgressResult(Guid Id, string DeliveryNo, bool IsCreated);
public record DeleteDeliveryProgressResult(bool IsSuccess, string DeliveryNo);
