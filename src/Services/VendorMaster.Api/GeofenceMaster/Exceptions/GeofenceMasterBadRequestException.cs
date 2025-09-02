namespace GeofenceMaster.GeofenceMaster.Exceptions;


public class GeofenceMasterBadRequestException(string message, string details)
    : BadRequestException(message, details)
{
}
