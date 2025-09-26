using System.Text.Json;
using GpsCollector.Application.DTOs;
using GpsCollector.Application.Ports;
using GpsCollector.Domain.Models;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Shared.Messaging.RabbitMqClient.Provider;

namespace GpsCollector.Infrastructure.Adapters.Messaging;

// Adapter publisher event telemetri ke RabbitMQ (topic_exchange)
public sealed class RabbitMqTelemetryPublisher : ITelemetryEventPublisher
{
    private readonly IRabbitMqConnectionProvider? _connectionProvider;
    private readonly ILogger<RabbitMqTelemetryPublisher> _logger;
    private const string ExchangeName = "topic_exchange";

    public RabbitMqTelemetryPublisher(ILogger<RabbitMqTelemetryPublisher> logger, IRabbitMqConnectionProvider? connectionProvider = null)
    {
        _logger = logger;
        _connectionProvider = connectionProvider;
    }

    public Task PublishTelemetryEventAsync(
        Guid vendorId,
        string vendorName,
        Guid headerId,
        IReadOnlyList<TelemetryPoint> points,
        CancellationToken ct)
    {
        if (_connectionProvider == null || points.Count == 0) return Task.CompletedTask;

        try
        {
            var channel = _connectionProvider.GetChannel();
            if (channel == null || channel.IsClosed)
            {
                return Task.CompletedTask;
            }

            var pointDtos = new List<GpsTelemetryPointDto>(points.Count);
            foreach (var p in points)
            {
                pointDtos.Add(new GpsTelemetryPointDto(
                    Id: p.Id,
                    HeaderId: headerId,
                    Lpcd: p.Lpcd,
                    PlatNo: p.PlatNo,
                    DeviceId: p.DeviceId,
                    Datetime: p.Datetime,
                    X: p.X,
                    Y: p.Y,
                    Speed: p.Speed,
                    Course: p.Course,
                    StreetName: p.StreetName
                ));
            }

            var message = new GpsDeliveryMessageDto(
                Id: headerId,
                GpsVendorId: vendorId,
                VendorName: vendorName,
                Timestamp: DateTime.UtcNow,
                Data: pointDtos
            );

            var bodyBytes = JsonSerializer.SerializeToUtf8Bytes(message, GpsCollectorJsonContext.Default.GpsDeliveryMessageDto);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Type = "GpsTelemetryEvent";

            var routingKey = $"gps.vendor.{vendorId}";

            channel.BasicPublish(
                exchange: ExchangeName,
                routingKey: routingKey,
                basicProperties: properties,
                body: bodyBytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish GPS telemetry message to RabbitMQ for vendor {VendorName}", vendorName);
        }

        return Task.CompletedTask;
    }
}
