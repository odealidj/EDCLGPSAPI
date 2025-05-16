using Delivery.Delivery.Dtos;

namespace Delivery.Data.Repositories.IRepositories;

public interface IDeliveryDapperRepository
{
    Task<IEnumerable<TrackDeliveryEdclResponseDto>>GetTrackDeliveryAsync( TrackDeliveryEdclRequestDto param ,  bool asNoTracking = true, CancellationToken cancellationToken = default);

}