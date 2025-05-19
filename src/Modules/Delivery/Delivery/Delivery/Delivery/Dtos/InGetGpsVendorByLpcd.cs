namespace Delivery.Delivery.Dtos;

public class InGetGpsVendorByLpcd
{
    public string Lpcd { get; set; }
    
    public InGetGpsVendorByLpcd()
    {
        
    }
    
    public InGetGpsVendorByLpcd(string lpcd)
    {
        Lpcd = lpcd;
    }
}