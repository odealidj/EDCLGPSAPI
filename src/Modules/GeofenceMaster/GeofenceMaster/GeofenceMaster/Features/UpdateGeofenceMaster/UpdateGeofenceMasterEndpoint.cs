using GeofenceMaster.GeofenceMaster.Dtos;
using GeofenceMaster.GeofenceMaster.Models;

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
                
                /*
                var lpcds = new List<GeofenceMasterLpcdDto>();
                foreach (var item in request.GeofenceMaster.Lpcds)
                {
                    if (request.GeofenceMaster.Id != null)
                    {
                        var lpcd = new GeofenceMasterLpcdDto(
                            Guid.NewGuid(),
                            request.GeofenceMaster.Id.Value,
                            item
                        );
                        lpcds.Add(lpcd);
                    }
                } 
                */

                
                
                var command = new UpdateGeofenceMasterCommand(
                    new GeofenceMasterDto(
                        request.GeofenceMaster.Id,
                        request.GeofenceMaster.VendorName,
                        //request.GeofenceMaster.Lpcds,
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
                                item.Id,
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
                                item.Id,
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
                                    item.Id,
                                    item.GpsVendorId,
                                    item.ResponseField,
                                    item.MappedField
                                )).ToList(),
                        request.GeofenceMaster.Lpcds.Select(item =>
                            new GeofenceMasterLpcdDto(
                                item.Id,
                                item.GpsVendorId,
                                item.Lpcd
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