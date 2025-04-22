using GeofenceWorker.Events;

namespace GeofenceWorker.Workers.Models;

public class GpsLastPosition: Entity<Guid>
{
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
    
    public static GpsLastPosition Create(
        Guid id, 
        Guid gpsVendorId,
        string? lpcd, 
        string platNo, 
        string deviceId, 
        DateTime datetime, 
        decimal? x,
        decimal? y,
        decimal? speed,
        decimal? course,
        string streetName
        )
    {
        //ArgumentException.ThrowIfNullOrEmpty(id);
        //ArgumentOutOfRangeException.ThrowIfNegativeOrZero(x);

        var gpsLastPosition = new GpsLastPosition
        {
            Id = id,
            GpsVendorId = gpsVendorId,
            Lpcd = lpcd,
            PlatNo = platNo,
            DeviceId = deviceId,
            Datetime = datetime,
            X = x,
            Y = y,
            Speed = speed,
            Course = course,
            StreetName = streetName
        };
           
        ////gpsLastPosition.AddDomainEvent(new GpsLastPositionCreatedEvent(gpsLastPosition));

        return gpsLastPosition;
    }
    
   
}