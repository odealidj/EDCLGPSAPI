namespace GPSInterfacing.Data.Repository;

public interface IGeofenceMasterRepository
{
    Task<GpsVendor> CreateBasket(GpsVendor gpsVendor, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(string? userName = null, CancellationToken cancellationToken = default);

}