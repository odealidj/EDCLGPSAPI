using Shared.Exceptions;

namespace GeofenceMaster.GeofenceMaster.Exceptions;

public class GeofenceMasterNotFoundException(Guid id) : NotFoundException("GpsVendor", id);