using Ping.Data;
using Ping.Ping.Dtos;
using Shared.Contracts.CQRS;

namespace Ping.Ping.Feature.HttpPing;

public record HttpPingCommand(RequestPingDto Ping)
    : ICommand<HttpPingResult>;
public record HttpPingResult(ResponsePingDto Ping);

public class GetHttpPingHandler(PingDbContext dbContext): 
    ICommandHandler<HttpPingCommand, HttpPingResult>
{
    public async Task<HttpPingResult> Handle(HttpPingCommand command, CancellationToken cancellationToken)
    {
        var ping = new Models.Ping(Guid.NewGuid(), command.Ping.Message, DateTime.Now, "Http" );

        dbContext.Pings.Add(ping);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new HttpPingResult(new ResponsePingDto(ping.Id, ping.Message, ping.PingDate, ping.Protocol));
    }
}