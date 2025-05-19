namespace Delivery.Delivery.Dtos;

public class InGetGpsLpcdByGpsVendorId
{
    public Guid GpsVendorId { get; set; }
    
    public InGetGpsLpcdByGpsVendorId()
    {
        
    }
    
    public InGetGpsLpcdByGpsVendorId(Guid gpsVendorId)
    {
        GpsVendorId = gpsVendorId;
    }
}