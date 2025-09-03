using Shared.Exceptions;

namespace GeofenceWorker.Workers.Exceptions;

public class GeofenceMasterMappingNotFoundException(int id) : NotFoundException("Mapping", id);