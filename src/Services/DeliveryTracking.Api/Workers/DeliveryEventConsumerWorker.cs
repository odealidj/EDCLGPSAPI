using System.Text;
using System.Text.Json;
using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Application.UseCases.CompleteDeliveryJob;
using DeliveryTracking.Application.UseCases.StartDeliveryJob;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Messaging.RabbitMqClient.Provider;

namespace DeliveryTracking.Workers;

// Background Worker yang mengonsumsi event siklus hidup delivery dari RabbitMQ (queue: edcl.delivery.collector)
// Menggantikan pemanggilan REST langsung dari EDCL API
public sealed class DeliveryEventConsumerWorker : BackgroundService
{
    private readonly IRabbitMqConnectionProvider _connectionProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeliveryEventConsumerWorker> _logger;
    private const string QueueName = "edcl.delivery.collector";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DeliveryEventConsumerWorker(
        IRabbitMqConnectionProvider connectionProvider,
        IServiceScopeFactory scopeFactory,
        ILogger<DeliveryEventConsumerWorker> logger)
    {
        _connectionProvider = connectionProvider;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DeliveryEventConsumerWorker starting. Waiting for RabbitMQ connection...");

        IModel? channel = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_connectionProvider.IsConnected)
                {
                    _logger.LogWarning("RabbitMQ connection is not ready. Retrying in 5 seconds...");
                    await Task.Delay(5000, stoppingToken);
                    continue;
                }

                channel = _connectionProvider.CreateModel();
                channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += async (sender, ea) =>
                {
                    var routingKey = ea.RoutingKey;
                    var bodyText = Encoding.UTF8.GetString(ea.Body.Span);

                    _logger.LogInformation("📨 [RabbitMQ] Received event on {Queue}: RoutingKey={RoutingKey}", QueueName, routingKey);

                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

                        if (routingKey.EndsWith("started", StringComparison.OrdinalIgnoreCase))
                        {
                            var request = JsonSerializer.Deserialize<StartDeliveryJobRequestDto>(bodyText, JsonOptions);
                            if (request == null || string.IsNullOrWhiteSpace(request.DeliveryNo))
                            {
                                throw new InvalidOperationException($"Invalid StartDeliveryJob payload: {bodyText}");
                            }

                            _logger.LogInformation("🚀 [RabbitMQ] Processing start-job for DeliveryNo '{DeliveryNo}', LPCD '{Lpcd}', Value '{Val}'",
                                request.DeliveryNo, request.Lpcd, request.GetEffectiveValue());

                            var result = await mediator.Send(new StartDeliveryJobCommand(request), stoppingToken);

                            _logger.LogInformation("✅ [RabbitMQ] Delivery '{DeliveryNo}' started successfully. Vendor: {VendorName}, StartedAt: {StartedAt}",
                                result.DeliveryNo, result.VendorName, result.StartedAt);
                        }
                        else if (routingKey.EndsWith("completed", StringComparison.OrdinalIgnoreCase))
                        {
                            var request = JsonSerializer.Deserialize<CompleteDeliveryJobRequestDto>(bodyText, JsonOptions);
                            if (request == null || string.IsNullOrWhiteSpace(request.DeliveryNo))
                            {
                                throw new InvalidOperationException($"Invalid CompleteDeliveryJob payload: {bodyText}");
                            }

                            _logger.LogInformation("🏁 [RabbitMQ] Processing complete-job for DeliveryNo '{DeliveryNo}', CompletedBy '{CompletedBy}'",
                                request.DeliveryNo, request.CompletedBy);

                            var result = await mediator.Send(new CompleteDeliveryJobCommand(request), stoppingToken);

                            _logger.LogInformation("✅ [RabbitMQ] Delivery '{DeliveryNo}' completed successfully. Status: {Status}, CompletedAt: {CompletedAt}",
                                result.DeliveryNo, result.Status, result.CompletedAt);
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ [RabbitMQ] Unhandled routing key '{RoutingKey}' on queue '{Queue}'. Ignoring.",
                                routingKey, QueueName);
                        }

                        channel.BasicAck(ea.DeliveryTag, multiple: false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ [RabbitMQ] Error processing event with RoutingKey '{RoutingKey}'. Dead-lettering to edcl.dlx/edcl.dlq.",
                            routingKey);

                        // Nack without requeue -> RabbitMQ will send this to dead-letter exchange (edcl.dlx -> edcl.dlq)
                        channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                    }
                };

                channel.BasicConsume(
                    queue: QueueName,
                    autoAck: false,
                    consumer: consumer);

                _logger.LogInformation("🚀 DeliveryEventConsumerWorker actively consuming queue '{Queue}' for routing keys 'delivery.active.#'", QueueName);

                // Keep alive until stopped
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection error in DeliveryEventConsumerWorker. Re-establishing in 5 seconds...");
                channel?.Dispose();
                channel = null;
                await Task.Delay(5000, stoppingToken);
            }
        }

        channel?.Dispose();
        _logger.LogInformation("DeliveryEventConsumerWorker stopped.");
    }
}
