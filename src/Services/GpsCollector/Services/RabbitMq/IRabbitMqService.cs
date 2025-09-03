using RabbitMQ.Client;

namespace GeofenceWorker.Services.RabbitMq;

public interface IRabbitMqService : IDisposable
{
    Task PublishAsync<T>(T message, string routingKey);
}