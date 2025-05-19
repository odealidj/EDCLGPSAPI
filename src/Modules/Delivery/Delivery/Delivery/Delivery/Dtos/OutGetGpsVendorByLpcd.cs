namespace Delivery.Delivery.Dtos;

public class OutGetGpsVendorByLpcd
{
    public Guid GpdVendorId { get; set; } 
    public string GpsVendorName { get; set; } = string.Empty;
    
    public OutGetGpsVendorByLpcd(){}
    
    public OutGetGpsVendorByLpcd(Guid gpdVendorId, string gpsVendorName)
    {
        GpdVendorId = gpdVendorId;
        GpsVendorName = gpsVendorName;
    }
}