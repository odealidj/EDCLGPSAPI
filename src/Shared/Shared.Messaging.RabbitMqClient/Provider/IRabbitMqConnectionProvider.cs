
namespace Shared.Messaging.RabbitMqClient.Provider;

public interface IRabbitMqConnectionProvider {
    bool IsConnected { get; }
    IConnection GetConnection();
    IModel CreateModel();
    IModel? GetChannel();
}