using Carter;
using Microsoft.AspNetCore.Mvc;
using PositionQuery.Api.Models;
using PositionQuery.Api.Services;

namespace PositionQuery.Api.Endpoints;

public class PositionEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/positions")
            .WithTags("Positions");

        // 1. Single vehicle quick lookup by license plate (Redis-first)
        group.MapGet("/{platNo}", async (string platNo, IPositionQueryService service, CancellationToken ct) =>
        {
            var position = await service.GetLastPositionByPlatNoAsync(platNo, ct);
            return position != null 
                ? Results.Ok(position) 
                : Results.NotFound(new { Message = $"No telemetry found for plate {platNo}" });
        })
        .WithName("GetPositionByPlate")
        .WithSummary("Get current vehicle position by license plate")
        .WithDescription("Checks Redis cache first (<1ms) with transparent fallback to PostgreSQL.")
        .Produces<LastPositionDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // 2. Multi-filter & paginated position query
        group.MapGet("/", async ([AsParameters] LastPositionRequest request, IPositionQueryService service, CancellationToken ct) =>
        {
            var result = await service.QueryPositionsAsync(request, ct);
            return Results.Ok(result);
        })
        .WithName("QueryPositions")
        .WithSummary("Query historical & current vehicle positions")
        .Produces<PaginatedPositionResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // 3. Backward Compatibility Endpoint: /gps-lastposition
        app.MapGet("/gps-lastposition", async ([AsParameters] LastPositionRequest request, IPositionQueryService service, CancellationToken ct) =>
        {
            var result = await service.QueryPositionsAsync(request, ct);
            // Wrap in legacy response shape: { lastPosition: { pageIndex, pageSize, count, data } }
            return Results.Ok(new { LastPosition = result });
        })
        .WithTags("Legacy Compatibility")
        .WithName("LegacyGpsLastPosition")
        .WithSummary("Legacy compatibility endpoint for GPS last position")
        .Produces<object>(StatusCodes.Status200OK);
    }
}
