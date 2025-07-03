using Microsoft.Extensions.Logging;
using Shared.Messaging.RabbitMqClient.Provider;

namespace GeofenceWorker.Services.RabbitMqClient;

public class RabbitMqConnectionProviderNonTls: RabbitMqConnectionProvider
{
    //string hostName, string userName, string password, int port = 5672, string virtualHost = "/", ILogger<RabbitMqConnectionProvider> logger = null
    public RabbitMqConnectionProviderNonTls(IConfiguration config, ILogger<RabbitMqConnectionProvider> logger)
        : base(
            config["RabbitMq5672:HostName"], 
            config["RabbitMq5672:UserName"], 
            config["RabbitMq5672:Password"], 
            int.TryParse(config["RabbitMq5672:Port"], out var port) ? port : 5672,
            config["RabbitMq5672:VirtualHost"],
            logger)
    {
    }
}