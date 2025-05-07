namespace Delivery.Delivery.Exceptions;

public class DeliveryProgressInternalServerException(string message, string details)
    : InternalServerException(message, details)
{
}