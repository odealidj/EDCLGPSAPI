using Shared.Messaging.RabbitMqClient.Provider;

namespace GeofenceWorker.Services.RabbitMqClient.Services;

public interface IGpsPublisherService
{
    Task PublishAsync<T>(T message, string routingKey);
}