namespace Delivery.Delivery.Dtos;

public class InGetGpsVendorByPlatNo
{
    public string PlatNo { get; set; }
    
    public InGetGpsVendorByPlatNo()
    {
        
    }
    
    public InGetGpsVendorByPlatNo(string platNo)
    {
        PlatNo = platNo;
    }
}