namespace Delivery.Delivery.Dtos;

public class OutGetGpsLpcdByGpsVendorId
{
    public Guid GpsLpcdId { get; set; } 
    public string GpsLpcd { get; set; } = string.Empty;
    
    public OutGetGpsLpcdByGpsVendorId(){}
    
    public OutGetGpsLpcdByGpsVendorId(Guid gpslpcdId, string gpsLpcd)
    {
        GpsLpcdId = gpslpcdId;
        GpsLpcd = gpsLpcd;
    }
}