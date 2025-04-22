using GeofenceWorker.Workers.Models;

namespace GeofenceWorker.Events;

public record GpsLastPositionCreatedEvent(GpsLastPosition GpsLastPosition)
    : IDomainEvent;