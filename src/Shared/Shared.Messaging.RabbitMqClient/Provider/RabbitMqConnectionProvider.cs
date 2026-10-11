namespace Shared.Messaging.RabbitMqClient.Provider;

public class RabbitMqConnectionProvider : IRabbitMqConnectionProvider, IDisposable {
    
    private readonly IConnection? _connection;
    private readonly IModel? _channel;
    private readonly ILogger<RabbitMqConnectionProvider>? _logger;

    public bool IsConnected => _connection != null && _connection.IsOpen;
    
    public RabbitMqConnectionProvider(string hostName, string userName, string password, int port = 5672, string virtualHost = "/", ILogger<RabbitMqConnectionProvider>? logger = null)
    {
        _logger = logger;
        var factory = new ConnectionFactory
        {
            HostName = hostName,
            UserName = userName,
            Password = password,
            Port = port,
            VirtualHost = virtualHost,
            DispatchConsumersAsync = true
        };
        try
        {
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            SetupTopology(_channel);
        }
        catch (BrokerUnreachableException ex)
        {
            _logger?.LogError(ex, "Could not connect to RabbitMQ ({Host}:{Port}). Running in degraded mode.", hostName, port);
            _connection = null;
        }
        catch (Exception ex) {
            _logger?.LogError(ex, "An unexpected error occurred while connecting to RabbitMQ.");
            _connection = null;
        }
    }
    
    public RabbitMqConnectionProvider(IConfiguration config, ILogger<RabbitMqConnectionProvider> logger) {
        _logger = logger;

        var hostName = config["RabbitMqClient:HostName"] ?? config["RabbitMq:HostName"] ?? "localhost";
        var userName = config["RabbitMqClient:UserName"] ?? config["RabbitMq:UserName"] ?? "guest";
        var password = config["RabbitMqClient:Password"] ?? config["RabbitMq:Password"] ?? "guest";
        var portStr = config["RabbitMqClient:Port"] ?? config["RabbitMq:Port"];
        var port = int.TryParse(portStr, out var p) ? p : 5672;
        var virtualHost = config["RabbitMqClient:VirtualHost"] ?? config["RabbitMq:VirtualHost"] ?? "/";

        var factory = new ConnectionFactory {
            HostName = hostName,
            UserName = userName,
            Password = password,
            Port = port,
            VirtualHost = virtualHost,
            DispatchConsumersAsync = true
        };
        try
        {
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            SetupTopology(_channel);
        }
        catch (BrokerUnreachableException ex)
        {
            _logger?.LogError(ex, "Could not connect to RabbitMQ ({Host}:{Port}). Running in degraded mode.", hostName, port);
            _connection = null;
        }
        catch (Exception ex) {
            _logger?.LogError(ex, "An unexpected error occurred while connecting to RabbitMQ.");
            _connection = null;
        }
    }

    private void SetupTopology(IModel channel)
    {
        try
        {
            // 1. Exchanges
            channel.ExchangeDeclare(exchange: "edcl.delivery", type: ExchangeType.Topic, durable: true);
            channel.ExchangeDeclare(exchange: "edcl.gps.position", type: ExchangeType.Topic, durable: true);
            channel.ExchangeDeclare(exchange: "edcl.vendor.config", type: ExchangeType.Fanout, durable: true);
            channel.ExchangeDeclare(exchange: "edcl.dlx", type: ExchangeType.Topic, durable: true);
            channel.ExchangeDeclare(exchange: "topic_exchange", type: ExchangeType.Topic, durable: true);

            // 2. Dead-letter queue
            channel.QueueDeclare(queue: "edcl.dlq", durable: true, exclusive: false, autoDelete: false, arguments: null);
            channel.QueueBind(queue: "edcl.dlq", exchange: "edcl.dlx", routingKey: "#");

            // 3. Delivery Collector Queue (edcl.delivery.collector <- edcl.delivery with delivery.active.#)
            var dlxArgs = new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = "edcl.dlx",
                ["x-dead-letter-routing-key"] = "dlq.delivery.collector"
            };
            channel.QueueDeclare(queue: "edcl.delivery.collector", durable: true, exclusive: false, autoDelete: false, arguments: dlxArgs);
            channel.QueueBind(queue: "edcl.delivery.collector", exchange: "edcl.delivery", routingKey: "delivery.active.#");

            // 4. GPS Position Queues
            var geofenceArgs = new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = "edcl.dlx",
                ["x-dead-letter-routing-key"] = "dlq.gps.position.geofence"
            };
            channel.QueueDeclare(queue: "edcl.gps.position.geofence", durable: true, exclusive: false, autoDelete: false, arguments: geofenceArgs);
            channel.QueueBind(queue: "edcl.gps.position.geofence", exchange: "edcl.gps.position", routingKey: "gps.truck.#");

            var webappArgs = new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = "edcl.dlx",
                ["x-dead-letter-routing-key"] = "dlq.gps.position.webapp"
            };
            channel.QueueDeclare(queue: "edcl.gps.position.webapp", durable: true, exclusive: false, autoDelete: false, arguments: webappArgs);
            channel.QueueBind(queue: "edcl.gps.position.webapp", exchange: "edcl.gps.position", routingKey: "gps.position.#");

            _logger?.LogInformation("RabbitMQ EDCL Topology declared successfully.");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to declare RabbitMQ topology automatically. Continuing.");
        }
    }

    public IModel GetChannel() => _channel ?? throw new InvalidOperationException("RabbitMQ channel is not available.");

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
        _channel?.Close();
        _connection?.Dispose();
    }
}