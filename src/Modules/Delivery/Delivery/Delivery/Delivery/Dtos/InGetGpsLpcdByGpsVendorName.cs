namespace Delivery.Delivery.Dtos;

public class InGetGpsLpcdByGpsVendorName
{
    public string VendorName { get; set; }
    
    public InGetGpsLpcdByGpsVendorName()
    {
        
    }
    
    public InGetGpsLpcdByGpsVendorName(string vendorName)
    {
        VendorName = vendorName;
    }
}