namespace GPSInterfacing.Vendors.Dtos;

public record GpsVendorDto(
    Guid Id,
    string VendorName,
    string LpcdId,
    string Timezone,
    bool RequiredAuth
);
