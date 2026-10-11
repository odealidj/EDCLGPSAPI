using System.Text.Json;
using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Application.Ports;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Shared.Messaging.RabbitMqClient.Provider;

namespace DeliveryTracking.Infrastructure.Adapters.Messaging;

// Driven Adapter: Mem-publish event status delivery (seperti geofence arrival) ke RabbitMQ exchange edcl.delivery
public sealed class RabbitMqDeliveryEventPublisher : IDeliveryEventPublisher
{
    private readonly IRabbitMqConnectionProvider? _connectionProvider;
    private readonly ILogger<RabbitMqDeliveryEventPublisher> _logger;
    private const string ExchangeName = "edcl.delivery";
    private const string RoutingKeyDelivered = "delivery.status.delivered";

    public RabbitMqDeliveryEventPublisher(
        ILogger<RabbitMqDeliveryEventPublisher> logger,
        IRabbitMqConnectionProvider? connectionProvider = null)
    {
        _logger = logger;
        _connectionProvider = connectionProvider;
    }

    public Task PublishDeliveryDeliveredAsync(
        DeliveryDeliveredEventDto eventDto,
        CancellationToken cancellationToken = default)
    {
        if (_connectionProvider == null)
        {
            _logger.LogWarning("RabbitMQ connection provider tidak tersedia. Event delivery.status.delivered tidak dapat dipublish.");
            return Task.CompletedTask;
        }

        try
        {
            using var channel = _connectionProvider.CreateModel();
            var bodyBytes = JsonSerializer.SerializeToUtf8Bytes(eventDto, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Type = "DeliveryStatusDeliveredEvent";
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            channel.BasicPublish(
                exchange: ExchangeName,
                routingKey: RoutingKeyDelivered,
                basicProperties: properties,
                body: bodyBytes);

            _logger.LogInformation("Event delivery.status.delivered berhasil dipublish ke RabbitMQ untuk DeliveryNo {DeliveryNo}", eventDto.DeliveryNo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gagal mem-publish event delivery.status.delivered ke RabbitMQ untuk DeliveryNo {DeliveryNo}", eventDto.DeliveryNo);
        }

        return Task.CompletedTask;
    }
}
