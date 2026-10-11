using DeliveryTracking.Application.DTOs;

namespace DeliveryTracking.Application.Ports;

// Port outbound: Publisher event siklus delivery ke RabbitMQ (exchange edcl.delivery)
public interface IDeliveryEventPublisher
{
    Task PublishDeliveryDeliveredAsync(
        DeliveryDeliveredEventDto eventDto,
        CancellationToken cancellationToken = default);
}
