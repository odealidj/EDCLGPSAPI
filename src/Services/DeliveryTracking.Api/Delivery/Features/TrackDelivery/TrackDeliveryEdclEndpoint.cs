using Delivery.Delivery.Dtos;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Delivery.Delivery.Features.TrackDelivery;

public record TrackDeliveryEdclResponse(List<TrackDeliveryEdclResponseDto> TrackDelivery);

public class TrackDeliveryEdclEndpoint: ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
                           
        app.MapGet("/api/v1/gps-data-edcl/track-delivery", async ([AsParameters] TrackDeliveryEdclRequestDto request, ISender sender) =>
            {
                // Map the request to the command
                var command = new TrackDeliveryEdclCommand(request);

                // Send the command using MediatR
                var result = await sender.Send(command);

                // Map the result to the response
                ////var response = result.Adapt<TrackDeliveryEdclResponse>();

                // Wrap the result in an ApiResponse object
                var response = new ApiResponse<List<TrackDeliveryEdclResponseDto>>
                {
                    Data = result.TrackDeliveryResult
                };
                
                // Return the created response
                return Results.Ok(response);
            })
            .Produces<TrackDeliveryEdclResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Track Delivery Edcl")
            .WithDescription("Track Delivery Edcl");
    }
}

public record ApiResponse<T>
{
    public T Data { get; init; }
}