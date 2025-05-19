namespace Delivery.Delivery.Models;

public class DeliveryProgress: Entity<Guid>
{
    public string DeliveryNo { get; set; } = string.Empty;
    public string PlatNo { get; set; } = string.Empty;
    public string NoKtp { get; set; } = string.Empty;
    public string? VendorName { get; set; } 
    public string? Lpcd { get; set; }


    public static DeliveryProgress Create(
        Guid id,
        string deliveryNo,
        string platNo,
        string noKtp,
        string? vendorName,
        string? lpcd
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(deliveryNo);
        ArgumentException.ThrowIfNullOrEmpty(platNo);
        ArgumentException.ThrowIfNullOrEmpty(noKtp);
        //ArgumentException.ThrowIfNullOrEmpty(vendorName);

        var deliveryProgress = new DeliveryProgress
        {
            Id = id,
            DeliveryNo = deliveryNo,
            PlatNo = platNo,
            NoKtp = noKtp,
            VendorName = vendorName,
            Lpcd = !string.IsNullOrEmpty(lpcd) ? lpcd : string.Empty
        };

        return deliveryProgress;
    }
}