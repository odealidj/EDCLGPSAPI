using Healthy.Data;
using Healthy.Healthy.Dtos;
using Healthy.Healthy.Models;

namespace Healthy.Healthy.Features.HttpPing;

public record HttpPingCommand(HttpPingRequestDto HttpPing)
    : ICommand<HttpPingResult>;
public record HttpPingResult(HttpPingResponseDto HttpPing);

internal class HttpPingHandler(PingDbContext dbContext)
    : ICommandHandler<HttpPingCommand, HttpPingResult>
{
    public async Task<HttpPingResult> Handle(HttpPingCommand command, CancellationToken cancellationToken)
    {
        var dateNow = DateTime.Now;
        var protocol = "Http";
        
        var newPing = Ping.Create(
            Guid.NewGuid(),
            "Pong",
            dateNow, 
            protocol,
            command.HttpPing.Url
        );
        
        await dbContext.Pings.AddAsync(newPing, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        
        //string message, DateTime timestamp, string protocol, string url 
        return new HttpPingResult(new HttpPingResponseDto(
            newPing.Message ?? string.Empty, 
            dateNow, 
            protocol, 
            command.HttpPing.Url ?? string.Empty));
    }
}