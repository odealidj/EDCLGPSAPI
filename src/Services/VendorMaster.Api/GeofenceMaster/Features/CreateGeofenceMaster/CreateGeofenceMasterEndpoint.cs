using GeofenceMaster.GeofenceMaster.Dtos;

namespace GeofenceMaster.GeofenceMaster.Features.CreateGeofenceMaster;

public record CreateGeofenceMasterRequest(GeofenceMasterDto GeofenceMaster);
public record CreateGeofenceMasterResponse(Guid Id);
public class CreateGeofenceMasterEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/geofencemaster", async (CreateGeofenceMasterRequest request, ISender sender) =>
            {
                ////var command = request.Adapt<CreateGeofenceMasterCommand>();
                ///

                var command = new CreateGeofenceMasterCommand(
                    new GeofenceMasterDto(
                        Guid.Empty,
                        request.GeofenceMaster.VendorName,
                        ////request.GeofenceMaster.Lpcds,
                        request.GeofenceMaster.Timezone,
                        request.GeofenceMaster.RequiredAuth,
                        request.GeofenceMaster.AuthType,
                        request.GeofenceMaster.Username,
                        request.GeofenceMaster.Password,
                        request.GeofenceMaster.ProcessingStrategy,
                        request.GeofenceMaster.ProcessingStrategyPathData,
                        request.GeofenceMaster.ProcessingStrategyPathKey,
                        request.GeofenceMaster.GeofenceMasterEndpoints.Select(item =>
                            new GeofenceMasterEndpointDto(
                                Guid.Empty,
                                item.GpsVendorId,
                                item.BaseUrl,
                                item.Method,
                                item.ContentType,
                                item.Headers,
                                item.Params,
                                item.Bodies,
                                item.VarParams,
                                item.MaxPath
                            )).ToList(),
                        request.GeofenceMaster.GeofenceMasterAuth != null ? new GeofenceMasterAuthDto(
                            Guid.Empty,
                            request.GeofenceMaster.GeofenceMasterAuth.GpsVendorId,
                            request.GeofenceMaster.GeofenceMasterAuth.BaseUrl,
                            request.GeofenceMaster.GeofenceMasterAuth.Method,
                            request.GeofenceMaster.GeofenceMasterAuth.Authtype,
                            request.GeofenceMaster.GeofenceMasterAuth.ContentType,
                            request.GeofenceMaster.GeofenceMasterAuth.Username,
                            request.GeofenceMaster.GeofenceMasterAuth.Password,
                            request.GeofenceMaster.GeofenceMasterAuth.TokenPath,
                            request.GeofenceMaster.GeofenceMasterAuth.Headers,
                            request.GeofenceMaster.GeofenceMasterAuth.Params,
                            request.GeofenceMaster.GeofenceMasterAuth.Bodies
                            ) : null,
                        request.GeofenceMaster.GeofenceMasterMappings.Select(item =>
                            new GeofenceMasterMappingDto(
                                item.GpsVendorId,
                                item.ResponseField,
                                item.MappedField
                            )).ToList(),
                        request.GeofenceMaster.Lpcds.Select(item =>
                            new GeofenceMasterLpcdDto(
                                item.GpsVendorId,
                                item.Lpcd
                            )).ToList()
                    )
                );

                var result = await sender.Send(command);

                var response = result.Adapt<CreateGeofenceMasterResponse>();

                return Results.Created($"/geofencemaster/{response.Id}", response);
            })
            .Produces<CreateGeofenceMasterResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Create Geofence Master")
            .WithDescription("Create geofence master");
    }
    
    
}