namespace GeofenceMaster.GeofenceMaster.Features.DeleteGeofenceMaster;

//public record DeleteProductRequest(Guid Id);
public record DeleteGeofenceMasterResponse(bool IsSuccess);

public class DeleteGeofenceMasterEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/geofencemaster/{id}", async (Guid id, ISender sender) =>
            {
                var result = await sender.Send(new DeleteGeofenceMasterCommand(id));

                var response = result.Adapt<DeleteGeofenceMasterResponse>();

                return Results.Ok(response);
            })
            .WithName("DeleteGeofenceMasterResponse")
            .Produces<DeleteGeofenceMasterResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete GeofenceMasterResponse")
            .WithDescription("Delete GeofenceMasterResponse");
    }
}