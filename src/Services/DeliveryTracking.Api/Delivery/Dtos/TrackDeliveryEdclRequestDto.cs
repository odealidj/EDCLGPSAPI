using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Delivery.Delivery.Dtos;

public class TrackDeliveryEdclRequestDto
{
    public TrackDeliveryEdclRequestDto()
    {
        
    }

    public TrackDeliveryEdclRequestDto(string deliveryNo, int density)
    {
        DeliveryNo = deliveryNo;
        Density = density;
    }
    
    [Required(ErrorMessage = "DeliveryNo is required")]
    public string DeliveryNo { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Density is required")]
    [DefaultValue(1)]
    public int Density { get; set; }
}