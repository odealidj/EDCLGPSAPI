using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Ping.Ping.Dtos;

namespace Ping.Ping.Features.HttpPing;

public record HttpPingResponse(ResponsePingDto Ping);

public class GetHttpPingEndpoint: ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/http-ping/{message}", async (string message, ISender sender) =>
            {
                var result = await sender.Send(new HttpPingCommand(new RequestPingDto(message)));
                
                var response = result.Adapt<HttpPingResult>();

                return Results.Ok(response);
            })
            .WithName("HttpPing")
            .Produces<HttpPingResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Http Ping")
            .WithDescription("Pint Edcl GPS API");
    }
}