using Delivery.Delivery.Dtos;

namespace Delivery.Delivery.Features.UpsertDeliveryProgress;

public record CreateDeliveryProgressRequest(DeliveryProgressDto DeliveryProgress);
public record CreateDeliveryProgressResponse(Guid Id);

public class CreateDeliveryProgressEndpoint: ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/RDeliveryOnProgress", async (CreateDeliveryProgressRequest request, ISender sender) =>
            {
                // Map the request to the command
                var command = new CreateDeliveryProgressCommand(
                    request.DeliveryProgress // Ensure this is properly passed
                );

                // Send the command using MediatR
                var result = await sender.Send(command);

                // Map the result to the response
                var response = result.Adapt<CreateDeliveryProgressResponse>();

                // Return the created response
                return Results.Created($"/delivery-progress/{response.Id}", response);
            })
            .Produces<CreateDeliveryProgressResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Create Delivery Progress")
            .WithDescription("Create delivery progress");
    }
}