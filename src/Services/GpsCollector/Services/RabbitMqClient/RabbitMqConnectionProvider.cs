using Microsoft.Extensions.Logging;
using Shared.Messaging.RabbitMqClient.Provider;

namespace GeofenceWorker.Services.RabbitMqClient;

public class RabbitMqConnectionProvider(
    IConfiguration config,
    ILogger<Shared.Messaging.RabbitMqClient.Provider.RabbitMqConnectionProvider> logger)
    : Shared.Messaging.RabbitMqClient.Provider.RabbitMqConnectionProvider(
        config["RabbitMq5672:HostName"] ?? string.Empty,
        config["RabbitMq5672:UserName"] ?? string.Empty,
        config["RabbitMq5672:Password"] ?? string.Empty,
        int.TryParse(config["RabbitMq5672:Port"], out var port) ? port : 5672,
        config["RabbitMq5672:VirtualHost"] ?? string.Empty,
        logger)
{
    //string hostName, string userName, string password, int port = 5672, string virtualHost = "/", ILogger<RabbitMqConnectionProvider> logger = null
}