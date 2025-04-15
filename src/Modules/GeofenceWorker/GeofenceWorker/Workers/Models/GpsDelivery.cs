namespace GeofenceWorker.Workers.Models;

public class GpsDelivery: Entity<Guid>
{
    public Guid? GpsLastPositionIdH { get; set; } = Guid.Empty;
    public string? Lpcd { get; set; }   // LPCD ID dari GPS Vendor
    public string DeliveryNo { get; set; } = string.Empty; 
    public string NoKtp { get; set; }  = string.Empty;
}
