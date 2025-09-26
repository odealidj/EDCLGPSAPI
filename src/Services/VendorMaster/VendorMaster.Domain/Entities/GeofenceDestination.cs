namespace VendorMaster.Domain.Entities;

// master area tujuan / plant geofence toyota tmmin (tabel m_geofence_destination)
public class GeofenceDestination : Entity<Guid>
{
    public string DestinationCode { get; set; } = string.Empty;
    public string DestinationName { get; set; } = string.Empty;
    public string GeofenceType { get; set; } = VendorDefaults.DefaultGeofenceType;
    public decimal CenterLatitude { get; set; }
    public decimal CenterLongitude { get; set; }
    public int RadiusMeters { get; set; } = VendorDefaults.DefaultGeofenceRadiusMeters;
    public JsonObject? PolygonCoordinates { get; set; }
    public bool IsActive { get; set; } = true;
}
