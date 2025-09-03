namespace GeofenceWorker.Workers.Exceptions;



public class WorkerConflictException(string message, Exception? innerException = null)
    : Exception(message, innerException);