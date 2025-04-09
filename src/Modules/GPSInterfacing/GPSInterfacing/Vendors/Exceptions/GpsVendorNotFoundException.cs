using Shared.Exceptions;

namespace GPSInterfacing.Vendors.Exceptions;

public class GpsVendorNotFoundException : NotFoundException
{
    public GpsVendorNotFoundException(Guid id) 
        : base("GpsVendor", id)
    {
    }
}