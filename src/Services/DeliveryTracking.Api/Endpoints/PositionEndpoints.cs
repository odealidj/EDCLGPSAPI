using Carter;
using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Application.Ports;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DeliveryTracking.Endpoints;

// modul routing carter untuk kueri posisi armada (dilebur dari positionquery.api)
public class PositionEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/positions")
            .WithTags("Positions");

        // 1. GET /api/v1/positions/{platNo} (Redis-first lookup by license plate)
        group.MapGet("/{platNo}", async (string platNo, IPositionQueryService service, CancellationToken ct) =>
        {
            var position = await service.GetLastPositionByPlatNoAsync(platNo, ct);
            return position != null
                ? Results.Ok(position)
                : Results.NotFound(new { Message = $"Tidak ada data telemetri untuk plat {platNo}" });
        })
        .WithName("GetPositionByPlate")
        .WithSummary("Ambil posisi terkini armada berdasarkan nomor plat kendaraan (Redis-first <1ms dengan fallback PostgreSQL ADO.NET)")
        .Produces<LastPositionDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // 2. GET /api/v1/positions (Multi-filter & paginated position query)
        group.MapGet("/", async ([AsParameters] LastPositionRequest request, IPositionQueryService service, CancellationToken ct) =>
        {
            var result = await service.QueryPositionsAsync(request, ct);
            return Results.Ok(result);
        })
        .WithName("QueryPositions")
        .WithSummary("Kueri multi-filter dan paginasi posisi historis & posisi terkini armada")
        .Produces<PaginatedPositionResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // 3. GET /gps-lastposition (Backward compatibility legacy endpoint)
        app.MapGet("/gps-lastposition", async ([AsParameters] LastPositionRequest request, IPositionQueryService service, CancellationToken ct) =>
        {
            var result = await service.QueryPositionsAsync(request, ct);
            return Results.Ok(new { LastPosition = result });
        })
        .WithTags("Legacy Compatibility")
        .WithName("LegacyGpsLastPosition")
        .WithSummary("Endpoint kompatibilitas legacy untuk last position armada ({ LastPosition: { ... } })")
        .Produces<object>(StatusCodes.Status200OK);
    }
}
