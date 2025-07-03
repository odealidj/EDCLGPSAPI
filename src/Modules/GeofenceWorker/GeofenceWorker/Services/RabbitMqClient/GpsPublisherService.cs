using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using Shared.Messaging.RabbitMqClient.Provider;

namespace GeofenceWorker.Services.RabbitMqClient;

public class GpsPublisherService : IGpsPublisherService, IDisposable
{
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly ILogger<GpsPublisherService> _logger;
    private readonly IModel _channel;
    private const string ExchangeName = "topic_exchange";

    public GpsPublisherService(
        IRabbitMqConnectionProvider mqProvider, 
        ILogger<GpsPublisherService> logger, 
        IModel channel) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _channel = channel;


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
        
        try
        {
            _channel = mqProvider.CreateModel();
            _channel.ExchangeDeclare(exchange: ExchangeName, type: ExchangeType.Topic, durable: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create RabbitMQ channel or declare exchange.");
        }

    }
    
    
    public async Task PublishAsync<T>(T message, string routingKey)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));

        await _retryPolicy.ExecuteAsync(() =>
        {
            
            if (!_channel.IsOpen) throw new InvalidOperationException("RabbitMQ channel was closed unexpectedly.");

            try
            {
                var json = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(json);

                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;

                _channel.BasicPublish(
                    exchange: "topic_exchange",
                    routingKey: routingKey,
                    mandatory: false,
                    basicProperties: properties,
                    body: body);

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
    
    public void Dispose()
    {
        _channel?.Dispose();
    }
}