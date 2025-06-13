using Microsoft.AspNetCore.Mvc;
using Ping.Ping.Dtos;

namespace Ping.Ping.Features.HttpPing.CreateHttpPing;

public record CreateHttpPingRequest(RequestPingDto HttpPing);
public record CreateHttpPingResponse(ResponsePingDto HttpPing);

public class CreateHttpPingEndpoint: ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        
        app.MapPost("/http-ping", async ([FromBody]RequestPingDto request, ISender sender, HttpContext httpContext) =>
            {
                var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";

                var command = new CreateHttpPingCommand(
                    new RequestPingDto(
                        request.Message,  
                        string.IsNullOrEmpty(request.Url)? $"{baseUrl}" :request.Url ));

                var result = await sender.Send(command);

                var response = result.Adapt<CreateHttpPingResponse>();

                return Results.Ok( response.HttpPing.Id);
            })
            .Produces<CreateHttpPingResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Create Geofence Master")
            .WithDescription("Create geofence master");
    }
}