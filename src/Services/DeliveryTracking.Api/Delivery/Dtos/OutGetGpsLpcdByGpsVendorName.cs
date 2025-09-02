namespace Delivery.Delivery.Dtos;

public class OutGetGpsLpcdByGpsVendorName
{
    public Guid Id { get; set; } 
    public string Lpcd { get; set; } = string.Empty;
    
    public OutGetGpsLpcdByGpsVendorName(){}
    
    public OutGetGpsLpcdByGpsVendorName(Guid id, string lpcd)
    {
        Id = id;
        Lpcd = lpcd;
    }
}