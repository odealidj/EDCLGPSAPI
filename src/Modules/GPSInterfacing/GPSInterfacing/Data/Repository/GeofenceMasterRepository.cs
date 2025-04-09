namespace GPSInterfacing.Data.Repository;

public class GeofenceMasterRepository(GPSInterfacingDbContext dbContext)
    : IGeofenceMasterRepository
{
    public async Task<GpsVendor> CreateBasket(GpsVendor gpsVendor, CancellationToken cancellationToken = default)
    {
        dbContext.GpsVendors.Add(gpsVendor);
        await dbContext.SaveChangesAsync(cancellationToken);
        return gpsVendor;
    }

    public async Task<int> SaveChangesAsync(string? userName = null, CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken);
    }
}