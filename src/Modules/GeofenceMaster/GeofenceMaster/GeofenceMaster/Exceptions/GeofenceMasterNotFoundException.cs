using Shared.Exceptions;

namespace GeofenceMaster.GeofenceMaster.Exceptions;

public class GeofenceMasterNotFoundException : NotFoundException
{
    public GeofenceMasterNotFoundException(Guid id) 
        : base("GpsVendor", id)
    {
    }
}