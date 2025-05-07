namespace Delivery.Delivery.Dtos;

public class DeleteDeliveryProgressDto
{
    public string DeliveryNo { get; set; }

    public DeleteDeliveryProgressDto()
    {
    }

    public DeleteDeliveryProgressDto(string deliveryNo)
    {
        DeliveryNo = deliveryNo;
    }
}

