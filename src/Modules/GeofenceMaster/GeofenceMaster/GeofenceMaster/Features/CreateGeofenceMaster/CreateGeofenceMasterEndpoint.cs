using GeofenceMaster.GeofenceMaster.Dtos;
using GeofenceMaster.GeofenceMaster.Models;

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
                                item.Bodies
                            )).ToList(),
                        request.GeofenceMaster.GeofenceMasterAuths?.Select(item =>
                            new GeofenceMasterAuthDto(
                                Guid.Empty,
                                item.GpsVendorId,
                                item.BaseUrl,
                                item.Method,
                                item.Authtype,
                                item.ContentType,
                                item.Username,
                                item.Password,
                                item.TokenPath,
                                item.Headers,
                                item.Params,
                                item.Bodies
                            )).ToList(),
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