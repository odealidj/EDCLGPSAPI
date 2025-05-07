namespace Delivery.Delivery.Exceptions;

public class DeliveryProgressNotFoundException(string deliveryNo) : NotFoundException("Delivery Progress ", deliveryNo);