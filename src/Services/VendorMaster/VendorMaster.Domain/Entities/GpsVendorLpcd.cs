namespace VendorMaster.Domain.Entities;

// pemetaan unit kode armada toyota (lpcd) ke vendor gps (tabel m_gps_vendor_lpcd)
public class GpsVendorLpcd : Entity<Guid>
{
    public Guid VendorId { get; set; }
    public string Lpcd { get; set; } = string.Empty;

    // navigasi ke parent vendor
    public GpsVendor? GpsVendor { get; set; }
}
