using Carter;
using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Application.UseCases.BulkInsertDeliveryProgress;
using DeliveryTracking.Application.UseCases.BulkInsertGpsDelivery;
using DeliveryTracking.Application.UseCases.CompleteDeliveryJob;
using DeliveryTracking.Application.UseCases.DeleteDeliveryProgress;
using DeliveryTracking.Application.UseCases.GetActiveDeliverySessions;
using DeliveryTracking.Application.UseCases.GetDeliveryLiveTracking;
using DeliveryTracking.Application.UseCases.GetDeliveryProgress;
using DeliveryTracking.Application.UseCases.GetDeliverySession;
using DeliveryTracking.Application.UseCases.StartDeliveryJob;
using DeliveryTracking.Application.UseCases.TrackDelivery;
using DeliveryTracking.Application.UseCases.UpsertDeliveryProgress;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi.Models;
using Shared.Data.Mongo.DTOs;
using Shared.Data.Mongo.Repositories;
using Shared.Infrastructure.Idempotency;
using Shared.Pagination;

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

        // 7. POST /api/v1/deliveries/start-job (Start Job dari Mobile via EDCL_API)
        group.MapPost("/start-job", async (StartDeliveryJobRequestDto request, ISender sender) =>
        {
            var result = await sender.Send(new StartDeliveryJobCommand(request));
            return Results.Ok(result);
        })
        .WithName("StartDeliveryJob")
        .Produces<StartDeliveryJobResultDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Mulai sesi pelacakan GPS pengiriman armada (Start Job dipanggil oleh EDCL_API saat supir klik Start Job di Mobile App)")
        .WithDescription("Menerima DeliveryNo, Lpcd, Key (kunci mapping: PlatNo, VehicleId, Imei, DriverKtp), dan Val (nilai kunci). Menyimpan sesi ke PostgreSQL (edcl.t_delivery_tracking_session) dan mendaftarkan pemetaan in-memory ke Redis RAM untuk korelasi O(1).");

        // 8. POST /api/v1/deliveries/complete-job (Complete Job dari Mobile via EDCL_API)
        group.MapPost("/complete-job", async (CompleteDeliveryJobRequestDto request, ISender sender) =>
        {
            var result = await sender.Send(new CompleteDeliveryJobCommand(request));
            return Results.Ok(result);
        })
        .WithName("CompleteDeliveryJob")
        .Produces<CompleteDeliveryJobResultDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Selesaikan sesi pelacakan GPS pengiriman armada (Complete Job)");

        // 9. GET /api/v1/deliveries/session/{deliveryNo}
        group.MapGet("/session/{deliveryNo}", async (string deliveryNo, ISender sender) =>
        {
            var result = await sender.Send(new GetDeliverySessionQuery(deliveryNo));
            return Results.Ok(result);
        })
        .WithName("GetDeliverySession")
        .Produces<DeliveryTrackingSessionDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Ambil detail status sesi pelacakan pengiriman berdasarkan nomor DO");

        // 10. GET /api/v1/deliveries/sessions/active
        group.MapGet("/sessions/active", async (int? limit, ISender sender) =>
        {
            var result = await sender.Send(new GetActiveDeliverySessionsQuery(limit ?? 100));
            return Results.Ok(result);
        })
        .WithName("GetActiveDeliverySessions")
        .Produces<IReadOnlyList<DeliveryTrackingSessionDto>>(StatusCodes.Status200OK)
        .WithSummary("Ambil daftar seluruh sesi pelacakan pengiriman yang sedang aktif (STARTED / IN_TRANSIT)");

        // 11. GET /api/v1/deliveries/live/{deliveryNo}
        group.MapGet("/live/{deliveryNo}", async (string deliveryNo, ISender sender) =>
        {
            var result = await sender.Send(new GetDeliveryLiveTrackingQuery(deliveryNo));
            return Results.Ok(result);
        })
        .WithName("GetDeliveryLiveTracking")
        .Produces<DeliveryLiveTrackingDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Ambil status realtime dan posisi live pelacakan delivery (Redis-first sub-milidetik dengan status TRACKING_ACTIVE, DATA_MISMATCH, VENDOR_CONNECTION_ERROR, atau WAITING_TELEMETRY)");

        // 12. GET /api/v1/deliveries/raw-responses (Arsip JSON Asli MongoDB dengan Filter & Pagination)
        group.MapGet("/raw-responses", async (
            string? vendorId,
            string? vendorName,
            DateTime? fromDate,
            DateTime? toDate,
            string? search,
            int? pageIndex,
            int? pageSize,
            IGpsRawResponseRepository rawRepo,
            CancellationToken ct) =>
        {
            var filter = new RawResponseFilterRequest
            {
                VendorId = vendorId,
                VendorName = vendorName,
                FromDate = fromDate,
                ToDate = toDate,
                Search = search,
                PageIndex = pageIndex ?? 1,
                PageSize = pageSize ?? 20
            };
            var result = await rawRepo.QueryRawResponsesAsync(filter, ct);
            return Results.Ok(result);
        })
        .WithName("QueryDeliveryRawResponses")
        .Produces<PaginatedResult<RawResponseItemDto>>(StatusCodes.Status200OK)
        .WithSummary("Lihat arsip data raw response JSON asli dari vendor di MongoDB dengan filter dan paginasi")
        .WithDescription("Mendukung filter: vendorId, vendorName, fromDate, toDate, search (pencarian di URL/batch/nama), pageIndex, dan pageSize.");

        // 13. GET /api/v1/deliveries/raw-responses/{id}
        group.MapGet("/raw-responses/{id}", async (
            string id,
            IGpsRawResponseRepository rawRepo,
            CancellationToken ct) =>
        {
            var result = await rawRepo.GetRawResponseByIdAsync(id, ct);
            return result != null
                ? Results.Ok(result)
                : Results.NotFound(new { Message = $"Raw response dengan ID {id} tidak ditemukan di MongoDB." });
        })
        .WithName("GetDeliveryRawResponseById")
        .Produces<RawResponseItemDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Ambil detail satu dokumen data raw response JSON asli berdasarkan ID MongoDB");

        // --- Alias Routes untuk kesesuaian spesifikasi /api/v1/delivery-session/* ---
        var sessionGroup = app.MapGroup("/api/v1/delivery-session")
            .WithTags("Delivery Sessions");

        sessionGroup.MapPost("/start", async (StartDeliveryJobRequestDto request, ISender sender) =>
        {
            var result = await sender.Send(new StartDeliveryJobCommand(request));
            return Results.Ok(result);
        })
        .WithName("StartDeliverySessionAlias")
        .Produces<StartDeliveryJobResultDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Alias: Mulai sesi pelacakan GPS pengiriman armada (Start Job)");

        sessionGroup.MapPost("/complete", async (CompleteDeliveryJobRequestDto request, ISender sender) =>
        {
            var result = await sender.Send(new CompleteDeliveryJobCommand(request));
            return Results.Ok(result);
        })
        .WithName("CompleteDeliverySessionAlias")
        .Produces<CompleteDeliveryJobResultDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Alias: Selesaikan sesi pelacakan GPS pengiriman armada (Complete Job)");

        sessionGroup.MapGet("/{deliveryNo}", async (string deliveryNo, ISender sender) =>
        {
            var result = await sender.Send(new GetDeliverySessionQuery(deliveryNo));
            return Results.Ok(result);
        })
        .WithName("GetDeliverySessionAlias")
        .Produces<DeliveryTrackingSessionDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Alias: Ambil status sesi pelacakan pengiriman berdasarkan nomor DO");

        sessionGroup.MapGet("/live/{deliveryNo}", async (string deliveryNo, ISender sender) =>
        {
            var result = await sender.Send(new GetDeliveryLiveTrackingQuery(deliveryNo));
            return Results.Ok(result);
        })
        .WithName("GetDeliveryLiveTrackingAlias")
        .Produces<DeliveryLiveTrackingDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Alias: Ambil status realtime dan posisi live pelacakan delivery");
    }
}
