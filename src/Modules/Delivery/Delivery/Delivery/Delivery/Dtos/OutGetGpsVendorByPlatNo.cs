namespace Delivery.Delivery.Dtos;

public class OutGetGpsVendorByPlatNo
{
    public Guid GpdVendorId { get; set; } 
    public string VendorName { get; set; } = string.Empty;
    
    public OutGetGpsVendorByPlatNo(){}
    
    public OutGetGpsVendorByPlatNo(Guid gpdVendorId, string vendorName)
    {
        GpdVendorId = gpdVendorId;
        VendorName = vendorName;
    }
}