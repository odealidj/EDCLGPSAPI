using Ping.Data;
using Ping.Ping.Dtos;
using Shared.Contracts.CQRS;

namespace Ping.Ping.Features.HttpPing.CreateHttpPing;

public record CreateHttpPingCommand(RequestPingDto PingDto)
    : ICommand<CreateHttpPingResult>;
public record CreateHttpPingResult(ResponsePingDto PingDto);

public class CreateHttpPingHandler()
    : ICommandHandler<CreateHttpPingCommand, CreateHttpPingResult>
{
    public async Task<CreateHttpPingResult> Handle(CreateHttpPingCommand command, CancellationToken cancellationToken)
    {
        var ping = new Models.Ping(Guid.NewGuid(), command.PingDto.Message, DateTime.Now, "Http", command.PingDto.Url);
        /*dbContext.Pings.Add(ping);
        await dbContext.SaveChangesAsync(cancellationToken);
        */
        var result = new ResponsePingDto(ping.Id, ping.Message, ping.PingDate, ping.Protocol);
        return new CreateHttpPingResult(result);
    }
}