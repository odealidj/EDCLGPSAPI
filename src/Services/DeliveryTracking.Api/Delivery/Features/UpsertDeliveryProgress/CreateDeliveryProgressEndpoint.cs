using System.Text.Json.Serialization;
using Delivery.Delivery.Dtos;

namespace Delivery.Delivery.Features.UpsertDeliveryProgress;

public record CreateDeliveryProgressRequest(DeliveryProgressDto DeliveryProgress);
public record CreateDeliveryProgressResponse(Guid Id);

public class CreateDeliveryProgressEndpoint: ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/RDeliveryOnProgress", async (DeliveryProgressDto request, ISender sender) =>
            {
                try
                {
                    // Map the request to the command
                    var command = new CreateDeliveryProgressCommand(
                        request // Ensure this is properly passed
                    );

                    // Send the command using MediatR
                    var result = await sender.Send(command);

                    // Map the result to the response
                    var response = result.Adapt<CreateDeliveryProgressResponse>();
                    
                    // Wrap the successful response
                    var successResponse = new ApiResponse<object>
                    {
                        Data = new { isSuccess = true },
                        Status = 200,
                        Message = "Data valid"
                    };

                    // Return the created response
                    return Results.Ok(successResponse);
                }
                catch (Exception ex)
                {
                    // Wrap the error response
                    var errorResponse = new ApiResponse<object>
                    {
                        Data = null,
                        Status = 400,
                        Message = "Bad Request"
                    };

                    // Return the error response with status code 400
                    return Results.BadRequest(errorResponse);
                }
            })
            .Produces<CreateDeliveryProgressResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Create Delivery Progress")
            .WithDescription("Create delivery progress");
    }
}

public record ApiResponse<T>
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public T? Data { get; init; }
    public int Status { get; init; }
    public string Message { get; init; }
}