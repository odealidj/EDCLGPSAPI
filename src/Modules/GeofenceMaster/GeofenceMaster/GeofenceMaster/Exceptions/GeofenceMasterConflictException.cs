using Shared.Exceptions;

namespace GeofenceMaster.GeofenceMaster.Exceptions;

public class GeofenceMasterConflictException(string message, string details)
    : ConflictException(message, details)
{
}

