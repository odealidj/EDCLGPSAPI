namespace GPSInterfacing.Vendors.Events;

public record GpsVendorCreatedEvent(GpsVendor Product)
    : IDomainEvent;