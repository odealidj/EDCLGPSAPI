using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Polly;
using Polly.Retry;


namespace GeofenceWorker.Services.RabbitMq;

public class RabbitMqService: IRabbitMqService
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly ILogger<RabbitMqService> _logger;
    
    public RabbitMqService(RabbitMqSettings settings, ILogger<RabbitMqService> logger)
    {
        ////var settings = rabbitMqSettings.Value;
        
        if (string.IsNullOrEmpty(settings.HostName))
            throw new ArgumentException("HostName is required.", nameof(settings.HostName));
        
        /*
        var factory = new ConnectionFactory
        {
            HostName = settings.HostName,
            UserName = settings.UserName,
            Password = settings.Password,
            Port = settings.Port, // Use the configured port
            VirtualHost = settings.VirtualHost // Use the configured virtual host
        };
        */
    
        // Konversi ke URI
        int port = settings.Port > 0 ? settings.Port : 5672;
        string virtualHost = string.IsNullOrEmpty(settings.VirtualHost) ? "" : settings.VirtualHost;

        
        string rabbitMqUri;
        if (string.IsNullOrEmpty(settings.VirtualHost))
        {
            rabbitMqUri = $"{settings.Protocol}://{settings.UserName}:{settings.Password}@{settings.HostName}:{port}";
        }
        else
        {
            rabbitMqUri = $"{settings.Protocol}://{settings.UserName}:{settings.Password}@{settings.HostName}:{port}/{settings.VirtualHost}";
        }
        
        var factory = new ConnectionFactory
        {
            Uri = new Uri(rabbitMqUri)
            //,
            //DispatchConsumersAsync = true // Opsional: Untuk konsumsi async
        };
        
        
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _logger = logger;
        

        // Declare topic exchange
        _channel.ExchangeDeclare(exchange: "topic_exchange", type: ExchangeType.Topic);
        
        // Define retry policy with Polly
        _retryPolicy = Policy
            .Handle<Exception>() // Tangani semua jenis exception
            .WaitAndRetryAsync(
                retryCount: 3, // Jumlah percobaan maksimal
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)), // Exponential backoff
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    Console.WriteLine($"Retry {retryCount} after {timeSpan.TotalSeconds} seconds due to: {exception.Message}");
                });
    }
    
    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        Console.WriteLine("Closed RabbitMQ connection.");
    }

    public async Task PublishAsync<T>(T message, string routingKey)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        if (!_channel.IsOpen) throw new InvalidOperationException("RabbitMQ channel is not open.");

        await _retryPolicy.ExecuteAsync(() =>
        {
            if (!_channel.IsOpen) throw new InvalidOperationException("RabbitMQ channel was closed unexpectedly.");

            try
            {
                var json = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(json);

                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;

                _channel.BasicPublish(exchange: "topic_exchange" , routingKey: routingKey, basicProperties: properties, body: body);
                _logger.LogInformation("Published message with routing key '{RoutingKey}'", routingKey);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Serialization error while publishing message.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while publishing message.");
                throw;
            }

            return Task.CompletedTask;
        }).ConfigureAwait(false);
    }

}