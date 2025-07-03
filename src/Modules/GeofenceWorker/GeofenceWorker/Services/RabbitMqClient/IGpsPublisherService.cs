namespace GeofenceWorker.Services.RabbitMqClient;

public interface IGpsPublisherService
{
    Task PublishAsync<T>(T message, string routingKey);
}