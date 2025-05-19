using Delivery.Data;
using Delivery.Data.Repositories.IRepositories;
using Delivery.Delivery.Dtos;
using Npgsql;

namespace Delivery.Delivery.Features.TrackDelivery;


public record TrackDeliveryEdclCommand(TrackDeliveryEdclRequestDto TrackDeliveryRequest)
    : ICommand<TrackDeliveryEdclResult>;
public record TrackDeliveryEdclResult(List<TrackDeliveryEdclResponseDto> TrackDeliveryResult);

internal class TrackDeliveryEdclHandler(IDeliveryDapperRepository deliveryRepository, ILogger<TrackDeliveryEdclHandler> logger)
    : ICommandHandler<TrackDeliveryEdclCommand, TrackDeliveryEdclResult>
{
    public async Task<TrackDeliveryEdclResult> Handle(TrackDeliveryEdclCommand command, CancellationToken cancellationToken)
    {
        
        var data = await deliveryRepository.GetTrackDeliveryAsync(
            new TrackDeliveryEdclRequestDto(
                command.TrackDeliveryRequest.DeliveryNo, command.TrackDeliveryRequest.Density),
            true, cancellationToken);

        return new TrackDeliveryEdclResult(data.ToList());
       
        
    }
}