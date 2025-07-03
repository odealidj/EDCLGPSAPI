
namespace Shared.Messaging.RabbitMqClient.Provider;

public interface IRabbitMqConnectionProvider {
    IConnection GetConnection();
    IModel CreateModel();
}