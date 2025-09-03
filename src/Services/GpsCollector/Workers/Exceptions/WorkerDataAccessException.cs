namespace GeofenceWorker.Workers.Exceptions;

public class WorkerDataAccessException(string message, Exception? innerException = null)
    : Exception(message, innerException);