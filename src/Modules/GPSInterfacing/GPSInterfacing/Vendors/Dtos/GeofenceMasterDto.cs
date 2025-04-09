namespace GPSInterfacing.Vendors.Dtos;

public record GeofenceMasterDto(
    Guid? Id,
    string VendorName,
    string LpcdId,
    string? Timezone,
    bool RequiredAuth,
    List<GeofenceMasterAuthDto> Items
);