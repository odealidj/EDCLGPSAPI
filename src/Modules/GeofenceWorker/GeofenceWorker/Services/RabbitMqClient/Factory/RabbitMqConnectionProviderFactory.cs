using Microsoft.Extensions.Logging;
using Shared.Messaging.RabbitMqClient.Provider;

namespace GeofenceWorker.Services.RabbitMqClient.factory;

public class RabbitMqConnectionProviderFactory : IRabbitMqConnectionProviderFactory
{
    private readonly IConfiguration _config;
    private readonly ILogger<RabbitMqConnectionProvider> _logger;
    public RabbitMqConnectionProviderFactory(IConfiguration config, ILogger<RabbitMqConnectionProvider> logger)
    {
        _config = config;
        _logger = logger;
    }
    public IRabbitMqConnectionProvider CreateProvider1()
    {
        return new RabbitMqConnectionProviderTls(_config, _logger);
    }
    public IRabbitMqConnectionProvider CreateProvider2()
    {
        return new RabbitMqConnectionProviderNonTls(_config, _logger);
    }
}