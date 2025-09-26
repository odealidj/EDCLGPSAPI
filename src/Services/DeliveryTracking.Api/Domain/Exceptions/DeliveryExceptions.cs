using Shared.Exceptions;

namespace DeliveryTracking.Domain.Exceptions;

// exception khusus domain delivery tracking
public class DeliveryProgressNotFoundException(string deliveryNo)
    : NotFoundException($"Data Delivery Progress dengan nomor '{deliveryNo}' tidak ditemukan.")
{
}

public class DeliveryProgressConflictException(string message, string details = "")
    : ConflictException(message, details)
{
}

public class DeliveryProgressBadRequestException(string message, string details = "")
    : BadRequestException(message, details)
{
}
