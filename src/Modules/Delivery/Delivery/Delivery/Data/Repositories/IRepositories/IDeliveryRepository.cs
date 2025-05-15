using Delivery.Delivery.Dtos;
using Delivery.Delivery.Models;

namespace Delivery.Data.Repositories.IRepositories;

public interface IDeliveryRepository
{
    Task<Guid> UpsertDeliveryProgressAsync(DeliveryProgress deliveryProgress, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<TrackDeliveryEdclResponseDto>>GetTrackDeliveryAsync( TrackDeliveryEdclRequestDto param ,  bool asNoTracking = true, CancellationToken cancellationToken = default);
    
}