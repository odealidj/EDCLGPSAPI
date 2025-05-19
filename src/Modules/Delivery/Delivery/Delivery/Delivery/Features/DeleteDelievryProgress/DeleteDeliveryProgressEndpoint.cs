using System.Text.Json.Serialization;
using Delivery.Delivery.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Delivery.Features.DeleteDelievryProgress;

public record DeleteDeliveryProgressRequest(DeleteDeliveryProgressDto DeliveryProgress);
public record DeleteDeliveryProgressResponse(bool IsSuccess);

public class DeleteDeliveryProgressEndpoint: ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        /*
        app.MapDelete("/api/v1/RDeliveryOnProgress/{deliveryNo}", async (string deliveryNo, ISender sender) =>
            {
                try
                {
                    var result = await sender.Send(new DeleteDeliveryProgressCommand(deliveryNo));

                    var response = result.Adapt<DeleteDeliveryProgressResponse>();

                    // Wrap the successful response
                    var successResponse = new ApiResponse<object>
                    {
                        Data = new { isSuccess = true },
                        Status = 200,
                        Message = "Data valid"
                    };

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
            .WithName("DeleteDeliveryProgress")
            .Produces<DeleteDeliveryProgressResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete  Delivery Progress")
            .WithDescription("Delete  Delivery Progress");
        
        */
        
        app.MapDelete("/api/v1/RDeliveryOnProgress", async ([FromBody]DeleteDeliveryProgressDto request, ISender sender) =>
            {
                try
                {
                    var result = await sender.Send(new DeleteDeliveryProgressCommand(request.DeliveryNo));

                    var response = result.Adapt<DeleteDeliveryProgressResponse>();

                    // Wrap the successful response
                    var successResponse = new ApiResponse<object>
                    {
                        Data = new { isSuccess = true },
                        Status = 200,
                        Message = "Data valid"
                    };

                    return Results.Ok(successResponse);
                }
                catch (Exception ex)
                {
                    // Wrap the error response
                    var errorResponse = new ApiResponse<object>
                    {
                        Status = 400,
                        Message = "Bad Request"
                    };

                    // Return the error response with status code 400
                    return Results.BadRequest(errorResponse);
                }
            })
            .WithName("DeleteDeliveryProgress")
            .Produces<DeleteDeliveryProgressResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete  Delivery Progress")
            .WithDescription("Delete  Delivery Progress");

    }
}

public record ApiResponse<T>
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public T? Data { get; init; }
    public int Status { get; init; }
    public string Message { get; init; }
}

public class DeleteDelieveryRequest
{
    public string DeliveryNo { get; set; }
}