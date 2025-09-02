using Shared.Exceptions;

namespace GeofenceMaster.GeofenceMaster.Exceptions;

public class GeofenceMasterEndpointNotFoundException(Guid id) : NotFoundException("GpsVendorEndpoint", id);