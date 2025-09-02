namespace GeofenceMaster.GeofenceMaster.Exceptions;

public class GeofenceMasterInternalServerException(string message, string details)
    : InternalServerException(message, details)
{
}