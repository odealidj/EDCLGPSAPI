using Carter;
using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Application.UseCases.BulkInsertDeliveryProgress;
using DeliveryTracking.Application.UseCases.BulkInsertGpsDelivery;
using DeliveryTracking.Application.UseCases.DeleteDeliveryProgress;
using DeliveryTracking.Application.UseCases.GetDeliveryProgress;
using DeliveryTracking.Application.UseCases.TrackDelivery;
using DeliveryTracking.Application.UseCases.UpsertDeliveryProgress;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi.Models;
using Shared.Infrastructure.Idempotency;

namespace DeliveryTracking.Endpoints;

// modul routing carter untuk seluruh endpoint layanan delivery tracking
public class DeliveryTrackingEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/deliveries")
            .WithTags("Delivery Tracking");

        // 1. GET /api/v1/deliveries/track?deliveryNo=...&density=1
        group.MapGet("/track", async (string deliveryNo, int? density, ISender sender) =>
        {
            var result = await sender.Send(new TrackDeliveryQuery(deliveryNo, density ?? 1));
            return Results.Ok(result);
        })
        .WithName("TrackDelivery")
        .Produces<IReadOnlyList<TrackDeliveryPointDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Ambil jejak koordinat GPS pengiriman armada (Stored Procedure PostgreSQL)");

        // 2. GET /api/v1/deliveries/progress/{deliveryNo}
        group.MapGet("/progress/{deliveryNo}", async (string deliveryNo, ISender sender) =>
        {
            var result = await sender.Send(new GetDeliveryProgressQuery(deliveryNo));
            return Results.Ok(result);
        })
        .WithName("GetDeliveryProgress")
        .Produces<DeliveryProgressDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Ambil status progres pengiriman berdasarkan nomor DO");

        // 3. POST /api/v1/deliveries/progress (Upsert)
        group.MapPost("/progress", async (DeliveryProgressDto request, ISender sender) =>
        {
            var result = await sender.Send(new UpsertDeliveryProgressCommand(request));
            return Results.Ok(result);
        })
        .RequireIdempotency()
        .WithName("UpsertDeliveryProgress")
        .Produces<SaveDeliveryProgressResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Simpan atau update status progres pengiriman armada")
        .WithOpenApi(operation =>
        {
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-Idempotency-Key",
                In = ParameterLocation.Header,
                Required = true,
                Description = "Kunci idempotensi (UUID) untuk mencegah eksekusi duplikat.",
                Schema = new OpenApiSchema { Type = "string", Format = "uuid" }
            });
            return operation;
        });

        // 4. POST /api/v1/deliveries/progress/bulk (Bulk Insert via PostgreSQL Binary COPY)
        group.MapPost("/progress/bulk", async (BulkInsertDeliveryProgressRequestDto request, ISender sender) =>
        {
            var result = await sender.Send(new BulkInsertDeliveryProgressCommand(request.Items));
            return Results.Ok(result);
        })
        .WithName("BulkInsertDeliveryProgress")
        .Produces<BulkInsertResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Bulk insert data progres delivery secara massal (PostgreSQL Binary COPY stream)");

        // 5. POST /api/v1/deliveries/telemetry/bulk (Bulk Insert Titik GPS via Binary COPY)
        group.MapPost("/telemetry/bulk", async (BulkInsertGpsDeliveryRequestDto request, ISender sender) =>
        {
            var result = await sender.Send(new BulkInsertGpsDeliveryCommand(request.Items));
            return Results.Ok(result);
        })
        .WithName("BulkInsertGpsTelemetry")
        .Produces<BulkInsertResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Bulk insert koordinat titik GPS telemetri armada secara massal (Binary COPY stream)");

        // 6. DELETE /api/v1/deliveries/progress/{deliveryNo}
        group.MapDelete("/progress/{deliveryNo}", async (string deliveryNo, ISender sender) =>
        {
            var result = await sender.Send(new DeleteDeliveryProgressCommand(deliveryNo));
            return Results.Ok(result);
        })
        .WithName("DeleteDeliveryProgress")
        .Produces<DeleteDeliveryProgressResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Hapus status progres pengiriman berdasarkan nomor DO");
    }
}
