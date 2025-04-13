using GeofenceMaster.GeofenceMaster.Dtos;

namespace GeofenceMaster.GeofenceMaster.Features.UpdateGeofenceMaster;

public record UpdateGeofenceMasterRequest(GeofenceMasterDto GeofenceMaster);
public record UpdateGeofenceMasterResponse(bool IsSuccess);

public class UpdateGeofenceMasterEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/geofencemaster", async (UpdateGeofenceMasterRequest request, ISender sender) =>
            {
                // var command = request.Adapt<UpdateGeofenceMasterCommand>();
                var command = new UpdateGeofenceMasterCommand(
                    new GeofenceMasterDto(
                        request.GeofenceMaster.Id,
                        request.GeofenceMaster.VendorName,
                        request.GeofenceMaster.LpcdId,
                        request.GeofenceMaster.Timezone,
                        request.GeofenceMaster.RequiredAuth,
                        request.GeofenceMaster.GeofenceMasterEndpoints.Select(item =>
                            new GeofenceMasterEndpointDto(
                                item.Id,
                                item.GpsVendorId,
                                item.BaseUrl,
                                item.Method,
                                item.Headers,
                                item.Params,
                                item.Bodies
                            )).ToList(),
                        request.GeofenceMaster.GeofenceMasterAuths.Select(item =>
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

                var response = result.Adapt<UpdateGeofenceMasterResponse>();

                return Results.Ok(response);
            })
            .WithName("UpdateGeofenceMaster")
            .Produces<UpdateGeofenceMasterResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update GeofenceMaster")
            .WithDescription("Update GeofenceMaster");
    }
}