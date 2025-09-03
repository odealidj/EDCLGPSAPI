using Shared.Messaging.RabbitMqClient.Provider;

namespace GeofenceWorker.Services.RabbitMqClient.factory;

public interface IRabbitMqConnectionProviderFactory
{
    IRabbitMqConnectionProvider CreateProvider1();
    IRabbitMqConnectionProvider CreateProvider2();
}