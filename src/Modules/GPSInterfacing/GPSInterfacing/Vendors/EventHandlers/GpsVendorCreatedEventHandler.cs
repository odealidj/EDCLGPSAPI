using GPSInterfacing.Vendors.Events;

namespace GPSInterfacing.Vendors.EventHandlers;

public class GpsVendorCreatedEventHandler(ILogger<GpsVendorCreatedEventHandler> logger)
    : INotificationHandler<GpsVendorCreatedEvent>
{
    public Task Handle(GpsVendorCreatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Domain Event handled: {DomainEvent}", notification.GetType().Name);
        return Task.CompletedTask;
    }
}