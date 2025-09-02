using Shared.Exceptions;

namespace GeofenceMaster.GeofenceMaster.Exceptions;

public class GeofenceMasterDatabaseAccessException(string message, string details)
    : DatabaseAccessException(message, details)
{
}