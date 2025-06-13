using GeofenceWorker.Workers.Dtos;

namespace GeofenceWorker.Workers.Features.GetLastPosition;

public record GetLastPositionResponse(PaginatedResult<LastPositionResponseDto> LastPosition);

public class GetLastPositionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/gps-lastposition", async ([AsParameters] LastPositionRequestDto request, ISender sender) =>
            {
                var result = await sender.Send(new GetLastPositionQuery(request));

                //var response = result.Adapt<GetGeofenceMastersResponse>();

                var response = result;

                return Results.Ok(response);
            })
            .WithName("GPS-LastPosition")
            .Produces<GetLastPositionResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("GPS LastPosition")
            .WithDescription("GPS LastPosition");
    }
}