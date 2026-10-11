namespace DeliveryTracking.Domain.Entities;

// entitas titik telemetri gps pengiriman (tabel tb_r_gps_delivery)
public class GpsDelivery : Entity<Guid>
{
    public Guid GpsVendorId { get; set; }
    public string GpsVendorName { get; set; } = string.Empty;
    public Guid? GpsDeliveryHId { get; set; }
    public Guid? LpcdId { get; set; }
    public string? Lpcd { get; set; }
    public string DeliveryNo { get; set; } = string.Empty;
    public string? NoKtp { get; set; }
    public string? PlatNo { get; set; }
    public string? DeviceId { get; set; }
    public DateTime? Datetime { get; set; }
    public decimal? X { get; set; } // latitude
    public decimal? Y { get; set; } // longitude
    public decimal? Speed { get; set; }
    public decimal? Course { get; set; }
    public string? StreetName { get; set; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "Domain factory method mapping telemetry columns")]
    public static GpsDelivery Create(
        Guid id,
        Guid gpsVendorId,
        string gpsVendorName,
        string deliveryNo,
        string? platNo,
        string? lpcd,
        DateTime? datetime,
        decimal? x,
        decimal? y,
        decimal? speed,
        decimal? course,
        string? streetName,
        string? deviceId = null,
        string? noKtp = null)
    {
        return new GpsDelivery
        {
            Id = id == Guid.Empty ? Guid.NewGuid() : id,
            GpsVendorId = gpsVendorId,
            GpsVendorName = gpsVendorName,
            DeliveryNo = deliveryNo.Trim().ToUpperInvariant(),
            PlatNo = platNo?.Trim().ToUpperInvariant(),
            Lpcd = lpcd?.Trim().ToUpperInvariant(),
            Datetime = datetime ?? DateTime.UtcNow,
            X = x,
            Y = y,
            Speed = speed,
            Course = course,
            StreetName = streetName,
            DeviceId = deviceId,
            NoKtp = noKtp
        };
    }
}
