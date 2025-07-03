using Microsoft.Extensions.Logging;
using Shared.Messaging.RabbitMqClient.Provider;

namespace GeofenceWorker.Services.RabbitMqClient;

public class RabbitMqConnectionProviderTls: RabbitMqConnectionProvider
{
    //string hostName, string userName, string password, int port = 5672, string virtualHost = "/", ILogger<RabbitMqConnectionProvider> logger = null
    public RabbitMqConnectionProviderTls(IConfiguration config, ILogger<RabbitMqConnectionProvider> logger)
        : base(
            config["RabbitMQ:HostName"], 
            config["RabbitMQ:UserName"], 
            config["RabbitMQ:Password"], 
            int.TryParse(config["RabbitMQ:Port"], out var port) ? port : 5671,
            config["RabbitMQ:VirtualHost"],
            logger)
    {
    }
}