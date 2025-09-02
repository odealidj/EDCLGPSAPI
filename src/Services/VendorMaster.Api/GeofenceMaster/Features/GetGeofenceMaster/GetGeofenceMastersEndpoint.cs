using GeofenceMaster.GeofenceMaster.Dtos;
using Shared.Pagination;

namespace GeofenceMaster.GeofenceMaster.Features.GetGeofenceMaster;

public record GetGeofenceMastersResponse(PaginatedResult<GeofenceMasterDto> GeofenceMasters);

public class GetGeofenceMastersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/geofencemaster", async ([AsParameters] GetGeoferenceMasterDto request, ISender sender) =>
            {
                var result = await sender.Send(new GetGeofenceMasterQuery(request));

                //var response = result.Adapt<GetGeofenceMastersResponse>();

                var response = result;

                return Results.Ok(response);
            })
            .WithName("GetGeofenceMasters")
            .Produces<GetGeofenceMastersResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Get GeofenceMasters")
            .WithDescription("Get GeofenceMasters");
    }
}