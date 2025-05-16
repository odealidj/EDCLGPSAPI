namespace Delivery.Delivery.Dtos;

public class TrackDeliveryEdclResponseDto
{
    
    ////public Guid? Id { get; set; } = default!;
    public int PositionId { get; set; }
    public string DeliveryNo { get; set; }
    public string NoKTP { get; set; }
    public string LPCD { get; set; }
    public string GpsVendor { get; set; }
    public string FlagGps { get; set; }
    public string PlatNo { get; set; }
    public string DeviceId { get; set; }
    public DateTime? Datetime { get; set; }
    public decimal? X { get; set; }
    public decimal? Y { get; set; }
    public decimal? Speed { get; set; }
    public decimal? Course { get; set; }
    public string StreetName { get; set; }
    

}