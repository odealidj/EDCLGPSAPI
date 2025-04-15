namespace GeofenceWorker.Workers.Models;

public class GpsLastPositionH : Entity<Guid>
{
    public Guid GpsVendorId { get; set; }
}