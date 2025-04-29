using Shared.Exceptions;

namespace GeofenceMaster.GeofenceMaster.Exceptions;

public class GeofenceMasterAuthNotFoundException(Guid id) : NotFoundException("GpsVendorAuth", id);