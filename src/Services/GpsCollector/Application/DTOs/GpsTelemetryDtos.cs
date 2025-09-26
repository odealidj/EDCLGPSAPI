namespace GpsCollector.Application.DTOs;

// DTO titik koordinat telemetri armada (AOT ready)
public sealed record GpsTelemetryPointDto(
    Guid Id,
    Guid HeaderId,
    string? Lpcd,
    string? PlatNo,
    string? DeviceId,
    DateTime Datetime,
    decimal? X,
    decimal? Y,
    decimal? Speed,
    decimal? Course,
    string? StreetName
);

// DTO event message yang dikirimkan ke RabbitMQ
public sealed record GpsDeliveryMessageDto(
    Guid Id,
    Guid GpsVendorId,
    string VendorName,
    DateTime Timestamp,
    List<GpsTelemetryPointDto> Data
);

// DTO cache snapshot posisi terkini di Redis (key: gps:last:{platNo})
public sealed record GpsCachePayloadDto(
    Guid Id,
    Guid HeaderId,
    string? Lpcd,
    string? PlatNo,
    string? DeviceId,
    DateTime Datetime,
    decimal? X,
    decimal? Y,
    decimal? Speed,
    decimal? Course,
    string? StreetName,
    DateTime CachedAt
);

// DTO status service
public sealed record CollectorStatusDto(
    string Service,
    string Role,
    string Status,
    string Architecture,
    bool AotReady,
    DateTime Timestamp
);

// DTO hasil polling vendor
public sealed record VendorPollingResultDto(
    Guid VendorId,
    string VendorName,
    bool Success,
    int PointsCount,
    int DeliveryPointsCount,
    double DurationMs,
    string? ErrorMessage = null
);

// DTO alert notifikasi kegagalan/pemulihan koneksi vendor
public sealed record VendorAlertMessageDto(
    Guid VendorId,
    string VendorName,
    string EndpointUrl,
    int ConsecutiveFailures,
    int? LastStatusCode,
    string? LastErrorMessage,
    string AlertType,
    DateTime Timestamp,
    double? DowntimeSeconds = null
);

// DTO live snapshot posisi delivery aktif di Redis (key: delivery:live:{deliveryNo})
public sealed record DeliveryLivePositionCacheDto(
    string DeliveryNo,
    string TrackingStatus,
    string? PlateNumber,
    string? DeviceId,
    decimal? Latitude,
    decimal? Longitude,
    decimal? Speed,
    decimal? Course,
    string? StreetName,
    DateTime Datetime,
    DateTime UpdatedAt
);

// DTO status live tracking delivery di Redis (misal: DATA_MISMATCH atau VENDOR_CONNECTION_ERROR)
public sealed record DeliveryLiveStatusCacheDto(
    string DeliveryNo,
    string TrackingStatus,
    string? ErrorMessage,
    DateTime UpdatedAt
);



