using Delivery.Delivery.Dtos;

namespace Delivery.Delivery.Features.DeleteDelievryProgress;

public record DeleteDeliveryProgressRequest(DeleteDeliveryProgressDto DeliveryProgress);
public record DeleteDeliveryProgressResponse(bool IsSuccess);

public class DeleteDeliveryProgressEndpoint: ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/delivery-progress/{deliveryNo}", async (string deliveryNo, ISender sender) =>
            {
                var result = await sender.Send(new DeleteDeliveryProgressCommand(deliveryNo));

                var response = result.Adapt<DeleteDeliveryProgressResponse>();

                return Results.Ok(response);
            })
            .WithName("DeleteDeliveryProgress")
            .Produces<DeleteDeliveryProgressResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete  Delivery Progress")
            .WithDescription("Delete  Delivery Progress");
    }
}