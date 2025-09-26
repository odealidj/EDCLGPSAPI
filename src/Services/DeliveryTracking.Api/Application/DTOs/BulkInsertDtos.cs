namespace DeliveryTracking.Application.DTOs;

// request dto untuk operasi bulk insert pengiriman massal
public record BulkInsertDeliveryProgressRequestDto(
    List<DeliveryProgressDto> Items
);

// request dto untuk operasi bulk insert koordinat gps telemetri
public record BulkInsertGpsDeliveryItemDto(
    Guid GpsVendorId,
    string GpsVendorName,
    string DeliveryNo,
    string? PlatNo,
    string? Lpcd,
    DateTime? Datetime,
    decimal? X,
    decimal? Y,
    decimal? Speed,
    decimal? Course,
    string? StreetName,
    string? DeviceId = null,
    string? NoKtp = null
);

public record BulkInsertGpsDeliveryRequestDto(
    List<BulkInsertGpsDeliveryItemDto> Items
);

// hasil response operasi bulk insert
public record BulkInsertResult(int RowsInserted, string EntityName, TimeSpan Elapsed);
