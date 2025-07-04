using System.Text;
using System.Text.Json;
using GeofenceWorker.Services.RabbitMqClient.factory;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using Shared.Messaging.RabbitMqClient.Provider;

namespace GeofenceWorker.Services.RabbitMqClient.Services;

public class GpsPublisherService : IGpsPublisherService, IDisposable
{
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly ILogger<GpsPublisherService> _logger;
    ////private readonly IModel _channel;
    ////private const string ExchangeName = "topic_exchange";

    private readonly IRabbitMqConnectionProvider _mqProvider1;
    private readonly IRabbitMqConnectionProvider _mqProvider2;

    public GpsPublisherService(
        IRabbitMqConnectionProviderFactory mqProviderFactory, 
        ILogger<GpsPublisherService> logger 
        ///IModel channel
        ) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ////_channel = channel;

        _mqProvider1 = mqProviderFactory.CreateProvider1();
        _mqProvider2 = mqProviderFactory.CreateProvider2();

        // Define retry policy with Polly
        /*
        _retryPolicy = Policy
            .Handle<Exception>() // Tangani semua jenis exception
            .WaitAndRetryAsync(
                retryCount: 5, // Jumlah percobaan maksimal
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(4, attempt)), // Exponential backoff
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    logger.LogWarning("Retry {RetryCount} after {DelaySeconds} seconds due to: {ExceptionMessage}", retryCount, timeSpan.TotalSeconds, exception.Message);
                });
        */
        
        var retryCycle = 0;
        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(4, attempt)),
                onRetryAsync: async (exception, timeSpan, retryCount, context) =>
                {
                    logger.LogWarning("Retry {RetryCount} after {DelaySeconds} seconds due to: {ExceptionMessage}", retryCount, timeSpan.TotalSeconds, exception.Message);
                    if (retryCount == 3)
                    {
                        retryCycle++;
                        if (retryCycle % 3 == 0)
                        {
                            logger.LogWarning("Reached 3 cycles of 5 retries. Pausing for 1 hour before next cycle.");
                            await Task.Delay(TimeSpan.FromHours(1));
                        }
                    }
                });
        
        try
        {
            ////_channel = mqProvider1.CreateModel();
            ////_channel.ExchangeDeclare(exchange: ExchangeName, type: ExchangeType.Topic, durable: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create RabbitMQ channel or declare exchange.");
        }

    }
    
    public async Task PublishAsync<T>(T message, string routingKey)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        await PublishToRabbitMq(_mqProvider1, message, routingKey);
        await PublishToRabbitMq(_mqProvider2, message, routingKey);
    }
    
    private async Task PublishToRabbitMq<T>(IRabbitMqConnectionProvider mqProvider, T message, string routingKey)
    {
        await _retryPolicy.ExecuteAsync(() =>
        {
            ////using var channel = mqProvider.CreateModel();
            try
            {
                var channel = mqProvider.GetChannel();
                
                ////channel.ExchangeDeclare(exchange: "topic_exchange", type: ExchangeType.Topic, durable: false);

                if ( channel is not { IsOpen: true }) throw new InvalidOperationException("RabbitMQ channel was closed unexpectedly.");

            
                var json = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(json);

                var properties = channel.CreateBasicProperties();
                properties.Persistent = true;

                channel.BasicPublish(
                    exchange: "topic_exchange",
                    routingKey: routingKey,
                    mandatory: false,
                    basicProperties: properties,
                    body: body);

                _logger.LogInformation("Published message to RabbitMQ with routing key '{RoutingKey}'", routingKey);
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
    
    /*
    public async Task PublishAsync<T>(T message, string routingKey)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));

        await _retryPolicy.ExecuteAsync(() =>
        {
            using var channel = _mqProvider1.CreateModel();

            channel.ExchangeDeclare(exchange: "topic_exchange", type: ExchangeType.Topic, durable: true);

            
            if (!channel.IsOpen) throw new InvalidOperationException("RabbitMQ channel was closed unexpectedly.");

            try
            {
                var json = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(json);

                var properties = channel.CreateBasicProperties();
                properties.Persistent = true;

                channel.BasicPublish(
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
    */
    public void Dispose()
    {
        ////_channel?.Dispose();
    }
}