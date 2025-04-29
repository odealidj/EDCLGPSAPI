using GeofenceWorker.Workers.Models;

namespace GeofenceWorker.Events;

public record GpsLastPositionCreatedEvent(GpsLastPositionD GpsLastPositionD)
    : IDomainEvent;