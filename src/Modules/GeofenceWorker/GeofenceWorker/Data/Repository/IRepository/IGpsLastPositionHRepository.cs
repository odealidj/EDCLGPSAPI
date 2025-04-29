using GeofenceWorker.Workers.Models;

namespace GeofenceWorker.Data.Repository.IRepository;

public interface IGpsLastPositionHRepository
{
    Task<GpsLastPositionH> InsertGpsLastPositionH(GpsLastPositionH gpsLastPositionH, bool asNoTracking = true, CancellationToken cancellationToken = default);
}