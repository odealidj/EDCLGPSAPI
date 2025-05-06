using GeofenceWorker.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GeofenceWorker.EventHandlers;

public class GpsLastPositionCreatedEventHandler(ILogger<GpsLastPositionCreatedEventHandler> logger)
    : INotificationHandler<GpsLastPositionCreatedEvent>
{
    public Task Handle(GpsLastPositionCreatedEvent notification, CancellationToken cancellationToken)
    {
        
        logger.LogInformation("Domain Event handled: {DomainEvent}", notification.GetType().Name);
        return Task.CompletedTask;
    }
}
