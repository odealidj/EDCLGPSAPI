using GeofenceMaster.GeofenceMaster.Models;

namespace GeofenceMaster.Data.Repository.IRepository;

public interface IGeofenceMasterRepository
{
    Task<GpsVendor> CreateGeofenceMaster(GpsVendor gpsVendor, CancellationToken cancellationToken = default);

    Task<IEnumerable<GpsVendor>>GetGeofenceMaster(string? vendorName, int pageIndex, int pageSize,  bool asNoTracking = true, CancellationToken cancellationToken = default);
    Task<int> GetGeofenceMasterCount(string? vendorName, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(string? userName = null, CancellationToken cancellationToken = default);

}