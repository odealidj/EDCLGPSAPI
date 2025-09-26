namespace VendorMaster.Api.Endpoints;

public class GeofenceDestinationEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/geofence-destinations")
            .WithTags("Geofence Destinations");

        // GET /api/v1/geofence-destinations
        group.MapGet("/", async (bool? activeOnly, ISender sender) =>
        {
            var result = await sender.Send(new GetGeofenceDestinationsQuery(activeOnly ?? true));
            return Results.Ok(result.Destinations);
        })
        .WithName("GetGeofenceDestinations")
        .Produces<List<GeofenceDestinationDto>>(StatusCodes.Status200OK)
        .WithSummary("Ambil daftar geofence pabrik Toyota TMMIN & supplier");
    }
}
