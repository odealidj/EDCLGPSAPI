using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Delivery.Delivery.Dtos;

public class TrackDeliveryEdclRequestDto
{
    [Required(ErrorMessage = "DeliveryNo is required")]
    public string DeliveryNo { get; set; }
    
    [Required(ErrorMessage = "Density is required")]
    [DefaultValue(1)]
    public int Density { get; set; }
}