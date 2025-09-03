namespace GeofenceWorker.Workers.Dtos;

public record LastPositionRequestDto(
    Guid? GpsVendorId,
    string? VendorName, 
    string? PlatNo,
    string? DeviceId,
    DateTime? Datetime,
    DateTime? CreatedAt,
    int PageIndex = 0, 
    int PageSize = 100
);

