namespace DeliveryTracking.Application.DTOs;

// dto lookup cepat data vendor dari tabel master/function postgres
public record VendorByLpcdLookupResult(Guid GpsVendorId, string GpsVendorName, string Lpcd);
public record VendorByPlatNoLookupResult(Guid GpsVendorId, string VendorName, string PlatNo);
public record LpcdByVendorNameLookupResult(Guid GpsVendorId, string VendorName, string Lpcd);
