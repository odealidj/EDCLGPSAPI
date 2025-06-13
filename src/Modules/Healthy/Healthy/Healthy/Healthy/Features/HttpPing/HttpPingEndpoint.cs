using Carter;
using Healthy.Healthy.Dtos;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Healthy.Healthy.Features.HttpPing;

public record HttpPingRequest(HttpPingRequestDto HttpPing);
public record HttpPingResponse(HttpPingResponseDto HttpPing);

public class HttpPingEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/http-ping", async (HttpPingRequestDto command, ISender sender, HttpContext httpContext) =>
            {
                var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";

                var result  = await sender.Send(new HttpPingCommand(
                    new HttpPingRequestDto(
                        string.IsNullOrEmpty(command.Url)? baseUrl: command.Url
                        )));
                
                //var result = await sender.Send(new DeleteGeofenceMasterCommand(id));

                var response = result.Adapt<HttpPingResponse>();

                return Results.Ok(response);
            })
            .WithName("HttpPing")
            .Produces<HttpPingResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Http Ping")
            .WithDescription("Http Ping");
    }
}