using System.ComponentModel.DataAnnotations;

namespace Delivery.Delivery.Dtos;

public class DeliveryHDto
{
    
    public Guid? Id { get; set; }
    
    [Required(ErrorMessage = "DeliveryNo harus diisi")]
    public string DeliveryNo { get; set; }
    [Required(ErrorMessage = "PlatNo harus diisi")]
    public string PlatNo { get; set; }
    [Required(ErrorMessage = "NoKtp harus diisi")]
    public string NoKtp { get; set; }
    [Required(ErrorMessage = "VendorName harus diisi")]
    public string? VendorName { get; set; }
    [Required(ErrorMessage = "Lpcd harus diisi")]
    public string? Lpcd { get; set; }
    public DateTime CreatedDt { get; set; } 
    public string? CreatedBy { get; set; }
    
    ICollection<DeliveryDDto> DeliveryDDtos { get; set; } = new List<DeliveryDDto>();
}