namespace Shared.Messaging.RabbitMqClient.Provider;
public class RabbitMqConnectionProvider : IRabbitMqConnectionProvider, IDisposable {
    
    private readonly IConnection? _connection;

    public RabbitMqConnectionProvider(IConfiguration config, ILogger<RabbitMqConnectionProvider> logger) {
        var factory = new ConnectionFactory {
            HostName = config["RabbitMqClient:HostName"] ?? "localhost",
            UserName = config["RabbitMqClient:UserName"] ?? "guest",
            Password = config["RabbitMqClient:Password"] ?? "guest",
            Port = int.TryParse(config["RabbitMqClient:Port"], out var port) ? port : 5672,
            VirtualHost = config["RabbitMqClient:VirtualHost"] ?? "/",
        };
        try
        {
            _connection = factory.CreateConnection();
        }
        catch (BrokerUnreachableException ex)
        {
            logger.LogError(ex, "Could not connect to RabbitMQ. The application will continue running without RabbitMQ connectivity.");
            _connection = null;
        }
        catch (Exception ex) {
            logger.LogError(ex, "An unexpected error occurred while connecting to RabbitMQ.");
            _connection = null;
        }
        
    }
    
    public IConnection GetConnection() {
        if (_connection == null)
            throw new InvalidOperationException("RabbitMQ connection is not available.");
        return _connection;
    }
    
    public IModel CreateModel() {
        if (_connection == null)
            throw new InvalidOperationException("RabbitMQ connection is not available.");
        return _connection.CreateModel();
    }
    
    public void Dispose() {
        _connection?.Dispose();
    }
}