namespace VendorMaster.Domain.Entities;

// pemetaan dinamis atribut respons vendor ke field edcl (tabel m_gps_vendor_mapping)
public class GpsVendorMapping : Entity<int>
{
    public Guid VendorId { get; set; }
    public string ResponseField { get; set; } = string.Empty;
    public string MappedField { get; set; } = string.Empty;
    public string DataType { get; set; } = VendorDefaults.DefaultMappingDataType;

    // navigasi ke parent vendor
    public GpsVendor? GpsVendor { get; set; }
}
