using GeofenceWorker.Services.RabbitMq;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GeofenceWorker.Workers.Features.Worker;

public class AmqpMessage(
    IRabbitMqService rabbitMqService,
    ILogger<Worker> logger)
    : BackgroundService
{
    private readonly ILogger<Worker> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly IRabbitMqService _rabbitMqService = rabbitMqService ?? throw new ArgumentNullException(nameof(rabbitMqService));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("System ping running at: {time}", DateTimeOffset.Now);
                
                var message = new PingMessage
                {
                    Message = "Pong",
                    Timestamp = DateTimeOffset.Now
                };
                
                string routingKey = "system.ping";
                await _rabbitMqService.PublishAsync(message, routingKey);
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); 
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred in the worker: {Message}", e.Message);
        }
    }
    

}

public class PingMessage
{
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}