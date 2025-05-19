using Delivery.Delivery.Dtos;

namespace Delivery.Data.Repositories.IRepositories;

public interface IDeliveryDapperRepository
{
    Task<IEnumerable<TrackDeliveryEdclResponseDto>>GetTrackDeliveryAsync( TrackDeliveryEdclRequestDto param ,  bool asNoTracking = true, CancellationToken cancellationToken = default);
    Task<OutGetGpsVendorByLpcd?>GetGpsVendorByLpcdAsync( InGetGpsVendorByLpcd param ,  CancellationToken cancellationToken = default);
    Task<OutGetGpsVendorByPlatNo?>GetGpsVendorByPlatNoAsync( InGetGpsVendorByPlatNo param ,  CancellationToken cancellationToken = default);
    
    Task<OutGetGpsLpcdByGpsVendorId?>GetGpsLpcdByGpsVendorIdAsync( InGetGpsLpcdByGpsVendorId param ,  CancellationToken cancellationToken = default);
    
    Task<OutGetGpsLpcdByGpsVendorName?>GetGpsLpcdByGpsVendorNameAsync( InGetGpsLpcdByGpsVendorName param ,  CancellationToken cancellationToken = default);
}