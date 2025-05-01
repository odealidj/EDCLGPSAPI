using GeofenceWorker.Workers.Models;

namespace GeofenceWorker.Data.Repository.IRepository;

public interface IGpsLastPositionHRepository
{
    Task<GpsVendorEndpoint?> GetEndPointByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> UpdateVarParamsPropertyRawSqlAsync(Guid endpointId, string propertyName, object newValue);
    Task<bool> UpdateVarParamsAsync(GpsVendorEndpoint endpoint, CancellationToken cancellationToken = default);
    Task<GpsLastPositionH> InsertGpsLastPositionH(GpsLastPositionH gpsLastPositionH, CancellationToken cancellationToken = default);
}