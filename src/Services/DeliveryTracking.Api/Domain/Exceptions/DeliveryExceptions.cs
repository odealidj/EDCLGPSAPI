using Shared.Exceptions;

namespace DeliveryTracking.Domain.Exceptions;

// exception khusus domain delivery tracking
[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3925:Update this implementation of 'ISerializable' to conform to the recommended serialization pattern", Justification = "BinaryFormatter is obsolete in .NET 8+")]
public class DeliveryProgressNotFoundException(string deliveryNo)
    : NotFoundException($"Data Delivery Progress dengan nomor '{deliveryNo}' tidak ditemukan.")
{
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3925:Update this implementation of 'ISerializable' to conform to the recommended serialization pattern", Justification = "BinaryFormatter is obsolete in .NET 8+")]
public class DeliveryProgressConflictException(string message, string details = "")
    : ConflictException(message, details)
{
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3925:Update this implementation of 'ISerializable' to conform to the recommended serialization pattern", Justification = "BinaryFormatter is obsolete in .NET 8+")]
public class DeliveryProgressBadRequestException(string message, string details = "")
    : BadRequestException(message, details)
{
}
