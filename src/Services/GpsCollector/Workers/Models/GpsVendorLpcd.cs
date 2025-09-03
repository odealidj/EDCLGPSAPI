using System.Text.Json.Serialization;

namespace GeofenceWorker.Workers.Models;

public class GpsVendorLpcd : Entity<Guid>
{
    
    public Guid GpsVendorId { get; set; }
    public string Lpcd { get; set; } = string.Empty;
    
    internal GpsVendorLpcd(Guid gpsVendorId, string lpcd)
    {
        GpsVendorId = gpsVendorId;
        Lpcd = lpcd; 
    }

    [JsonConstructor]
    public GpsVendorLpcd(Guid id, Guid gpsVendorId, string lpcd)
    {
        Id = id;
        GpsVendorId = gpsVendorId;
        Lpcd = lpcd;
    }

    [JsonConstructor]
    public GpsVendorLpcd()
    {
    }
}