using Shared.Exceptions;

namespace GeofenceMaster.GeofenceMaster.Exceptions;

public class GeofenceMasterMappingNotFoundException(int id) : NotFoundException("Mapping", id);