using Microsoft.Extensions.Logging;
using Shared.Messaging.RabbitMqClient.Provider;

namespace GeofenceWorker.Services.RabbitMqClient;

public class RabbitMqConnectionProviderSsl(IConfiguration config, ILogger<Shared.Messaging.RabbitMqClient.Provider.RabbitMqConnectionProvider> logger)
    : Shared.Messaging.RabbitMqClient.Provider.RabbitMqConnectionProvider(config["RabbitMQ:HostName"] ?? string.Empty,
        config["RabbitMQ:UserName"] ?? string.Empty,
        config["RabbitMQ:Password"] ?? string.Empty,
        int.TryParse(config["RabbitMQ:Port"], out var port) ? port : 5671,
        config["RabbitMQ:VirtualHost"] ?? string.Empty,
        logger)
{
    //string hostName, string userName, string password, int port = 5672, string virtualHost = "/", ILogger<RabbitMqConnectionProvider> logger = null
}