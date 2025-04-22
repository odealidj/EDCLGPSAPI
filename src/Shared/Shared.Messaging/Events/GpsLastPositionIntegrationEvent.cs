namespace Shared.Messaging.Events;

public record GpsLastPositionIntegrationEvent: IntegrationEvent
{
    public Guid Id { get; set; }
    public Guid GpsVendorId { get; set; }
    public string? Lpcd { get; set; }   // LPCD ID dari GPS Vendor
    public string? PlatNo { get; set; } // Nomor Plat Kendaraan
    public string? DeviceId { get; set; } // TRUCKID dari GPS Vendor
    public DateTime Datetime { get; set; } // Waktu dari GPS Vendor
    public decimal? X { get; set; } // Koordinat X dari GPS Vendor
    public decimal? Y { get; set; } // Koordinat Y dari GPS Vendor
    public decimal? Speed { get; set; } // Kecepatan dari GPS Vendor
    public decimal? Course { get; set; } // Arah dari GPS Vendor
    public string? StreetName { get; set; } // Nama Jalan dari GPS Vendor
}