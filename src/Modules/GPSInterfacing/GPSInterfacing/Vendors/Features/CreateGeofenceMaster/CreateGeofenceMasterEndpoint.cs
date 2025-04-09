namespace GPSInterfacing.Vendors.Features.CreateGeofenceMaster;

public record CreateGeofenceMasterRequest(GeofenceMasterDto GeofenceMaster);
public record CreateGeofenceMasterResponse(Guid Id);

public class CreateGeofenceMasterEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/geofencemaster", async (CreateGeofenceMasterRequest request, ISender sender) =>
            {
                ////var command = request.Adapt<CreateGeofenceMasterCommand>();
                
                var command = new CreateGeofenceMasterCommand(
                    new GeofenceMasterDto(
                        Guid.NewGuid(),
                        request.GeofenceMaster.VendorName,
                        request.GeofenceMaster.LpcdId,
                        request.GeofenceMaster.Timezone,
                        request.GeofenceMaster.RequiredAuth,
                        request.GeofenceMaster.Items.Select(item =>
                            new GeofenceMasterAuthDto(
                                item.Id,
                                item.GpsVendorId,
                                item.BaseUrl,
                                item.Method,
                                item.Authtype,
                                item.Headers,
                                item.Params,
                                item.Bodies
                            )).ToList()
                    )
                );

                var result = await sender.Send(command);

                var response = result.Adapt<CreateGeofenceMasterResponse>();

                return Results.Created($"/geofencemaster/{response.Id}", response);
            })
            .Produces<CreateGeofenceMasterResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Create Geofence Master")
            .WithDescription("Create geofence master");
    }
    
    
}

