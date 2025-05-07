

namespace Delivery.Delivery.Exceptions;


public class DeliveryProgressConflictException(string message, string details)
    : ConflictException(message, details)
{
}