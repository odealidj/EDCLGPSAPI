namespace Delivery.Delivery.Exceptions;

public class DeliveryProgressDatabaseAccessException(string message, string details)
    : DatabaseAccessException(message, details)
{
}