namespace GPSInterfacing.Vendors.Features.CreateGpsVendor;

public record CreateGpsVendorRequest(GpsVendorDto GpsVendor);
public record CreateGpsVendorResponse(Guid Id);

public class CreateGppVendorEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/gpsvendors", async (CreateGpsVendorRequest request, ISender sender) =>
            {
                var command = request.Adapt<CreateGpsVendorCommand>();

                var result = await sender.Send(command);

                var response = result.Adapt<CreateGpsVendorResponse>();

                return Results.Created($"/gpsvendors/{response.Id}", response);
            })
            .WithName("CreateGpsVendor")
            .Produces<CreateGpsVendorResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Create GpsVendor")
            .WithDescription("Create GpsVendor");
    }
}