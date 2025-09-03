using GeofenceWorker.Workers.Models;

namespace GeofenceWorker.Data.Repository.IRepository;

public interface IGpsApiLogRepository
{
    Task<int> InsertGpsApiLog(GpsApiLog gpsApiLog, CancellationToken cancellationToken = default);
}