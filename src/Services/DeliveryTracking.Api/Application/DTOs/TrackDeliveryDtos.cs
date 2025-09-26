using System.Text.Json.Serialization;

namespace DeliveryTracking.Application.DTOs;

// request dto untuk tracking jejak gps pengiriman (aot-ready record)
public record TrackDeliveryRequestDto(
    string DeliveryNo,
    int Density = 1
);

// response dto koordinat gps jejak pengiriman per titik (aot-ready record)
public record TrackDeliveryPointDto
{
    public int PositionId { get; init; }
    public string DeliveryNo { get; init; } = string.Empty;
    public string NoKtp { get; init; } = string.Empty;
    public string Lpcd { get; init; } = string.Empty;
    public string GpsVendor { get; init; } = string.Empty;
    public string FlagGps { get; init; } = string.Empty;
    public string PlatNo { get; init; } = string.Empty;
    public string DeviceId { get; init; } = string.Empty;
    public DateTime? Datetime { get; init; }
    public decimal? X { get; init; } // latitude
    public decimal? Y { get; init; } // longitude
    public decimal? Speed { get; init; }
    public decimal? Course { get; init; }
    public string StreetName { get; init; } = string.Empty;
}
